using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

// Informational JSON only. No workbook bytes, source bytes, approval tokens or projector access.
public sealed record OfficeDestination(string Sheet, string Cell);
public sealed record OfficePreviewField(string Key, string Label, string Scope, string Type, bool Required,
    int? Row, string? Value, bool Missing, ImmutableArray<OfficeDestination> Destinations);
public sealed record OfficeReadiness(bool MappingConfirmed, bool DataValid, bool CanReviewNow,
    bool CanApproveNow, bool CanExport, ImmutableArray<string> Blockers);
public sealed record OfficePreview(string SessionId, long Version, OfficeReadiness Readiness, ImmutableArray<OfficePreviewField> Fields);
public sealed partial class UniversalDocWorkflowOrchestrator
{
    public OfficePreview PreviewOffice(string id, long expectedVersion)
    {
        var actor = Actor(); var s = store.Read(actor, id);
        if (s.Version != expectedVersion) throw new StudioFault(409, "SESSION_VERSION_CONFLICT");
        var blockers = ImmutableArray.CreateBuilder<string>(); var mapped = false; var valid = false;
        try { ConfirmedLayout(s); mapped = true; }
        catch (StudioFault fault) { blockers.Add(fault.Code); }
        if (s.Document is null) blockers.Add("EXTRACTION_REQUIRED");
        else {
            try { Valid(s.Document, s.Profile); valid = true; }
            catch (StudioFault fault) { blockers.Add(fault.Code); }
            if (!s.Document.HasCurrentReview(s.Profile)) blockers.Add("CURRENT_HUMAN_REVIEW_REQUIRED");
            if (!new ExportEligibilityPolicy().Evaluate(s.Document, s.Profile).CanExport) blockers.Add("CURRENT_APPROVAL_REQUIRED");
        }
        if (!actor.CanApprove) blockers.Add("EXPORT_PERMISSION_REQUIRED");
        var view = View(s, actor); var fields = ImmutableArray.CreateBuilder<OfficePreviewField>();
        foreach (var f in view.Fields) {
            var scope = Enum.Parse<FieldScope>(f.Scope);
            if (s.Proposal?.OmittedOptionalFields.Contains(new(scope, f.Key)) == true) continue;
            var definition = s.Profile.ExtractionSchema.Fields.Single(x => x.Scope == scope && x.Key == f.Key);
            var label = s.SchemaDraft?.Fields.SingleOrDefault(x => x.Scope == f.Scope && x.Key == f.Key)?.Label ?? f.Key;
            var destinations = s.Proposal?.Layout.Sheets.SelectMany(sheet => scope == FieldScope.Header
                ? sheet.Headers.Where(h => h.Source.Key == f.Key).Select(h => new OfficeDestination(sheet.Worksheet, h.Cell))
                : sheet.Items.Columns.Where(c => c.Source.Key == f.Key).Select(c => new OfficeDestination(sheet.Worksheet,
                    Column(c.Column) + (sheet.Items.StartRow + f.Row!.Value)))).ToImmutableArray() ?? ImmutableArray<OfficeDestination>.Empty;
            fields.Add(new(f.Key, label, f.Scope, f.Type, definition.Required, f.Row, f.Value, string.IsNullOrWhiteSpace(f.Value), destinations));
        }
        // Fail closed if any future change exceeds the accepted 32-field/20-row preview envelope.
        if (fields.Count > 640 || fields.Any(f => f.Destinations.Length > 3)) throw new StudioFault(400, "PREVIEW_LIMIT");
        var current = store.Read(actor, id);
        if (current.Version != expectedVersion) throw new StudioFault(409, "SESSION_VERSION_CONFLICT");
        return new(id, expectedVersion, new(mapped, valid, actor.CanReview && mapped && valid && s.Document is not null && !s.Document.HasCurrentReview(s.Profile),
            actor.CanApprove && mapped && valid && s.Document?.ReviewState == DocumentReviewState.ReadyForApproval && s.Document.HasCurrentReview(s.Profile),
            blockers.Count == 0, blockers.Distinct().ToImmutableArray()), fields.ToImmutable());
    }
    private static string Column(int column) => column <= 26 ? ((char)('A' + column - 1)).ToString() : "A" + (char)('A' + column - 27);
}
