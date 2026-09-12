using System.ComponentModel.DataAnnotations;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class BatchOcrScanResultDto
{
    public string BatchId { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int ReportedTotal { get; set; }
    public int CalculatedTotal { get; set; }
    public bool IsMatched => ReportedTotal > 0 && ReportedTotal == CalculatedTotal;
    public int Discrepancy => CalculatedTotal - ReportedTotal;
    public List<OcrItemDto> Items { get; set; } = new();
    public bool HasStandardItems { get; set; }
    public bool HasGoItems { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class BatchOcrConfirmRequestDto
{
    public int? ContractFolderId { get; set; }
    [MaxLength(100)]
    public string PoSuffix { get; set; } = "(KM3.PO5.26)";

    [MaxLength(100)]
    public string ContractNo { get; set; } = "KM-HANEW/01-2025";

    [MaxLength(255)]
    public string CustomerName { get; set; } = "CÔNG TY TNHH KINGMAKER III (VIỆT NAM) FOOTWEAR";

    [MaxLength(500)]
    public string Address { get; set; } = "SỐ 1, ĐƯỜNG 4A, KCN VIỆT NAM SINGAPORE, XÃ THỌ PHONG, TỈNH QUẢNG NGÃI, VIỆT NAM";

    [MaxLength(100)]
    public string DeliveryTerms { get; set; } = "DAP";

    [MaxLength(100)]
    public string PaymentTerms { get; set; } = "T/T";

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    /// <summary>Số thứ tự hóa đơn bắt đầu cấp phát (nếu người dùng chỉ định cụ thể, ví dụ: 233)</summary>
    public int? StartInvoiceNumber { get; set; }

    /// <summary>Thứ tự ưu tiên cấp số khi đơn hàng có cả Thành hình và Gò không may</summary>
    public ExportSequencePriority Priority { get; set; } = ExportSequencePriority.StandardFirst;

    [Required]
    [MinLength(1, ErrorMessage = "Danh sách lô xuất không được để trống")]
    public List<BatchScanItemExportDto> Batches { get; set; } = new();
}

public class BatchScanItemExportDto
{
    public string BatchId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<CreateShipmentItemDto> Items { get; set; } = new();
}

public class BatchExportItemSummaryDto
{
    public string BatchId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public ProcessType ProcessType { get; set; }
}

public class BatchExportSummaryDto
{
    public int TotalBatches { get; set; }
    public int TotalFiles { get; set; }
    public int TotalQuantity { get; set; }
    public string ZipFileName { get; set; } = string.Empty;
    public List<BatchExportItemSummaryDto> ExportedShipments { get; set; } = new();
}
