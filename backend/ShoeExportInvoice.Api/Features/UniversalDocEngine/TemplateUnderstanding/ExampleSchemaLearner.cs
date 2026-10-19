using System.Collections.Immutable;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

public sealed record LearnedField(string Key, string Label, string Scope, string Type, bool Required,
    bool Omit, int? Candidate, string Status, string Rationale, ImmutableArray<MappingCandidate> Candidates);
public sealed record ExampleSchemaDraft(long Revision, string State, string TemplateSha256, string? ExampleSha256,
    ImmutableArray<LearnedField> Fields)
{
    public string ProfileFingerprint { get; init; } = "";
    public int ProfileVersion { get; init; }
}

// Deterministic evidence collection, never a confirmation or a business-semantics classifier.
public sealed class ExampleSchemaLearner
{
    public ImmutableArray<LearnedField> Suggest(TemplateStructure structure, byte[] template, byte[]? example)
    {
        using var workbook = new XLWorkbook(new MemoryStream(example ?? template));
        var candidates = new List<(FieldScope Scope, MappingCandidate Candidate)>();
        foreach (var sheet in structure.Sheets)
        {
            foreach (var table in sheet.Tables.Where(t => t.PrototypeRows.Length == 1))
                foreach (var column in table.Columns)
                    if (sheet.Cells.Any(c => c.Row == table.PrototypeRows[0] && c.Column == column.Column && c.Formula is null && !c.Merged))
                        candidates.Add((FieldScope.LineItem, new(sheet.Name, column.Address, column.Label!, null,
                            column.Column, table.PrototypeRows[0], column.Address,
                            "Bold table heading and one materialized prototype; select one destination.")));
            var firstTable = sheet.Tables.Select(t => t.HeaderRow).DefaultIfEmpty(0).Min();
            // Restrict header evidence to labels before the first table, with a materialized adjacent slot.
            foreach (var slot in sheet.HeaderSlots.Where(h => {
                var label = sheet.Cells.Single(c => c.Address == h.LabelAddress);
                var target = sheet.Cells.Single(c => c.Address == h.ValueAddress);
                return label.Row < firstTable && (label.Bold || label.Column == 1) && target.Row == label.Row && target.Column == label.Column + 1;
            }))
                candidates.Add((FieldScope.Header, new(sheet.Name, slot.LabelAddress, slot.Label, slot.ValueAddress,
                    null, null, slot.LabelAddress, "Adjacent non-formula value slot before the table; human correction required.")));
        }
        var fields = new List<LearnedField>();
        foreach (var group in candidates.GroupBy(x => (x.Scope, Label: UnderstandingIdentity.Label(x.Candidate.MatchedLabel))))
        {
            var choices = group.Select(x => x.Candidate).Distinct().OrderBy(x => x.Worksheet, StringComparer.Ordinal)
                .ThenBy(x => x.LabelCell, StringComparer.Ordinal).ThenBy(x => x.Cell, StringComparer.Ordinal).ToImmutableArray();
            if (choices.Length > 32 || fields.Count >= 32) throw new ArgumentException("SCHEMA_CANDIDATE_LIMIT");
            var types = choices.Select(c => Type(workbook.Worksheet(c.Worksheet).Cell(c.Cell ?? XLHelper.GetColumnLetterFromNumber(c.Column!.Value) + c.PrototypeRow))).Distinct().ToArray();
            var ambiguous = choices.Length != 1 || types.Length != 1;
            fields.Add(new("field" + (fields.Count + 1), choices[0].MatchedLabel, group.Key.Scope.ToString(),
                types.Length == 1 ? types[0] : "Text", false, false, ambiguous ? null : 0,
                ambiguous ? "Ambiguous" : "Suggested", "Actual cell value type only; formats do not establish semantics, requiredness or arithmetic. Text and unknown numeric identifiers need human review.", choices));
        }
        return fields.ToImmutableArray();
    }
    private static string Type(IXLCell cell)
    {
        if (cell.HasFormula) return "Text";
        // Integral numeric storage cannot distinguish an identifier from an amount. Remain Text.
        // A fractional numeric value suggests Decimal storage only, never quantity/money semantics.
        return cell.DataType switch { XLDataType.Boolean => "Boolean", XLDataType.DateTime => "Date",
            XLDataType.Number when cell.GetDouble() != Math.Truncate(cell.GetDouble()) => "Decimal", _ => "Text" };
    }

    public bool Corresponds(TemplateStructure template, TemplateStructure example)
    {
        if (!template.Sheets.Select(s => s.Name).SequenceEqual(example.Sheets.Select(s => s.Name))) return false;
        foreach (var sheet in template.Sheets)
        {
            var other = example.Sheets.Single(s => s.Name == sheet.Name);
            if (sheet.UsedRange != other.UsedRange || !sheet.MergedRanges.SequenceEqual(other.MergedRanges) ||
                !sheet.Cells.Select(c => (c.Address, c.Formula, c.Merged, c.Bold, c.NumberFormat))
                    .SequenceEqual(other.Cells.Select(c => (c.Address, c.Formula, c.Merged, c.Bold, c.NumberFormat)))) return false;
            // Labels that define the template's headings/slots may not move or change.
            var firstTable = sheet.Tables.Select(t => t.HeaderRow).DefaultIfEmpty(0).Min();
            var evidence = sheet.Tables.SelectMany(t => t.Columns).Select(c => c.Address)
                .Concat(sheet.HeaderSlots.Where(h => {
                    var label = sheet.Cells.Single(c => c.Address == h.LabelAddress);
                    var target = sheet.Cells.Single(c => c.Address == h.ValueAddress);
                    return label.Row < firstTable && (label.Bold || label.Column == 1) && target.Row == label.Row && target.Column == label.Column + 1;
                }).Select(h => h.LabelAddress)).Distinct();
            if (evidence.Any(address => sheet.Cells.Single(c => c.Address == address).Label != other.Cells.Single(c => c.Address == address).Label)) return false;
        }
        return true;
    }
}
