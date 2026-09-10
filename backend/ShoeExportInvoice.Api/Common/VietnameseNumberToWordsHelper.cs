namespace ShoeExportInvoice.Api.Common;

public static class VietnameseNumberToWordsHelper
{
    private static readonly string[] DigitWords =
    {
        "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"
    };

    private static readonly string[] ScaleWords =
    {
        "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ"
    };

    /// <summary>
    /// Chuyển đổi số tiền USD sang chữ tiếng Việt chuẩn hóa đơn thương mại.
    /// Ví dụ: 61844.40 -> "Bằng chữ: Sáu mươi mốt nghìn tám trăm bốn mươi bốn đô la mỹ và bốn mươi cents"
    /// 15200.00 -> "Bằng chữ: Mười lăm nghìn hai trăm đô la mỹ chẵn"
    /// </summary>
    public static string ToVietnameseWords(decimal amount, string currencyName = "đô la mỹ", string subCurrencyName = "cents")
    {
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        long wholePart = (long)Math.Floor(amount);
        int centsPart = (int)Math.Round((amount - wholePart) * 100, MidpointRounding.AwayFromZero);

        string wholeWords;
        if (wholePart == 0)
        {
            wholeWords = "không";
        }
        else
        {
            wholeWords = ConvertIntegerToWords(wholePart);
        }

        // Viết hoa chữ cái đầu tiên của chuỗi số
        if (!string.IsNullOrEmpty(wholeWords))
        {
            wholeWords = char.ToUpper(wholeWords[0]) + wholeWords.Substring(1);
        }

        string result = $"Bằng chữ: {wholeWords} {currencyName}";

        if (centsPart == 0)
        {
            result += " chẵn";
        }
        else
        {
            string centsWords = ConvertCentsToWords(centsPart);
            result += $" và {centsWords} {subCurrencyName}";
        }

        return result;
    }

    private static string ConvertIntegerToWords(long number)
    {
        if (number == 0) return "không";

        var groups = new List<int>();
        long temp = number;
        while (temp > 0)
        {
            groups.Add((int)(temp % 1000));
            temp /= 1000;
        }

        var wordsList = new List<string>();
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            int groupValue = groups[i];
            if (groupValue == 0) continue;

            bool isHighest = (i == groups.Count - 1);
            string groupText = ReadThreeDigits(groupValue, isHighest);

            if (!string.IsNullOrWhiteSpace(groupText))
            {
                string scale = (i < ScaleWords.Length) ? ScaleWords[i] : "";
                if (!string.IsNullOrWhiteSpace(scale))
                {
                    wordsList.Add($"{groupText} {scale}");
                }
                else
                {
                    wordsList.Add(groupText);
                }
            }
        }

        return string.Join(" ", wordsList).Trim();
    }

    private static string ReadThreeDigits(int number, bool isHighest)
    {
        int hundred = number / 100;
        int tens = (number % 100) / 10;
        int unit = number % 10;

        var parts = new List<string>();

        // Đọc hàng trăm
        if (hundred > 0)
        {
            parts.Add($"{DigitWords[hundred]} trăm");
        }
        else if (!isHighest && (tens > 0 || unit > 0))
        {
            parts.Add("không trăm");
        }

        // Đọc hàng chục
        if (tens > 1)
        {
            parts.Add($"{DigitWords[tens]} mươi");
        }
        else if (tens == 1)
        {
            parts.Add("mười");
        }
        else if (tens == 0 && unit > 0)
        {
            if (!isHighest || hundred > 0)
            {
                parts.Add("linh");
            }
        }

        // Đọc hàng đơn vị
        if (unit > 0)
        {
            if (unit == 1)
            {
                if (tens > 1)
                {
                    parts.Add("mốt");
                }
                else
                {
                    parts.Add("một");
                }
            }
            else if (unit == 4)
            {
                // Chuẩn hóa đơn thương mại: "bốn mươi bốn"
                parts.Add("bốn");
            }
            else if (unit == 5)
            {
                if (tens > 0)
                {
                    parts.Add("lăm");
                }
                else
                {
                    parts.Add("năm");
                }
            }
            else
            {
                parts.Add(DigitWords[unit]);
            }
        }

        return string.Join(" ", parts).Trim();
    }

    private static string ConvertCentsToWords(int cents)
    {
        if (cents <= 0) return "";
        if (cents < 10)
        {
            // Với 1-9 cents: 5 -> "năm", 1 -> "một", 4 -> "bốn"
            return DigitWords[cents];
        }

        return ReadThreeDigits(cents, isHighest: true);
    }
}
