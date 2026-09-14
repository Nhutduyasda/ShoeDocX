namespace ShoeExportInvoice.Api.Services;

public interface ISequenceService
{
    /// <summary>
    /// Lấy N số thứ tự liên tiếp và cập nhật LastSequenceNumber vào DB.
    /// Ví dụ: GetNextAsync(2) trả về [233, 234] và lưu 234 là số cuối cùng đã dùng.
    /// </summary>
    Task<int[]> GetNextSequenceNumbersAsync(int count = 1);
    Task<int[]> ReservePartnerSequenceNumbersAsync(int folderId, int count = 1, int? requestedStart = null);

    /// <summary>
    /// Ghi đè số thứ tự bắt đầu. Lần xuất tiếp theo sẽ dùng số này.
    /// </summary>
    Task SetNextSequenceNumberAsync(int nextNumber);

    /// <summary>
    /// Đọc số thứ tự tiếp theo sẽ được cấp (chưa tiêu thụ).
    /// </summary>
    Task<int> GetCurrentNextNumberAsync();

    /// <summary>
    /// Chuyển đổi số thứ tự thành Invoice No chuẩn. Ví dụ: 233 -> "KMHD-NEW2026-0233"
    /// </summary>
    string ToInvoiceNo(int sequenceNumber);

    /// <summary>
    /// Chuyển đổi số thứ tự thành tên file chuẩn. Ví dụ: 233 -> "KM3-26-DH233.xlsx"
    /// </summary>
    string ToFileName(int sequenceNumber);

    /// <summary>
    /// Trích xuất số thứ tự từ chuỗi Invoice No bất kỳ.
    /// Ví dụ: "KMHD-NEW2026-0233" -> 233. Trả về null nếu không tìm thấy số hợp lệ.
    /// </summary>
    int? ExtractSequenceNumber(string invoiceNo);
}
