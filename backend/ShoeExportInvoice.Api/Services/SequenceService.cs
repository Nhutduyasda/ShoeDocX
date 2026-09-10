using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;
using System.Text.RegularExpressions;

namespace ShoeExportInvoice.Api.Services;

public class SequenceService : ISequenceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SequenceService> _logger;

    // Key dùng để lưu trong bảng SystemSettings
    private const string LastSequenceKey = "LastSequenceNumber";

    // Số bắt đầu mặc định nếu chưa có trong DB
    private const int DefaultStartNumber = 232; // Số tiếp theo sẽ là 233

    public SequenceService(AppDbContext context, ILogger<SequenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int[]> GetNextSequenceNumbersAsync(int count = 1)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Số lượng phải lớn hơn 0.");
        if (count > 100) throw new ArgumentOutOfRangeException(nameof(count), "Không thể cấp quá 100 số cùng lúc.");

        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == LastSequenceKey);

        int lastUsed = DefaultStartNumber;
        if (setting != null && int.TryParse(setting.Value, out var saved))
        {
            lastUsed = saved;
        }

        // Cấp N số tiếp theo
        var numbers = Enumerable.Range(lastUsed + 1, count).ToArray();
        int newLast = lastUsed + count;

        if (setting == null)
        {
            _context.SystemSettings.Add(new SystemSetting
            {
                Key = LastSequenceKey,
                Value = newLast.ToString(),
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = newLast.ToString();
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã cấp {Count} số thứ tự: {Numbers}", count, string.Join(", ", numbers));
        return numbers;
    }

    /// <inheritdoc/>
    public async Task SetNextSequenceNumberAsync(int nextNumber)
    {
        if (nextNumber <= 0) throw new ArgumentOutOfRangeException(nameof(nextNumber), "Số thứ tự phải lớn hơn 0.");

        // Lưu (nextNumber - 1) vì lần tiếp theo sẽ cấp nextNumber
        int lastUsed = nextNumber - 1;

        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == LastSequenceKey);

        if (setting == null)
        {
            _context.SystemSettings.Add(new SystemSetting
            {
                Key = LastSequenceKey,
                Value = lastUsed.ToString(),
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = lastUsed.ToString();
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã ghi đè số thứ tự: lần tiếp theo sẽ bắt đầu từ {NextNumber}", nextNumber);
    }

    /// <inheritdoc/>
    public async Task<int> GetCurrentNextNumberAsync()
    {
        var setting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == LastSequenceKey);

        if (setting != null && int.TryParse(setting.Value, out var saved))
        {
            return saved + 1;
        }

        return DefaultStartNumber + 1;
    }

    /// <inheritdoc/>
    public string ToInvoiceNo(int sequenceNumber)
    {
        return $"KMHD-NEW2026-0{sequenceNumber}";
    }

    /// <inheritdoc/>
    public string ToFileName(int sequenceNumber)
    {
        return $"KM3-26-DH{sequenceNumber}.xlsx";
    }

    /// <inheritdoc/>
    public int? ExtractSequenceNumber(string invoiceNo)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo)) return null;

        // Trích xuất phần số ở cuối chuỗi Invoice No
        // Ví dụ: "KMHD-NEW2026-0233" -> "233" -> 233
        var match = Regex.Match(invoiceNo.Trim(), @"\d+$");
        if (match.Success && int.TryParse(match.Value, out var num) && num > 0)
        {
            return num;
        }
        return null;
    }

    /// <summary>
    /// Trích xuất tên file chuẩn từ Invoice No.
    /// Ví dụ: "KMHD-NEW2026-0233" -> "KM3-26-DH233.xlsx"
    /// Fallback: "CUSTOM-INV-ABC" -> "CUSTOM-INV-ABC.xlsx"
    /// </summary>
    public string InvoiceNoToFileName(string invoiceNo)
    {
        var seqNum = ExtractSequenceNumber(invoiceNo);
        if (seqNum.HasValue)
        {
            return ToFileName(seqNum.Value);
        }

        // Fallback: dùng safeInvoiceNo
        var safe = invoiceNo.Trim()
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("?", "")
            .Replace("*", "")
            .Replace(":", "")
            .Replace("|", "")
            .Replace("\"", "")
            .Replace("<", "")
            .Replace(">", "");
        return $"{safe}.xlsx";
    }
}
