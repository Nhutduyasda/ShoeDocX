using System.Collections.Immutable;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

public sealed class ExcelLayoutValidator
{
    // Intentionally narrow formula language: A1 references, arithmetic, SUM and numeric literals.
    // No names, sheet references, strings, structured references, dynamic arrays or external functions.
    internal static bool SupportedFormula(string formula) => new FormulaGrammar(formula).Validate();

    private sealed class FormulaGrammar(string source)
    {
        private readonly string text = Regex.Replace(source, @"\s+", "");
        private int position;
        public bool Validate() => text.Length <= 8192 && Expression(0) && position == text.Length;
        private bool Take(char token)
        {
            if (position >= text.Length || text[position] != token) return false;
            position++; return true;
        }
        private bool Expression(int depth)
        {
            if (depth > 64 || !Term(depth + 1)) return false;
            while (Take('+') || Take('-')) if (!Term(depth + 1)) return false;
            return true;
        }
        private bool Term(int depth)
        {
            if (!Atom(depth + 1)) return false;
            while (Take('*') || Take('/')) if (!Atom(depth + 1)) return false;
            return true;
        }
        private bool Atom(int depth)
        {
            if (depth > 64) return false;
            if (Take('+') || Take('-')) return Atom(depth + 1);
            if (Take('(')) return Expression(depth + 1) && Take(')');
            if (text[position..].StartsWith("SUM(", StringComparison.Ordinal))
            {
                position += 4;
                return Reference() && Take(':') && Reference() && Take(')');
            }
            if (Reference()) return true;
            var number = Regex.Match(text[position..], @"^(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)");
            if (!number.Success) return false;
            position += number.Length; return true;
        }
        private bool Reference()
        {
            var match = Regex.Match(text[position..], @"^\$?[A-Z]{1,3}\$?[1-9][0-9]*");
            if (!match.Success || !XLHelper.IsValidA1Address(match.Value.Replace("$", ""))) return false;
            position += match.Length; return true;
        }
    }

    public ImmutableArray<ExcelProjectionError> Validate(XLWorkbook workbook, ExcelTemplateLayout layout,
        DocumentWorkflowProfile profile, int itemCount)
    {
        var errors = new List<ExcelProjectionError>();
        void Error(string code, string? at) => errors.Add(new(code, at ?? "layout", code + ": " + at));
        if (layout.Version != 1 || layout.Sheets.IsDefaultOrEmpty) Error("INVALID_LAYOUT", "layout");
        if (layout.Fingerprint() != profile.ExcelProjectionMap.ApprovedLayoutFingerprint)
            Error("LAYOUT_NOT_APPROVED", "profile");
        if (itemCount is < 1 or > 1000) Error("ITEM_LIMIT", "items");
        if (layout.Sheets.IsDefault) return errors.ToImmutableArray();
        long generatedCells = 0;
        var fields = profile.ExtractionSchema.Fields.Concat(profile.EnrichmentRules.Select(r => r.Target))
            .Concat(profile.ComputationRules.Select(r => r.Target)).ToArray();
        foreach (var duplicate in layout.Sheets.GroupBy(s => s.Worksheet).Where(g => g.Count() > 1))
            Error("DUPLICATE_SHEET", duplicate.Key);
        foreach (var sheet in layout.Sheets)
        {
            if (string.IsNullOrWhiteSpace(sheet.Worksheet) || !workbook.TryGetWorksheet(sheet.Worksheet, out var ws))
            { Error("WORKSHEET_MISSING", sheet.Worksheet); continue; }
            var region = sheet.Items;
            if (region.StartRow < 1 || region.StartRow > 1048576 || region.PrototypeRow != region.StartRow ||
                region.MaximumRows is < 1 or > 1000 || itemCount > region.MaximumRows ||
                region.Columns.IsDefaultOrEmpty || sheet.Headers.IsDefault ||
                (ws.LastRowUsed()?.RowNumber() ?? region.StartRow) + itemCount - 1 > 1048576)
            { Error("INVALID_REGION", sheet.Worksheet); continue; }
            foreach (var formula in ws.Row(region.StartRow).CellsUsed().Where(c => c.HasFormula))
                foreach (Match reference in Regex.Matches(formula.FormulaA1, @"\$?[A-Z]{1,3}\$?([1-9][0-9]*)"))
                    if (!int.TryParse(reference.Groups[1].Value, out var row) || row > region.StartRow)
                        Error("UNSUPPORTED_PROTOTYPE_REFERENCE", formula.Address.ToString());
            generatedCells += (long)(ws.LastColumnUsed()?.ColumnNumber() ?? 1) * itemCount;
            if (generatedCells > 200000)
                Error("GENERATED_CELL_LIMIT", sheet.Worksheet);
            if (ws.Row(region.StartRow).IsEmpty()) Error("PROTOTYPE_MISSING", sheet.Worksheet);
            foreach (var merge in ws.MergedRanges)
                if (merge.RangeAddress.FirstAddress.RowNumber <= region.StartRow &&
                    merge.RangeAddress.LastAddress.RowNumber >= region.StartRow)
                    Error("MERGED_REPEAT_REGION", merge.RangeAddress.ToString());
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Mapping(FieldReference source, FieldScope scope, IXLCell cell)
            {
                if (source.Scope != scope || !fields.Any(f => f.Scope == source.Scope && f.Key == source.Key))
                    Error("INVALID_SOURCE", source.ToString());
                if (!destinations.Add(cell.Address.ToString()!)) Error("OVERLAPPING_MAPPING", cell.Address.ToString());
                if (cell.HasFormula) Error("FORMULA_OVERWRITE", cell.Address.ToString());
                if (cell.IsMerged()) Error("MERGED_DESTINATION", cell.Address.ToString());
            }
            foreach (var header in sheet.Headers)
            {
                if (!Regex.IsMatch(header.Cell, @"^[A-Z]{1,3}[1-9][0-9]{0,6}$") ||
                    !XLHelper.IsValidA1Address(header.Cell))
                { Error("INVALID_CELL", header.Cell); continue; }
                var cell = ws.Cell(header.Cell);
                if (cell.Address.RowNumber >= region.StartRow) Error("HEADER_REGION_COLLISION", header.Cell);
                if (!Enum.IsDefined(header.Missing)) Error("INVALID_MISSING_POLICY", header.Cell);
                Mapping(header.Source, FieldScope.Header, cell);
            }
            foreach (var column in region.Columns)
            {
                if (column.Column is < 1 or > 16384) { Error("INVALID_COLUMN", column.Column.ToString()); continue; }
                Mapping(column.Source, FieldScope.LineItem, ws.Cell(region.StartRow, column.Column));
            }
        }
        foreach (var ws in workbook.Worksheets)
        {
            if (ws.Tables.Any() || ws.PivotTables.Any() || ws.ConditionalFormats.Any() || ws.DataValidations.Any())
                Error("UNSUPPORTED_STRUCTURE", ws.Name);
            foreach (var cell in ws.CellsUsed().Where(c => c.HasFormula))
                if (!SupportedFormula(cell.FormulaA1)) Error("UNSUPPORTED_FORMULA", ws.Name + "!" + cell.Address);
        }
        return errors.ToImmutableArray();
    }
}
