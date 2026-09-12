namespace ShoeExportInvoice.Api.Models.Entities;

public enum WarehouseBatchStatus
{
    Draft = 0,             // Bản nháp đang nhập
    SubmittedToXnk = 1,    // Đã bàn giao cho XNK
    ProcessedByXnk = 2     // XNK đã tiếp nhận tạo đơn hàng
}

public class WarehouseBatch
{
    public int Id { get; set; }
    public string BatchName { get; set; } = string.Empty;       // VD: LẦN 14 27/8 5BUY HD THÀNH HÌNH
    public string BatchNumber { get; set; } = string.Empty;     // VD: LẦN 14
    public DateTime ExportDate { get; set; } = DateTime.Today;  // Ngày xuất: 27/08/2026
    public string ContractNote { get; set; } = string.Empty;    // VD: 5BUY HD THÀNH HÌNH hoặc 6BUY COLUM
    public int? ContractFolderId { get; set; }
    public MasterDataFolder? ContractFolder { get; set; }
    public WarehouseBatchStatus Status { get; set; } = WarehouseBatchStatus.Draft;
    public int TotalQuantity { get; set; }
    public int? ShipmentOrderId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    public ICollection<WarehouseBatchItem> Items { get; set; } = new List<WarehouseBatchItem>();
}

public class WarehouseBatchItem
{
    public int Id { get; set; }
    public int WarehouseBatchId { get; set; }
    public WarehouseBatch WarehouseBatch { get; set; } = null!;
    public string StyleCode { get; set; } = string.Empty;                     // Mã giày: 40700-066
    public int Quantity { get; set; }                                         // Số đôi: 288
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;     // Standard (Thành hình) hay GoKhongMay (Gò không may)
    public bool IsPendingReview { get; set; } = false;                        // Mã tạm tạo nhanh tại hiện trường
    public int DisplayOrder { get; set; } = 0;
    public string? Note { get; set; }
}
