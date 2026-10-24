using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public sealed partial class UniversalDocWorkflowOrchestrator
{
    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    private static string DraftConfigurationIdentity(UniversalDocSession s) => Hash(JsonSerializer.SerializeToUtf8Bytes(new {
        Profile = s.MappingProfile with { Version = 1, ExtractionSchema = s.MappingProfile.ExtractionSchema with { Version = 1 }, ExcelProjectionMap = s.MappingProfile.ExcelProjectionMap with { ApprovedLayoutFingerprint = null } },
        Layout = s.Proposal is null ? null : s.Proposal.Layout with { Sheets = s.Proposal.Layout.Sheets.OrderBy(x => x.Worksheet, StringComparer.Ordinal).Select(sheet => sheet with {
            Headers = sheet.Headers.OrderBy(x => x.Source.Key, StringComparer.Ordinal).ToImmutableArray(),
            Items = sheet.Items with { Columns = sheet.Items.Columns.OrderBy(x => x.Source.Key, StringComparer.Ordinal).ToImmutableArray() }
        }).ToImmutableArray() }
    }, DraftSnapshot.Json));
    public (DraftInfo Draft, StudioSessionView Session) SaveDraft(IUniversalDocDraftStore drafts, string id, long version, string? draftId, long revision)
    {
        var actor = Actor();
        // Atomic authorized snapshot capture. Database failure is never reported as Saved; reload the advanced runtime version before retrying.
        var s = store.Update(actor, id, version, s => {
            if (s.Workflow != "example-template" || s.Template.IsEmpty || s.SchemaDraft is null) throw new StudioFault(400, "DRAFT_WORKFLOW_UNSUPPORTED");
            SourceIntake.RequireBudget(s); return s;
        });
        var view = View(s, actor);
        var valueFields = view.Fields.Where(f => f.Source != "Missing" && s.Proposal is not null && !s.Proposal.OmittedOptionalFields.Contains(new(Enum.Parse<FieldScope>(f.Scope), f.Key))).ToArray();
        var fields = s.Proposal is null ? ImmutableArray<SchemaFieldCorrection>.Empty : s.SchemaDraft!.Fields.Select(f => {
            var c = f.Omit ? null : f.Candidates[f.Candidate!.Value];
            return new SchemaFieldCorrection(f.Key, f.Label, f.Scope, f.Type, f.Required, f.Omit, null, c?.Worksheet, c?.Cell, c?.Column, c?.PrototypeRow);
        }).ToImmutableArray();
        ManualSample? sample = s.Document is null ? null : new(
            valueFields.Where(f => f.Scope == "Header").ToImmutableDictionary(f => f.Key, f => f.Value ?? ""),
            Enumerable.Range(0, s.Document.LineItems.Length).Select(row => valueFields.Where(f => f.Scope == "LineItem" && f.Row == row).ToImmutableDictionary(f => f.Key, f => f.Value ?? "")).ToImmutableArray());
        var snapshot = new DraftSnapshot(1, s.Workflow, s.Template.ToArray(), s.TemplateSha256!, s.Example.ToArray(), s.Example.IsEmpty ? null : Hash(s.Example.ToArray()), fields,
            view.Mappings.Select(m => new StudioSheetMapping(m.Sheet, m.StartRow, m.Headers, m.Columns)).ToImmutableArray(), s.ValidationRules, sample,
            s.Sources.Select(x => new DraftSource(x.Bytes.ToArray(), x.Sha256, x.MediaType)).ToImmutableArray(), s.Version, DraftConfigurationIdentity(s));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, DraftSnapshot.Json);
        if (bytes.Length > DraftSnapshot.MaximumPayload) throw new StudioFault(400, "DRAFT_PAYLOAD_LIMIT");
        try { return (drafts.Save(actor, draftId, revision, bytes), view); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public StudioSessionView ResumeDraft(IUniversalDocDraftStore drafts, string id, long revision, TimeProvider time)
    {
        var actor = Actor(); var saved = drafts.Read(actor, id, revision); DraftSnapshot snapshot;
        try { snapshot = JsonSerializer.Deserialize<DraftSnapshot>(saved.Payload, DraftSnapshot.Json) ?? throw new JsonException(); }
        catch (JsonException) { throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID"); }
        finally { CryptographicOperations.ZeroMemory(saved.Payload); }
        if (snapshot.Format != 1 || snapshot.Workflow != "example-template" || snapshot.Template is null || snapshot.Example is null || snapshot.Sources.IsDefault || snapshot.Sources.Length > 4 || snapshot.Fields.IsDefault || snapshot.Mappings.IsDefault || snapshot.Rules.IsDefault || snapshot.SavedSessionVersion < 1 || Hash(snapshot.Template) != snapshot.TemplateSha256 || (snapshot.Example.Length == 0 ? snapshot.ExampleSha256 is not null : Hash(snapshot.Example) != snapshot.ExampleSha256)) throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID");
        var lease = drafts.AcquireResume(actor, id, revision); StudioSessionView? session = null;
        try {
            session = Create(snapshot.Workflow); session = Upload(session.Id, session.Version, snapshot.Template, "template.xlsx");
            if (snapshot.Example.Length > 0) session = UploadLearning(session.Id, session.Version, snapshot.Example, "example.xlsx", true);
            foreach (var source in snapshot.Sources) {
                if (source is null || source.Bytes is null || Hash(source.Bytes) != source.Sha256) throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID");
                var extension = source.MediaType switch { "image/png" => ".png", "image/jpeg" => ".jpg", "image/webp" => ".webp", "application/pdf" => ".pdf", _ => throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID") };
                session = AddSource(session.Id, session.Version, source.Bytes, "source" + extension, source.MediaType, time);
            }
            if (!snapshot.Fields.IsEmpty) {
                session = CorrectSchema(session.Id, session.Version, snapshot.Fields);
                session = ChangeMultisheetMapping(session.Id, session.Version, snapshot.Mappings);
                session = ChangeValidationRules(session.Id, session.Version, snapshot.Rules);
                // Correspondence is a confirmation boundary. A saved draft may still need mapping corrections.
                // ConfirmMapping reruns it before any document can become export-eligible.
            } else if (!snapshot.Mappings.IsEmpty || !snapshot.Rules.IsEmpty || snapshot.Values is not null) throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID");
            if (DraftConfigurationIdentity(store.Read(actor, session.Id)) != snapshot.ConfigurationSha256) throw new StudioFault(409, "DRAFT_FINGERPRINT_MISMATCH");
            if (snapshot.Values is not null) session = RestoreDraftValues(session, snapshot.Values, actor);
            // Final expected-revision/expiry check after workbook reconstruction; no old approval exists anywhere in the contract.
            drafts.CompleteResume(actor, lease, id, revision, session.ExpiresAt);
            return View(store.Update(actor, session.Id, session.Version, current => current), actor);
        } catch {
            if (session is not null) { try { Remove(session.Id, Read(session.Id).Version); } catch (StudioFault) { } }
            try { drafts.ReleaseResume(lease); } catch (StudioFault) { } throw;
        }
    }
    private StudioSessionView RestoreDraftValues(StudioSessionView session, ManualSample sample, UniversalDocActor actor)
    {
        if (sample.Header is null || sample.Header.Count > 32 || sample.Rows.IsDefaultOrEmpty || sample.Rows.Length > 20 || sample.Rows.Any(r => r is null || r.Count > 32) || sample.Header.Concat(sample.Rows.SelectMany(r => r)).Any(x => x.Key.Length > 64 || x.Value?.Length > 2000)) throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID");
        return View(store.Update(actor, session.Id, session.Version, s => {
            ImmutableDictionary<string, TypedFieldValue> Normalize(ImmutableDictionary<string, string?> row, FieldScope scope) => row.ToImmutableDictionary(x => x.Key, x => {
                var field = s.Profile.ExtractionSchema.Fields.SingleOrDefault(f => f.Scope == scope && f.Key == x.Key);
                if (field is null || s.Proposal!.OmittedOptionalFields.Contains(new(scope, x.Key))) throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID");
                var value = new TypedValueNormalizer().Normalize(field.Type, x.Value, StudioDemoCatalog.Rule(new(scope, x.Key))).Value ?? throw new StudioFault(409, "DRAFT_PAYLOAD_INVALID");
                return value with { Source = FieldSource.Manual, Review = FieldReviewStatus.Unreviewed, Evidence = null };
            });
            var document = new CanonicalDocument("manual-" + s.Id, actor.TenantId, s.Profile.ProfileId, s.Profile.Version, Normalize(sample.Header, FieldScope.Header), sample.Rows.Select(r => Normalize(r, FieldScope.LineItem)).ToImmutableArray());
            return s with { Document = document, Validation = new DocumentValidator().Validate(document, s.Profile), Confirmation = null, MappingState = WorkflowMappingState.DraftSuggested };
        }), actor);
    }
}
