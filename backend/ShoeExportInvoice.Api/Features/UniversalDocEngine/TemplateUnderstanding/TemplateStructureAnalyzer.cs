using System.Collections.Immutable;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

public sealed class TemplateStructureAnalyzer
{
    public TemplateAnalysisResult Analyze(Stream template, string filename)
    {
        var input = TemplateWorkbookReader.Read(template, filename);
        if (input.Bytes is null) return new(null, input.Errors);
        try
        {
            using var workbook = new XLWorkbook(new MemoryStream(input.Bytes));
            var errors = TemplateWorkbookReader.Safety(workbook);
            if (!errors.IsEmpty) return new(null, errors);
            var sheets = new List<WorksheetStructure>();
            foreach (var sheet in workbook.Worksheets)
            {
                var cells = sheet.CellsUsed(XLCellsUsedOptions.All)
                    .OrderBy(c => c.Address.RowNumber).ThenBy(c => c.Address.ColumnNumber)
                    .Select(c => new TemplateCell(c.Address.ToString()!, c.Address.RowNumber, c.Address.ColumnNumber,
                        !c.HasFormula && c.DataType == XLDataType.Text && IsLabel(c.GetString()) ? c.GetString().Trim() : null,
                        c.HasFormula ? c.FormulaA1 : null, c.IsMerged(), c.Style.Font.Bold,
                        c.Style.NumberFormat.Format)).ToImmutableArray();
                var rows = cells.GroupBy(c => c.Row).ToDictionary(g => g.Key, g => g.ToArray());
                bool Footer(TemplateCell[] row) => row.Count(c => c.Label is not null) <= 1 && row.Any(c =>
                    c.Formula?.StartsWith("SUM(", StringComparison.Ordinal) == true ||
                    c.Label is string text && IsFooter(text));
                var footers = rows.Where(r => Footer(r.Value)).Select(r => r.Key).OrderBy(r => r).ToImmutableArray();
                var tables = new List<TableRegion>();
                foreach (var row in rows.OrderBy(r => r.Key))
                {
                    var labels = row.Value.Where(c => c.Label is not null && !c.Merged).ToImmutableArray();
                    if (labels.Length < 2 || labels.Count(c => c.Bold) < 2 || footers.Contains(row.Key)) continue;
                    var prototypes = new List<int>();
                    for (var next = row.Key + 1; next <= row.Key + 50 && rows.TryGetValue(next, out var data); next++)
                    {
                        if (footers.Contains(next)) break;
                        // A second bold label row is a new heading, not an item prototype.
                        if (data.Count(c => c.Label is not null && c.Bold) >= 2) break;
                        prototypes.Add(next);
                    }
                    tables.Add(new(row.Key, labels, prototypes.ToImmutableArray()));
                }
                
                var slots = new List<HeaderSlot>();
                foreach (var label in cells.Where(c => c.Label is not null && !footers.Contains(c.Row)))
                {
                    // Only materialized adjacent cells are evidence. Do not invent blank destinations.
                    foreach (var target in cells.Where(c => !c.Merged && c.Formula is null &&
                        ((c.Row == label.Row && c.Column == label.Column + 1) ||
                         (c.Column == label.Column && c.Row == label.Row + 1))))
                    {
                        if (target.Label is not null && target.Bold) continue;
                        slots.Add(new(label.Address, label.Label!, target.Address));
                    }
                }
                sheets.Add(new(sheet.Name, sheet.RangeUsed(XLCellsUsedOptions.All)?.RangeAddress.ToString() ?? "",
                    cells, slots.ToImmutableArray(), tables.ToImmutableArray(), footers,
                    sheet.MergedRanges.Select(r => r.RangeAddress.ToString()!).OrderBy(x => x, StringComparer.Ordinal).ToImmutableArray()));
            }
            return new(new(ExcelTemplateLayout.TemplateIdentity(input.Bytes), sheets.ToImmutableArray()), ImmutableArray<ExcelProjectionError>.Empty);
        }
        catch (Exception ex) when (TemplateWorkbookReader.KnownFailure(ex))
        { return new(null, ImmutableArray.Create(UnderstandingIdentity.Error("INVALID_WORKBOOK"))); }
    }
    private static bool IsLabel(string text) => text.Length is > 0 and <= 256 &&
        char.IsLetter(text.TrimStart().FirstOrDefault()) && text.Any(char.IsLetter);
    private static bool IsFooter(string text)
    {
        var normalized = UnderstandingIdentity.Label(text);
        return new[] { "total", "grandtotal", "tong", "tongcong", "signature", "nguoilapphieu", "warehousekeeper" }
            .Any(normalized.StartsWith);
    }
}
