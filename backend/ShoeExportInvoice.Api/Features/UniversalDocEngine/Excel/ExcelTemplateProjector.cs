using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

public sealed class ExcelTemplateProjector
{
    public const int MaximumInputBytes = 10 * 1024 * 1024;
    public const int MaximumExpandedBytes = 50 * 1024 * 1024;

    public ExcelProjectionResult Project(Stream template, string filename, CanonicalDocument document,
        DocumentWorkflowProfile profile, ExcelTemplateLayout layout)
    {
        ExcelProjectionResult Failure(string code, string location = "template", string? message = null) =>
            new(null, ImmutableArray.Create(new ExcelProjectionError(code, location, message ?? code)));
        if (!string.Equals(Path.GetExtension(filename), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return Failure("UNSUPPORTED_FILE_TYPE");
        var eligibility = new ExportEligibilityPolicy().Evaluate(document, profile);
        if (!eligibility.CanExport) return new(null, eligibility.DenialReasons.Select(r =>
            new ExcelProjectionError(r, "document", r)).ToImmutableArray());
        if (!template.CanRead) return Failure("STREAM_NOT_READABLE");
        foreach (var value in document.Header.Values.Concat(document.LineItems.SelectMany(row => row.Values)))
        {
            if (value.Text?.Length > 32767) return Failure("TEXT_CELL_LIMIT", "document");
            // Excel stores numbers as IEEE 754 doubles with at most 15 significant decimal digits.
            if (value.Number is decimal number && number != 0 &&
                ( !decimal.TryParse(((double)number).ToString("G15", System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var roundTrip) || roundTrip != number))
                return Failure("EXCEL_NUMERIC_PRECISION", "document");
            if (value.Date is DateOnly date && date < new DateOnly(1900, 1, 1))
                return Failure("EXCEL_DATE_RANGE", "document");
        }
        var position = template.CanSeek ? template.Position : (long?)null;
        try
        {
            // Read from the beginning, restoring caller's cursor even on failure.
            if (template.CanSeek) template.Position = 0;
            using var input = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = template.Read(buffer, 0, Math.Min(buffer.Length, MaximumInputBytes + 1 - (int)input.Length))) > 0)
            {
                input.Write(buffer, 0, read);
                if (input.Length > MaximumInputBytes) return Failure("INPUT_LIMIT");
            }
            var bytes = input.ToArray();
            if (ExcelTemplateLayout.TemplateIdentity(bytes) != layout.TemplateSha256)
                return Failure("TEMPLATE_IDENTITY_MISMATCH");
            input.Position = 0;
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read, true))
            {
                if (zip.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count)
                    return Failure("DUPLICATE_PACKAGE_PART");
                if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > MaximumExpandedBytes)
                    return Failure("EXPANDED_INPUT_LIMIT");
                foreach (var entry in zip.Entries)
                {
                    var path = entry.FullName;
                    if (path.Contains("vba", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("xl/externalLinks/") || path.StartsWith("xl/connections") ||
                        path.StartsWith("xl/embeddings/") || path.StartsWith("xl/drawings/") ||
                        path.StartsWith("xl/charts/") || path.StartsWith("xl/activeX/") || path.StartsWith("xl/queryTables/") || path.StartsWith("xl/slicer") ||
                        path.StartsWith("xl/ctrlProps/") || path.StartsWith("xl/threadedComments/"))
                        return Failure("UNSUPPORTED_PACKAGE_PART", path);
                    if (path.StartsWith("xl/worksheets/") && path.EndsWith(".xml"))
                    {
                        using var data = entry.Open();
                        using var reader = System.Xml.XmlReader.Create(data, new System.Xml.XmlReaderSettings {
                            DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null,
                            MaxCharactersInDocument = MaximumExpandedBytes });
                        var xml = XDocument.Load(reader);
                        if (xml.Descendants().Any(e => e.Name.LocalName is "extLst" or "oleObjects" or "controls" ||
                            (e.Name.LocalName == "f" && e.Attribute("t")?.Value is "array" or "dataTable")))
                            return Failure("UNSUPPORTED_WORKSHEET_EXTENSION", path);
                    }
                    if (path.EndsWith(".rels") || path == "[Content_Types].xml")
                    {
                        using var data = entry.Open();
                        using var reader = System.Xml.XmlReader.Create(data, new System.Xml.XmlReaderSettings {
                            DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null,
                            MaxCharactersInDocument = MaximumExpandedBytes });
                        var xml = XDocument.Load(reader);
                        if (xml.Descendants().Attributes().Any(a =>
                            (a.Name.LocalName == "TargetMode" && a.Value == "External") ||
                            a.Value.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase)))
                            return Failure("UNSUPPORTED_EXTERNAL_OR_MACRO", path);
                    }
                }
            }
            input.Position = 0;
            using var workbook = new XLWorkbook(input);
            var errors = new ExcelLayoutValidator().Validate(workbook, layout, profile, document.LineItems.Length);
            if (!errors.IsEmpty) return new(null, errors);
            foreach (var sheet in layout.Sheets)
            {
                var ws = workbook.Worksheet(sheet.Worksheet);
                var region = sheet.Items;
                foreach (var header in sheet.Headers)
                {
                    document.Header.TryGetValue(header.Source.Key, out var value);
                    Write(ws.Cell(header.Cell), value, header.Missing);
                }
                var start = region.StartRow;
                var extra = document.LineItems.Length - 1;
                // Capture prototype formulas before structural changes; R1C1 gives correct relative/mixed copies.
                var formulas = ws.Row(start).CellsUsed().Where(c => c.HasFormula)
                    .ToDictionary(c => c.Address.ColumnNumber, c => c.FormulaR1C1);
                if (extra > 0)
                {
                    ws.Row(start + 1).InsertRowsAbove(extra);
                    for (var i = 1; i <= extra; i++) ws.Row(start).CopyTo(ws.Row(start + i));
                    // Extend supported footer totals whose range ended at the single prototype row.
                    foreach (var cell in ws.CellsUsed().Where(c => c.HasFormula && c.Address.RowNumber > start + extra))
                        cell.FormulaA1 = Regex.Replace(cell.FormulaA1,
                            @"(\$?[A-Z]{1,3}\$?[0-9]+:\$?[A-Z]{1,3}\$?)" + start + @"(?![0-9])",
                            m => m.Groups[1].Value + (start + extra));
                }
                for (var i = 0; i < document.LineItems.Length; i++)
                {
                    var row = ws.Row(start + i);
                    foreach (var cell in row.CellsUsed().ToArray())
                        if (!formulas.ContainsKey(cell.Address.ColumnNumber)) cell.Clear(XLClearOptions.Contents);
                    foreach (var formula in formulas) ws.Cell(start + i, formula.Key).FormulaR1C1 = formula.Value;
                    foreach (var column in region.Columns)
                    {
                        document.LineItems[i].TryGetValue(column.Source.Key, out var value);
                        Write(ws.Cell(start + i, column.Column), value, MissingValuePolicy.Blank);
                    }
                }
            }
            workbook.CalculateMode = XLCalculateMode.Auto;
            using var output = new MemoryStream();
            workbook.SaveAs(output, new ClosedXML.Excel.SaveOptions { EvaluateFormulasBeforeSaving = false });
            // Reopening is an integrity check, not a claim that Excel evaluated every formula.
            output.Position = 0;
            using var verified = new XLWorkbook(output);
            return new(output.ToArray(), ImmutableArray<ExcelProjectionError>.Empty);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or
            InvalidOperationException or System.Xml.XmlException or FormatException or OverflowException or
            DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            return Failure("WORKBOOK_PROCESSING_FAILED", message: ex.GetType().Name + ": " + ex.Message);
        }
        finally { if (position.HasValue) template.Position = position.Value; }
    }

    private static void Write(IXLCell cell, TypedFieldValue? value, MissingValuePolicy missing)
    {
        if (value is null || value.IsMissing)
        {
            if (missing == MissingValuePolicy.Blank) cell.Clear(XLClearOptions.Contents);
            return;
        }
        switch (value.Type)
        {
            case FieldType.Text: cell.Value = value.Text!; break;
            case FieldType.Decimal: cell.Value = (double)value.Number!.Value; break;
            case FieldType.Date: cell.Value = value.Date!.Value.ToDateTime(TimeOnly.MinValue); break;
            case FieldType.Boolean: cell.Value = value.Boolean!.Value; break;
            default: throw new InvalidOperationException("Unsupported field type.");
        }
    }
}
