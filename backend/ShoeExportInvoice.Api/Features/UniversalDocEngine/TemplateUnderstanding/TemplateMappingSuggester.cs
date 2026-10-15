using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

public sealed class TemplateMappingSuggester
{
    public MappingSuggestionResult Suggest(TemplateStructure structure, DocumentWorkflowProfile profile,
        ApprovedLabelSynonyms synonyms)
    {
        var errors = new List<ExcelProjectionError>();
        if (string.IsNullOrWhiteSpace(synonyms.ApprovalReference) || synonyms.Fields.IsDefault || structure.Sheets.IsDefault)
            return new(structure.TemplateSha256, WorkflowProfileIdentity.Fingerprint(profile),
                ImmutableArray<MappingSuggestion>.Empty, ImmutableArray.Create(UnderstandingIdentity.Error("INVALID_SYNONYM_CONFIGURATION")));
        foreach (var alias in synonyms.Fields)
            if (alias.Labels.IsDefault || alias.Labels.Any(string.IsNullOrWhiteSpace) ||
                !profile.ExtractionSchema.Fields.Any(f => f.Scope == alias.Source.Scope && f.Key == alias.Source.Key))
                errors.Add(UnderstandingIdentity.Error("INVALID_SYNONYM_FIELD", alias.Source.ToString()));
        if (errors.Count != 0) return new(structure.TemplateSha256, WorkflowProfileIdentity.Fingerprint(profile),
            ImmutableArray<MappingSuggestion>.Empty, errors.ToImmutableArray());
        var suggestions = new List<MappingSuggestion>();
        foreach (var field in profile.ExtractionSchema.Fields)
        {
            var reference = new FieldReference(field.Scope, field.Key);
            var labels = synonyms.Fields.Where(a => a.Source == reference).SelectMany(a => a.Labels)
                .Append(field.Key).Select(UnderstandingIdentity.Label).ToHashSet(StringComparer.Ordinal);
            var candidates = new List<MappingCandidate>();
            foreach (var sheet in structure.Sheets)
            {
                if (field.Scope == FieldScope.Header)
                {
                    foreach (var slot in sheet.HeaderSlots.Where(s => labels.Contains(UnderstandingIdentity.Label(s.Label))))
                        candidates.Add(new(sheet.Name, slot.LabelAddress, slot.Label, slot.ValueAddress, null, null,
                            "Materialized adjacent value cell beside an exact normalized schema/approved synonym label.",
                            "Candidate only; adjacent cells can be ambiguous and require explicit human confirmation."));
                }
                else
                {
                    foreach (var table in sheet.Tables)
                        foreach (var column in table.Columns.Where(c => labels.Contains(UnderstandingIdentity.Label(c.Label!))))
                        {
                            var rows = table.PrototypeRows.IsEmpty ? new int?[] { null } : table.PrototypeRows.Select(r => (int?)r);
                            foreach (var row in rows)
                                candidates.Add(new(sheet.Name, column.Address, column.Label!, null, column.Column, row,
                                    "Exact normalized schema/approved synonym label in a candidate table header; prototype candidates retained.",
                                    "No inference or approval: confirm one supported prototype and resolve every competing destination."));
                        }
                }
            }
            var distinct = candidates.Distinct().OrderBy(c => c.Worksheet, StringComparer.Ordinal)
                .ThenBy(c => c.LabelCell, StringComparer.Ordinal).ThenBy(c => c.PrototypeRow).ToImmutableArray();
            var state = distinct.Length == 0 ? SuggestionStatus.Unresolved :
                distinct.Length == 1 && (field.Scope == FieldScope.Header || distinct[0].PrototypeRow.HasValue)
                    ? SuggestionStatus.UniqueCandidate : SuggestionStatus.Ambiguous;
            suggestions.Add(new(reference, state, distinct));
        }
        return new(structure.TemplateSha256, WorkflowProfileIdentity.Fingerprint(profile),
            suggestions.ToImmutableArray(), errors.ToImmutableArray());
    }

    // Generates an UNCONFIRMED draft only; a unique heuristic is never a human confirmation.
    public MappingProposal? CreateDraft(MappingSuggestionResult result, int maximumRows = 100)
    {
        if (!result.Errors.IsEmpty || result.Suggestions.IsDefaultOrEmpty || maximumRows is < 1 or > 1000 ||
            result.Suggestions.Any(s => s.Status != SuggestionStatus.UniqueCandidate || s.Candidates.Length != 1)) return null;
        var sheets = new List<ExcelSheetLayout>();
        foreach (var group in result.Suggestions.GroupBy(s => s.Candidates[0].Worksheet))
        {
            var items = group.Where(s => s.Source.Scope == FieldScope.LineItem).ToArray();
            var rows = items.Select(s => s.Candidates[0].PrototypeRow).Distinct().ToArray();
            if (items.Length == 0 || rows.Length != 1 || rows[0] is null) return null;
            sheets.Add(new(group.Key, group.Where(s => s.Source.Scope == FieldScope.Header)
                .Select(s => new ExcelCellMapping(s.Source, s.Candidates[0].Cell!)).ToImmutableArray(),
                new(rows[0]!.Value, rows[0]!.Value, maximumRows,
                    items.Select(s => new ExcelColumnMapping(s.Source, s.Candidates[0].Column!.Value)).ToImmutableArray())));
        }
        return new(1, 1, result.TemplateSha256, result.ProfileFingerprint,
            new(1, result.TemplateSha256, sheets.ToImmutableArray()), ImmutableArray<FieldReference>.Empty);
    }
}
