using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

public sealed class TypedValueNormalizer
{
    private static readonly Regex DotNumber = new(@"^[+-]?(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex CommaNumber = new(@"^[+-]?(?:[0-9]+|[0-9]{1,3}(?:\.[0-9]{3})+)(?:,[0-9]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    internal static readonly ImmutableArray<string> SupportedDateFormats =
        ImmutableArray.Create("yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd.MM.yyyy", "yyyyMMdd");
    public NormalizedValue Normalize(FieldType type, string? raw, FieldNormalizationRule? rule = null)
    {
        if (!Enum.IsDefined(type)) return new(NormalizedValueState.Invalid, null, "INVALID_TYPE");
        if (raw?.Length > ExtractionLimits.TextLength) return new(NormalizedValueState.Invalid, null, "TEXT_LIMIT");
        TypedFieldValue Empty() => type switch
        {
            FieldType.Text => TypedFieldValue.FromText(raw), FieldType.Decimal => TypedFieldValue.FromDecimal(null),
            FieldType.Date => TypedFieldValue.FromDate(null), _ => TypedFieldValue.FromBoolean(null)
        };
        if (raw is null) return new(NormalizedValueState.Invalid, null, "RAW_TEXT_MISSING");
        if (string.IsNullOrWhiteSpace(raw)) return new(NormalizedValueState.ObservedEmpty, Empty(), "OBSERVED_EMPTY");
        if (type == FieldType.Text) return new(NormalizedValueState.Value, TypedFieldValue.FromText(raw), null);
        var text = raw.Trim();
        if (type == FieldType.Decimal)
        {
            var conventions = rule?.DecimalConventions ?? ImmutableArray.Create(DecimalConvention.DotDecimal, DecimalConvention.CommaDecimal);
            var values = new HashSet<decimal>();
            if (conventions.IsDefaultOrEmpty) return Invalid();
            foreach (var convention in conventions)
            {
                if (!Enum.IsDefined(convention)) return Invalid();
                var dot = convention == DecimalConvention.DotDecimal;
                // Strict group sizes; no currency, exponent, parentheses, Unicode digits or implicit rounding.
                if (!(dot ? DotNumber : CommaNumber).IsMatch(text)) continue;
                var normalized = text.Replace(dot ? "," : ".", "").Replace(dot ? "." : ",", ".");
                // decimal.TryParse may round >28 fractional digits. Require exact representability.
                var digits = normalized.TrimStart('+', '-').Split('.');
                var fraction = digits.Length == 2 ? digits[1].TrimEnd('0') : "";
                var significant = (digits[0].TrimStart('0') + fraction).TrimStart('0');
                if (fraction.Length > 28 || significant.Length > 29) continue;
                if (decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var number))
                {
                    // Verify parse did not round a 29-digit input with fractional part.
                    var canonical = number.ToString("0.############################", CultureInfo.InvariantCulture);
                    var requested = normalized.TrimStart('+');
                    static string NumericText(string value)
                    {
                        var negative = value.StartsWith('-'); var parts = value.TrimStart('-').Split('.');
                        var whole = parts[0].TrimStart('0'); if (whole.Length == 0) whole = "0";
                        var tail = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
                        return (negative && (whole != "0" || tail.Length != 0) ? "-" : "") + whole + (tail.Length == 0 ? "" : "." + tail);
                    }
                    if (NumericText(canonical) == NumericText(requested)) values.Add(number);
                }
            }
            return values.Count switch { 0 => Invalid(), 1 => Good(TypedFieldValue.FromDecimal(values.Single())), _ => Ambiguous() };
        }
        if (type == FieldType.Date)
        {
            var formats = rule?.DateFormats ?? SupportedDateFormats;
            if (formats.IsDefaultOrEmpty || formats.Any(f => !SupportedDateFormats.Contains(f))) return Invalid();
            var values = formats.Select(f => DateOnly.TryParseExact(text, f, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? (DateOnly?)date : null).Where(d => d.HasValue).Distinct().ToArray();
            return values.Length switch { 0 => Invalid(), 1 => Good(TypedFieldValue.FromDate(values[0])), _ => Ambiguous() };
        }
        var trueTokens = rule?.TrueTokens ?? ImmutableArray.Create("true");
        var falseTokens = rule?.FalseTokens ?? ImmutableArray.Create("false");
        if (trueTokens.IsDefaultOrEmpty || falseTokens.IsDefaultOrEmpty) return Invalid();
        var isTrue = trueTokens.Contains(text, StringComparer.OrdinalIgnoreCase);
        var isFalse = falseTokens.Contains(text, StringComparer.OrdinalIgnoreCase);
        return isTrue && isFalse ? Ambiguous() : isTrue || isFalse ? Good(TypedFieldValue.FromBoolean(isTrue)) : Invalid();
        NormalizedValue Invalid() => new(NormalizedValueState.Invalid, null, "INVALID_" + type.ToString().ToUpperInvariant());
        NormalizedValue Ambiguous() => new(NormalizedValueState.Ambiguous, null, "AMBIGUOUS_" + type.ToString().ToUpperInvariant());
        static NormalizedValue Good(TypedFieldValue value) => new(NormalizedValueState.Value, value, null);
    }
}
