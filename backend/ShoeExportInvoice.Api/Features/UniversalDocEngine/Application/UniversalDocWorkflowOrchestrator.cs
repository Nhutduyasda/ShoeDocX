using System.Collections.Immutable;
using System.Globalization;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public sealed partial class UniversalDocWorkflowOrchestrator(IUniversalDocCurrentActor currentActor, IUniversalDocSessionStore store)
{
    public UniversalDocActor Actor() => currentActor.Resolve();
    public StudioSessionView Create(string workflow)
    { var actor = Actor(); return View(store.Create(actor, workflow), actor); }
    public StudioSessionView Read(string id)
    { var actor = Actor(); return View(store.Read(actor, id), actor); }
    public void Remove(string id, long version) => store.Remove(Actor(), id, version);
    public byte[] SampleTemplate(string workflow) { Actor(); return StudioDemoCatalog.Template(workflow); }

    public StudioSessionView Upload(string id, long version, byte[] bytes, string filename)
    {
        if (store.Read(Actor(), id).Workflow == "example-template") return UploadLearning(id, version, bytes, filename, false);
        var actor = Actor();
        if (bytes.Length is <= 0 or > InMemoryUniversalDocSessionStore.MaximumTemplateBytes ||
            !filename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new StudioFault(400, "SUPPORTED_XLSX_REQUIRED");
        return View(store.Update(actor, id, version, s =>
        {
            using var stream = new MemoryStream(bytes);
            var analysis = new TemplateStructureAnalyzer().Analyze(stream, "template.xlsx");
            if (!analysis.Success) throw new StudioFault(400, "UNSUPPORTED_OR_INVALID_TEMPLATE");
            var structure = analysis.Structure!;
            if (structure.Sheets.Length > 4 || structure.Sheets.Sum(sheet => sheet.Cells.Length) > 2000 ||
                structure.Sheets.Any(sheet => sheet.Cells.Any(c => c.Row > 1000 || c.Column > 32 || c.Label?.Length > 2000)))
                throw new StudioFault(400, "STUDIO_TEMPLATE_LIMIT");
            var profile = s.MappingProfile with { Version = s.Profile.Version + (s.Template.IsEmpty ? 0 : 1),
                ExcelProjectionMap = s.MappingProfile.ExcelProjectionMap with { ApprovedLayoutFingerprint = null } };
            var suggestions = new TemplateMappingSuggester().Suggest(structure, profile, StudioDemoCatalog.Synonyms(profile));
            if (!suggestions.Errors.IsEmpty || suggestions.Suggestions.Any(x => x.Candidates.Length > 32)) throw new StudioFault(400, "MAPPING_CANDIDATE_LIMIT");
            var proposal = new TemplateMappingSuggester().CreateDraft(suggestions, 20);
            if (proposal is not null) proposal = proposal with { Revision = (s.Proposal?.Revision ?? 0) + 1 };
            var document = s.Document?.WithProfileVersion(profile.Version);
            return s with { MappingProfile = profile, Profile = profile, Template = bytes.ToImmutableArray(), TemplateSha256 = structure.TemplateSha256,
                Structure = structure, Suggestions = suggestions, Proposal = proposal, Confirmation = null, MappingState = WorkflowMappingState.DraftSuggested,
                Document = document, Validation = document is null ? null : new DocumentValidator().Validate(document, profile) };
        }), actor);
    }
    public StudioSessionView ChangeMapping(string id, long version, StudioMapping mapping)
    {
        var actor = Actor();
        if (store.Read(actor, id).Workflow == "example-template") throw new StudioFault(400, "USE_SCHEMA_EDITOR");
        if (mapping.Headers is null || mapping.Columns is null || mapping.Headers.Count > 6 || mapping.Columns.Count > 6 ||
            mapping.Sheet?.Length is not (>= 1 and <= 31) || mapping.StartRow is < 1 or > 1000 ||
            mapping.Headers.Any(m => m.Key.Length > 128 || m.Value is null || m.Value.Length > 12) || mapping.Columns.Any(m => m.Key.Length > 128))
            throw new StudioFault(400, "INVALID_MAPPING_REQUEST");
        return View(store.Update(actor, id, version, s =>
        {
            RequireTemplate(s);
            var profile = s.MappingProfile with { Version = checked(s.Profile.Version + 1),
                ExcelProjectionMap = s.MappingProfile.ExcelProjectionMap with { ApprovedLayoutFingerprint = null } };
            var layout = new ExcelTemplateLayout(1, s.TemplateSha256!, ImmutableArray.Create(new ExcelSheetLayout(mapping.Sheet, mapping.Headers
                .OrderBy(x => x.Key, StringComparer.Ordinal).Select(m => new ExcelCellMapping(new(FieldScope.Header, m.Key), m.Value)).ToImmutableArray(),
                new(mapping.StartRow, mapping.StartRow, 20, mapping.Columns.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(m => new ExcelColumnMapping(new(FieldScope.LineItem, m.Key), m.Value)).ToImmutableArray()))));
            var proposal = new MappingProposal(1, (s.Proposal?.Revision ?? 0) + 1, s.TemplateSha256!, WorkflowProfileIdentity.Fingerprint(profile), layout,
                ImmutableArray<FieldReference>.Empty);
            using var stream = Template(s);
            if (!new MappingProposalValidator().Validate(stream, "template.xlsx", proposal, profile).IsValid) throw new StudioFault(400, "INVALID_MAPPING");
            var document = s.Document?.WithProfileVersion(profile.Version);
            return s with { Profile = profile, MappingProfile = profile, Proposal = proposal, Confirmation = null,
                MappingState = WorkflowMappingState.DraftSuggested, Document = document,
                Validation = document is null ? null : new DocumentValidator().Validate(document, profile) };
        }), actor);
    }
    public StudioSessionView ConfirmMapping(string id, long version)
    {
        var actor = Actor();
        return View(store.Update(actor, id, version, s =>
        {
            if (s.MappingState != WorkflowMappingState.DraftSuggested || s.Proposal is null) throw new StudioFault(409, "MAPPING_CORRECTION_REQUIRED");
            RequireTemplate(s);
            if (s.SchemaDraft is not null && s.SchemaDraft.State != "ReadyForConfirmation") throw new StudioFault(409, "SCHEMA_CORRECTION_REQUIRED");
            using var stream = Template(s); var service = new HumanMappingConfirmationService();
            var confirmed = service.Confirm(stream, "template.xlsx", s.Proposal, s.MappingProfile, actor.UserId);
            if (confirmed.Confirmation is null || !confirmed.Errors.IsEmpty) throw new StudioFault(400, "MAPPING_CONFIRMATION_INVALID");
            using var convertedStream = Template(s); var converted = service.Convert(convertedStream, "template.xlsx", s.Proposal, s.MappingProfile, confirmed.Confirmation);
            if (converted.Layout is null || !converted.Errors.IsEmpty) throw new StudioFault(409, "MAPPING_CONFIRMATION_STALE");
            var profile = s.MappingProfile with { ExcelProjectionMap = s.MappingProfile.ExcelProjectionMap with { ApprovedLayoutFingerprint = converted.Layout.Fingerprint() } };
            var document = s.Document?.WithProfileVersion(profile.Version);
            return s with { Confirmation = confirmed.Confirmation, Profile = profile, MappingState = WorkflowMappingState.UserConfirmed,
                Document = document, Validation = document is null ? null : new DocumentValidator().Validate(document, profile),
                SchemaDraft = s.SchemaDraft is null ? null : s.SchemaDraft with { State = "UserConfirmed" } };
        }), actor);
    }
    public StudioSessionView ExtractDemo(string id, long version)
    {
        var actor = Actor();
        return View(store.Update(actor, id, version, s =>
        {
            ConfirmedLayout(s);
            if (s.SchemaDraft is not null) throw new StudioFault(400, "USE_MANUAL_SAMPLE");
            var context = StudioDemoCatalog.ExtractionContext(s.Profile);
            var result = new ExtractionPipeline().Extract(context, StudioDemoCatalog.Observations(context));
            if (!result.Issues.IsEmpty || result.Documents.Length != 1) throw new StudioFault(400, "DEMO_EXTRACTION_FAILED");
            return s with { Extraction = result, Document = result.Documents[0].Document, Validation = result.Documents[0].Validation };
        }), actor);
    }
    public StudioSessionView EditField(string id, long version, string scope, string key, int? row, string? raw)
    {
        var actor = Actor();
        if (raw?.Length > 2000 || key.Length > 128) throw new StudioFault(400, "FIELD_LIMIT");
        return View(store.Update(actor, id, version, s =>
        {
            ConfirmedLayout(s); var document = RequireDocument(s);
            if (!Enum.TryParse<FieldScope>(scope, false, out var parsedScope) || !Enum.IsDefined(parsedScope)) throw new StudioFault(400, "INVALID_FIELD_SCOPE");
            var definition = s.Profile.ExtractionSchema.Fields.SingleOrDefault(f => f.Scope == parsedScope && f.Key == key)
                ?? throw new StudioFault(400, "UNKNOWN_FIELD");
            if ((parsedScope == FieldScope.Header && row is not null) || (parsedScope == FieldScope.LineItem && (row is null or < 0 || row >= document.LineItems.Length)))
                throw new StudioFault(400, "INVALID_FIELD_ROW");
            var normalized = new TypedValueNormalizer().Normalize(definition.Type, raw, StudioDemoCatalog.Rule(new(parsedScope, key)));
            if (normalized.Value is null) throw new StudioFault(400, "INVALID_EDITED_VALUE");
            var value = normalized.Value with { Source = FieldSource.Manual, Review = FieldReviewStatus.Unreviewed, Evidence = null };
            var edited = document.Edit(parsedScope == FieldScope.Header ? document.Header.SetItem(key, value) : document.Header,
                parsedScope == FieldScope.LineItem ? document.LineItems.SetItem(row!.Value, document.LineItems[row.Value].SetItem(key, value)) : document.LineItems);
            return s with { Document = edited, Validation = new DocumentValidator().Validate(edited, s.Profile) };
        }), actor);
    }
    public StudioSessionView Review(string id, long version)
    {
        var actor = Actor(); if (!actor.CanReview) throw new StudioFault(403, "REVIEW_PERMISSION_REQUIRED");
        return View(store.Update(actor, id, version, s =>
        {
            ConfirmedLayout(s); var document = RequireDocument(s); Valid(document, s.Profile);
            if (document.ReviewState != DocumentReviewState.NeedsReview) document = document.Review(DocumentReviewState.NeedsReview);
            return s with { Document = document.CompleteReview(actor.UserId, s.Profile), Validation = new DocumentValidator().Validate(document, s.Profile) };
        }), actor);
    }
    public StudioSessionView Approve(string id, long version)
    {
        var actor = Actor(); if (!actor.CanApprove) throw new StudioFault(403, "APPROVAL_PERMISSION_REQUIRED");
        return View(store.Update(actor, id, version, s =>
        {
            ConfirmedLayout(s); var document = RequireDocument(s); Valid(document, s.Profile);
            if (document.ReviewState != DocumentReviewState.ReadyForApproval || !document.HasCurrentReview(s.Profile)) throw new StudioFault(409, "CURRENT_HUMAN_REVIEW_REQUIRED");
            return s with { Document = document.Approve(actor.UserId, s.Profile) };
        }), actor);
    }
    public StudioExport Export(string id, long version)
    {
        var actor = Actor(); if (!actor.CanApprove) throw new StudioFault(403, "EXPORT_PERMISSION_REQUIRED");
        var snapshot = store.Read(actor, id);
        if (snapshot.Version != version) throw new StudioFault(409, "SESSION_VERSION_CONFLICT");
        var layout = ConfirmedLayout(snapshot); var document = RequireDocument(snapshot);
        Valid(document, snapshot.Profile);
        if (!new ExportEligibilityPolicy().Evaluate(document, snapshot.Profile).CanExport) throw new StudioFault(409, "CURRENT_APPROVAL_REQUIRED");
        using var stream = Template(snapshot);
        // The sole Gate 4A runtime call to the existing Gate 2 exporter.
        var result = new ExcelTemplateProjector().Project(stream, "template.xlsx", document, snapshot.Profile, layout);
        if (!result.Success) throw new StudioFault(409, "EXPORT_VALIDATION_FAILED");
        // Linearization point: rechecks owner, tenant, identity/session TTL and revision after preparation.
        var committed = store.Update(actor, id, version, current => current);
        return new(result.Workbook!, committed.Version);
    }
    private static void RequireTemplate(UniversalDocSession s)
    {
        if (s.Template.IsDefaultOrEmpty) throw new StudioFault(409, "TEMPLATE_REQUIRED");
        if (ExcelTemplateLayout.TemplateIdentity(s.Template.ToArray()) != s.TemplateSha256) throw new StudioFault(409, "TEMPLATE_IDENTITY_CHANGED");
        if (s.SchemaDraft is { } draft && (draft.TemplateSha256 != s.TemplateSha256 ||
            (s.Example.IsEmpty ? draft.ExampleSha256 is not null : ExcelTemplateLayout.TemplateIdentity(s.Example.ToArray()) != draft.ExampleSha256)))
            throw new StudioFault(409, "EXAMPLE_IDENTITY_CHANGED");
    }
    private static MemoryStream Template(UniversalDocSession s) => new(s.Template.ToArray(), false);
    private static CanonicalDocument RequireDocument(UniversalDocSession s) => s.Document ?? throw new StudioFault(409, "EXTRACTION_REQUIRED");
    private static ExcelTemplateLayout ConfirmedLayout(UniversalDocSession s)
    {
        RequireTemplate(s);
        if (s.MappingState != WorkflowMappingState.UserConfirmed || s.Confirmation is null || s.Proposal is null) throw new StudioFault(409, "MAPPING_CONFIRMATION_REQUIRED");
        using var stream = Template(s);
        var converted = new HumanMappingConfirmationService().Convert(stream, "template.xlsx", s.Proposal, s.MappingProfile, s.Confirmation);
        if (converted.Layout is null || !converted.Errors.IsEmpty) throw new StudioFault(409, "MAPPING_CONFIRMATION_STALE");
        var expected = s.MappingProfile with { ExcelProjectionMap = s.MappingProfile.ExcelProjectionMap with { ApprovedLayoutFingerprint = converted.Layout.Fingerprint() } };
        if (WorkflowProfileIdentity.Fingerprint(expected) != WorkflowProfileIdentity.Fingerprint(s.Profile)) throw new StudioFault(409, "PROFILE_IDENTITY_CHANGED");
        return converted.Layout;
    }
    private static void Valid(CanonicalDocument document, DocumentWorkflowProfile profile)
    {
        var validation = new DocumentValidator().Validate(document, profile);
        if (validation.Issues.Any(i => i.Severity != IssueSeverity.Info) || validation.Checks.Any(c => c.Required && c.Outcome != CheckOutcome.Passed))
            throw new StudioFault(409, "DOCUMENT_VALIDATION_REQUIRED");
    }
    private static StudioSessionView View(UniversalDocSession s, UniversalDocActor actor)
    {
        var fields = ImmutableArray.CreateBuilder<StudioFieldView>();
        if (s.Document is { } d)
        {
            foreach (var definition in s.Profile.ExtractionSchema.Fields)
            {
                var count = definition.Scope == FieldScope.Header ? 1 : d.LineItems.Length;
                for (int i = 0; i < count; i++)
                {
                    var data = definition.Scope == FieldScope.Header ? d.Header : d.LineItems[i]; data.TryGetValue(definition.Key, out var v);
                    var raw = s.Extraction?.Documents.SelectMany(x => x.Observations).Where(o => o.Field.Scope == definition.Scope && o.Field.Key == definition.Key &&
                        (definition.Scope == FieldScope.Header || o.RowPosition == i)).Select(o => o.RawText ?? "").Take(4).ToImmutableArray() ?? ImmutableArray<string>.Empty;
                    fields.Add(new(definition.Scope.ToString(), definition.Key, definition.Scope == FieldScope.Header ? null : i, definition.Type.ToString(),
                        v?.Text ?? v?.Number?.ToString(CultureInfo.InvariantCulture) ?? v?.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? v?.Boolean?.ToString(),
                        v?.Review.ToString() ?? "Unreviewed", v?.Source.ToString() ?? "Missing", v?.Evidence?.SourceFileId, v?.Evidence?.PageNumber, v?.Evidence?.Region, raw));
                }
            }
        }
        var suggestions = s.Suggestions?.Suggestions.Select(x => new StudioSuggestion(x.Source.Scope.ToString(), x.Source.Key, x.Status.ToString(),
            x.Candidates.Select(c => new StudioCandidate(c.Worksheet, c.LabelCell, c.Cell, c.Column, c.PrototypeRow)).ToImmutableArray())).ToImmutableArray()
            ?? ImmutableArray<StudioSuggestion>.Empty;
        var sheet = s.Proposal?.Layout.Sheets.FirstOrDefault();
        var mapping = sheet is null ? null : new StudioMapping(sheet.Worksheet, sheet.Items.StartRow,
            sheet.Headers.ToImmutableDictionary(x => x.Source.Key, x => x.Cell), sheet.Items.Columns.ToImmutableDictionary(x => x.Source.Key, x => x.Column));
        var issues = (s.Validation?.Issues.Select(i => new StudioIssueView(i.Code, i.CheckId, null, null)) ?? Enumerable.Empty<StudioIssueView>())
            .Concat(s.Extraction?.Documents.SelectMany(d => d.Issues).Select(i => new StudioIssueView(i.Code, i.Field?.Key, i.RowId, i.RowPosition)) ?? Enumerable.Empty<StudioIssueView>()).Take(100).ToImmutableArray();
        return new(s.Id, s.Version, s.ExpiresAt, s.Workflow, s.MappingState.ToString(), s.Proposal?.Revision, s.Profile.Version, s.Document?.DataRevision,
            s.Document?.ReviewState.ToString() ?? "NoDocument", s.Structure?.Sheets.Select(x => x.Name).ToImmutableArray() ?? ImmutableArray<string>.Empty,
            suggestions, mapping, fields.ToImmutable(), issues, actor.CanReview, actor.CanApprove)
            { Sources = s.Sources.Select(SourceIntake.View).ToImmutableArray(), SchemaDraft = s.SchemaDraft,
                SheetSummaries = s.Structure?.Sheets.Select(sheet => new StudioSheetSummary(sheet.Name, sheet.Cells.Length, sheet.Tables.Length,
                    sheet.Tables.SelectMany(t => t.PrototypeRows).Distinct().ToImmutableArray(), sheet.FooterRows, sheet.MergedRanges)).ToImmutableArray() ?? ImmutableArray<StudioSheetSummary>.Empty };
    }
}
