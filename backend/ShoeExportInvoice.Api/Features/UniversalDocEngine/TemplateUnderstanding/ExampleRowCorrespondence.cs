using System.Collections.Immutable;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

public sealed record ExampleSheetCorrespondence(string Worksheet, int PrototypeRow, int FirstExampleRow,
    int LastExampleRow, int ExampleRowCount, string Status, string FooterEvidence);
public sealed record ExampleCorrespondence(string TemplateSha256, string ExampleSha256, bool Supported,
    ImmutableArray<ExampleSheetCorrespondence> Sheets, ImmutableArray<string> Problems);

// Bounded structural comparison, independent from output generation. All data slots are
// derived from the ORIGINAL template. No example bytes ever become an output template.
public sealed class ExampleRowCorrespondence
{
    public ExampleCorrespondence Compare(byte[] template, byte[] example, TemplateStructure original, TemplateStructure filled, ISet<string>? selectedSheets = null)
    {
        var problems = new List<string>(); var evidence = new List<ExampleSheetCorrespondence>();
        if (!original.Sheets.Select(s => s.Name).SequenceEqual(filled.Sheets.Select(s => s.Name)))
            return new(original.TemplateSha256, filled.TemplateSha256, false, [], ["WORKSHEET_IDENTITY_CHANGED"]);
        using var expected = new XLWorkbook(new MemoryStream(template));
        using var actual = new XLWorkbook(new MemoryStream(example));
        foreach (var structure in original.Sheets)
        {
            var ws = expected.Worksheet(structure.Name); var other = actual.Worksheet(structure.Name);
            var candidates = structure.Tables.Where(t => t.PrototypeRows.Length == 1).ToArray();
            var table = (selectedSheets is null || selectedSheets.Contains(structure.Name)) && candidates.Length == 1 ? candidates[0] : null;
            var extra = (other.LastRowUsed(XLCellsUsedOptions.All)?.RowNumber() ?? 0) - (ws.LastRowUsed(XLCellsUsedOptions.All)?.RowNumber() ?? 0);
            var slots = new HashSet<string>(StringComparer.Ordinal);
            var prototype = table?.PrototypeRows[0] ?? 0;
            if (table is null)
            {
                if (extra != 0) problems.Add(structure.Name + ":REFERENCE_STRUCTURE_CHANGED");
            }
            else
            {
                if (extra is < 0 or > 4 || structure.Tables.Length != 1)
                { problems.Add(structure.Name + ":UNSUPPORTED_ROW_COUNT_OR_TABLES"); continue; }
                var matched = filled.Sheets.Single(s => s.Name == structure.Name).Tables
                    .SingleOrDefault(t => t.HeaderRow == table.HeaderRow);
                if (matched is null || !matched.PrototypeRows.SequenceEqual(Enumerable.Range(prototype, extra + 1)))
                { problems.Add(structure.Name + ":NONCONTIGUOUS_OR_UNRELATED_ROWS"); continue; }
                foreach (var column in table.Columns)
                    for (var row = prototype; row <= prototype + extra; row++)
                        if (!ws.Cell(prototype, column.Column).HasFormula && !ws.Cell(prototype, column.Column).IsMerged())
                            slots.Add(XLHelper.GetColumnLetterFromNumber(column.Column) + row);
                // Only existing horizontal header slots before the detected table are writable.
                foreach (var slot in structure.HeaderSlots)
                {
                    var label = structure.Cells.Single(c => c.Address == slot.LabelAddress);
                    var target = structure.Cells.Single(c => c.Address == slot.ValueAddress);
                    if (label.Row < table.HeaderRow && (label.Bold || label.Column == 1) &&
                        target.Row == label.Row && target.Column == label.Column + 1) slots.Add(slot.ValueAddress);
                }
                var formulas = ws.Row(prototype).CellsUsed().Where(c => c.HasFormula)
                    .ToDictionary(c => c.Address.ColumnNumber, c => c.FormulaR1C1);
                // Independently recognize supported footer totals by exact library range parsing.
                var totals = new List<(int Row, int Column, string First, string Last)>();
                foreach (var cell in ws.CellsUsed().Where(c => c.HasFormula && c.Address.RowNumber > prototype))
                {
                    if (extra == 0) continue;
                    var formula = cell.FormulaA1;
                    if (!formula.StartsWith("SUM(", StringComparison.Ordinal) || !formula.EndsWith(')'))
                    { problems.Add(structure.Name + ":UNVERIFIED_FOOTER_FORMULA"); continue; }
                    var inner = formula[4..^1].Split(':');
                    if (inner.Length != 2 || inner.Any(a => !XLHelper.IsValidA1Address(a.Replace("$", ""))))
                    { problems.Add(structure.Name + ":UNVERIFIED_FOOTER_RANGE"); continue; }
                    var first = ws.Cell(inner[0]).Address; var last = ws.Cell(inner[1]).Address;
                    if (first.RowNumber != prototype || last.RowNumber != prototype || first.ColumnNumber != last.ColumnNumber || last.FixedRow)
                    { problems.Add(structure.Name + ":UNSUPPORTED_FOOTER_TOTAL"); continue; }
                    totals.Add((cell.Address.RowNumber, cell.Address.ColumnNumber, inner[0],
                        (last.FixedColumn ? "$" : "") + XLHelper.GetColumnLetterFromNumber(last.ColumnNumber) + (prototype + extra)));
                }
                if (extra > 0)
                {
                    ws.Row(prototype + 1).InsertRowsAbove(extra);
                    for (var i = 1; i <= extra; i++) ws.Row(prototype).CopyTo(ws.Row(prototype + i));
                    foreach (var total in totals) ws.Cell(total.Row + extra, total.Column).FormulaA1 = "SUM(" + total.First + ":" + total.Last + ")";
                }
                for (var row = prototype; row <= prototype + extra; row++)
                    foreach (var formula in formulas) ws.Cell(row, formula.Key).FormulaR1C1 = formula.Value;
                evidence.Add(new(structure.Name, prototype, prototype, prototype + extra, extra + 1, "Supported",
                    extra == 0 ? "Unshifted footer compared cell by cell" : "Shifted footer, parsed SUM ranges, styles and merges compared cell by cell"));
            }
            var left = ws.CellsUsed(XLCellsUsedOptions.All).Select(c => c.Address.ToString()).ToHashSet();
            var right = other.CellsUsed(XLCellsUsedOptions.All).Select(c => c.Address.ToString()).ToHashSet();
            if (!left.SetEquals(right) || !ws.MergedRanges.Select(r => r.RangeAddress.ToString()).Order().SequenceEqual(other.MergedRanges.Select(r => r.RangeAddress.ToString()).Order()))
                problems.Add(structure.Name + ":CELL_OR_MERGE_STRUCTURE_CHANGED");
            foreach (var address in left.Union(right))
            {
                var a = ws.Cell(address!); var b = other.Cell(address!);
                if (!a.Style.Equals(b.Style) || a.FormulaA1 != b.FormulaA1 ||
                    (!slots.Contains(address!) && !a.HasFormula && !a.Value.Equals(b.Value)) ||
                    (slots.Contains(address!) && b.HasFormula))
                    problems.Add(structure.Name + ":CELL_CHANGED:" + address);
            }
            if (ws.Visibility != other.Visibility || ws.ShowGridLines != other.ShowGridLines ||
                ws.RowsUsed(XLCellsUsedOptions.All).Any(r => r.Height != other.Row(r.RowNumber()).Height || r.IsHidden != other.Row(r.RowNumber()).IsHidden) ||
                ws.ColumnsUsed(XLCellsUsedOptions.All).Any(c => c.Width != other.Column(c.ColumnNumber()).Width || c.IsHidden != other.Column(c.ColumnNumber()).IsHidden))
                problems.Add(structure.Name + ":SHEET_FORMAT_CHANGED");
        }
        if (evidence.Select(e => e.ExampleRowCount).Distinct().Count() > 1) problems.Add("INCONSISTENT_ORDERED_ROW_COUNTS");
        return new(original.TemplateSha256, filled.TemplateSha256, problems.Count == 0, evidence.Select(e => e with { Status = problems.Any(p => p.StartsWith(e.Worksheet + ":", StringComparison.Ordinal) || p == "INCONSISTENT_ORDERED_ROW_COUNTS") ? "Unsupported" : "Supported" }).ToImmutableArray(), problems.Distinct().Take(32).ToImmutableArray());
    }
}
