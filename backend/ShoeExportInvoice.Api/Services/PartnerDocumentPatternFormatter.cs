using System.Text.RegularExpressions;

namespace ShoeExportInvoice.Api.Services;

public static partial class PartnerDocumentPatternFormatter
{
    [GeneratedRegex(@"\{SEQ(?::(?<width>\d+))?\}", RegexOptions.IgnoreCase)]
    private static partial Regex SequenceTokenRegex();

    [GeneratedRegex(@"x+$", RegexOptions.IgnoreCase)]
    private static partial Regex LegacySequenceRegex();

    public static string Format(string? pattern, int sequenceNumber, string fallbackPattern)
    {
        var effectivePattern = string.IsNullOrWhiteSpace(pattern) ? fallbackPattern : pattern.Trim();
        var replaced = SequenceTokenRegex().Replace(effectivePattern, match =>
        {
            var widthText = match.Groups["width"].Value;
            return int.TryParse(widthText, out var width) && width > 0
                ? sequenceNumber.ToString($"D{Math.Min(width, 20)}")
                : sequenceNumber.ToString();
        });

        return replaced == effectivePattern
            ? LegacySequenceRegex().Replace(effectivePattern, sequenceNumber.ToString())
            : replaced;
    }

    public static string InvoiceNo(string? pattern, int sequenceNumber) =>
        Format(pattern, sequenceNumber, "KMHD-NEW2026-{SEQ:4}");

    public static string FileName(string? pattern, int sequenceNumber)
    {
        var result = Format(pattern, sequenceNumber, "KM3-26-DH{SEQ}.xlsx");
        return result.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? result : $"{result}.xlsx";
    }
}
