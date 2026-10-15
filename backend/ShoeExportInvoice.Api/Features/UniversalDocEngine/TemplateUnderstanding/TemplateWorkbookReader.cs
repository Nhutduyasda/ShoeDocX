using System.Collections.Immutable;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

internal static class TemplateWorkbookReader
{
    internal sealed record Result(byte[]? Bytes, ImmutableArray<ExcelProjectionError> Errors);
    internal static Result Read(Stream stream, string filename)
    {
        Result Fail(string code) => new(null, ImmutableArray.Create(UnderstandingIdentity.Error(code)));
        if (!string.Equals(Path.GetExtension(filename), ".xlsx", StringComparison.OrdinalIgnoreCase)) return Fail("UNSUPPORTED_FILE_TYPE");
        if (!stream.CanRead) return Fail("STREAM_NOT_READABLE");
        var position = stream.CanSeek ? stream.Position : (long?)null;
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            using var copy = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, Math.Min(buffer.Length, ExcelTemplateProjector.MaximumInputBytes + 1 - (int)copy.Length))) > 0)
            {
                copy.Write(buffer, 0, read);
                if (copy.Length > ExcelTemplateProjector.MaximumInputBytes) return Fail("INPUT_LIMIT");
            }
            copy.Position = 0;
            using var zip = new ZipArchive(copy, ZipArchiveMode.Read, true);
            if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > ExcelTemplateProjector.MaximumExpandedBytes)
                return Fail("EXPANDED_INPUT_LIMIT");
            if (zip.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count)
                return Fail("DUPLICATE_PACKAGE_PART");
            foreach (var entry in zip.Entries)
            {
                var path = entry.FullName.ToLowerInvariant();
                if (path.Contains("vba") || new[] { "xl/externallinks/", "xl/connections", "xl/embeddings/", "xl/drawings/", "xl/charts/", "xl/activex/", "xl/querytables/", "xl/slicer", "xl/ctrlprops/", "xl/threadedcomments/" }.Any(path.StartsWith))
                    return Fail("UNSUPPORTED_PACKAGE_PART");
                if (!path.EndsWith(".xml") && !path.EndsWith(".rels")) continue;
                using var data = entry.Open();
                using var reader = XmlReader.Create(data, new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                    MaxCharactersInDocument = ExcelTemplateProjector.MaximumExpandedBytes });
                var xml = XDocument.Load(reader);
                if (xml.Descendants().Attributes().Any(a =>
                    (a.Name.LocalName == "TargetMode" && a.Value.Equals("External", StringComparison.OrdinalIgnoreCase)) ||
                    a.Value.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase))) return Fail("UNSUPPORTED_EXTERNAL_OR_MACRO");
                if (path.StartsWith("xl/worksheets/") && xml.Descendants().Any(e =>
                    e.Name.LocalName is "extLst" or "oleObjects" or "controls" ||
                    (e.Name.LocalName == "f" && e.Attribute("t")?.Value is "array" or "dataTable"))) return Fail("UNSUPPORTED_WORKSHEET_EXTENSION");
            }
            return new(copy.ToArray(), ImmutableArray<ExcelProjectionError>.Empty);
        }
        catch (Exception ex) when (KnownFailure(ex)) { return Fail("INVALID_WORKBOOK"); }
        finally { if (position.HasValue) stream.Position = position.Value; }
    }
    internal static bool KnownFailure(Exception ex) => ex is InvalidDataException or IOException or
        ArgumentException or InvalidOperationException or XmlException or FormatException or OverflowException or
        DocumentFormat.OpenXml.Packaging.OpenXmlPackageException or KeyNotFoundException;

    internal static ImmutableArray<ExcelProjectionError> Safety(XLWorkbook workbook)
    {
        var errors = new List<ExcelProjectionError>();
        if (workbook.Worksheets.Count > 20) return ImmutableArray.Create(UnderstandingIdentity.Error("SHEET_LIMIT"));
        var totalCells = 0;
        foreach (var sheet in workbook.Worksheets)
        {
            var range = sheet.RangeUsed(XLCellsUsedOptions.All);
            if (range is not null && (range.RangeAddress.LastAddress.RowNumber > 10000 || range.RangeAddress.LastAddress.ColumnNumber > 256))
            { errors.Add(UnderstandingIdentity.Error("ANALYSIS_RANGE_LIMIT", sheet.Name)); continue; }
            totalCells += sheet.CellsUsed(XLCellsUsedOptions.All).Take(20001).Count();
            if (totalCells > 20000) { errors.Add(UnderstandingIdentity.Error("ANALYSIS_CELL_LIMIT", sheet.Name)); continue; }
            if (sheet.Tables.Any() || sheet.PivotTables.Any() || sheet.ConditionalFormats.Any() || sheet.DataValidations.Any())
                errors.Add(UnderstandingIdentity.Error("UNSUPPORTED_STRUCTURE", sheet.Name));
            foreach (var cell in sheet.CellsUsed().Where(c => c.HasFormula))
                if (!ExcelLayoutValidator.SupportedFormula(cell.FormulaA1))
                    errors.Add(UnderstandingIdentity.Error("UNSUPPORTED_FORMULA", sheet.Name + "!" + cell.Address));
        }
        return errors.ToImmutableArray();
    }
}
