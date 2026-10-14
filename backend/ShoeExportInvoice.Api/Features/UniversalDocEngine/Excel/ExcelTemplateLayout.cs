using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

public enum MissingValuePolicy { Blank, Preserve }
public sealed record ExcelCellMapping(FieldReference Source, string Cell,
    MissingValuePolicy Missing = MissingValuePolicy.Blank);
public sealed record ExcelColumnMapping(FieldReference Source, int Column);
// v1 deliberately supports exactly one prototype row, at StartRow, per worksheet.
// All non-formula contents in this owned row are cleared before writing items.
public sealed record ExcelItemRegion(int StartRow, int PrototypeRow, int MaximumRows,
    ImmutableArray<ExcelColumnMapping> Columns);
public sealed record ExcelSheetLayout(string Worksheet, ImmutableArray<ExcelCellMapping> Headers,
    ExcelItemRegion Items);
public sealed record ExcelTemplateLayout(int Version, string TemplateSha256,
    ImmutableArray<ExcelSheetLayout> Sheets)
{
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
    public static string TemplateIdentity(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
public sealed record ExcelProjectionError(string Code, string Location, string Message);
public sealed record ExcelProjectionResult(byte[]? Workbook, ImmutableArray<ExcelProjectionError> Errors)
{
    public bool Success => Workbook is not null && Errors.IsEmpty;
}
