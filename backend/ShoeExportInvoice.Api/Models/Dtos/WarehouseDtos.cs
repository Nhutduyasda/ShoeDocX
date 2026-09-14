using ShoeExportInvoice.Api.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class WarehouseBatchDto
{
    public int Id { get; set; }
    public string BatchName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
    public string ContractNote { get; set; } = string.Empty;
    public int? ContractFolderId { get; set; }
    public string? ContractFolderName { get; set; }
    public WarehouseBatchStatus Status { get; set; }
    public string StatusText => Status switch
    {
        WarehouseBatchStatus.Draft => "Bản nháp",
        WarehouseBatchStatus.SubmittedToXnk => "Đã bàn giao XNK",
        WarehouseBatchStatus.ProcessedByXnk => "XNK đã tiếp nhận",
        _ => Status.ToString()
    };
    public int TotalQuantity { get; set; }
    public int? ShipmentOrderId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public List<WarehouseBatchItemDto> Items { get; set; } = new();
}

public class WarehouseBatchItemDto
{
    public int Id { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public ProcessType ProcessType { get; set; }
    public string ProcessTypeName => ProcessType == ProcessType.GoKhongMay ? "GÒ KHÔNG MAY" : "Thành hình";
    public bool IsPendingReview { get; set; }
    public int DisplayOrder { get; set; }
    public string? Note { get; set; }
}

public class SaveWarehouseBatchRequestDto
{
    public int? Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty; // VD: LẦN 14
    public DateTime ExportDate { get; set; } = DateTime.Today; // VD: 2026-08-27
    public string ContractNote { get; set; } = string.Empty; // VD: 5BUY HD THÀNH HÌNH
    public int? ContractFolderId { get; set; }
    public bool SubmitImmediately { get; set; } = false; // Bấm nút "Bàn giao XNK"
    public List<SaveWarehouseBatchItemRequestDto> Items { get; set; } = new();
}

public class SaveWarehouseBatchItemRequestDto
{
    [Required, MaxLength(50)]
    public string StyleCode { get; set; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
    [EnumDataType(typeof(ProcessType))]
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    public bool IsPendingReview { get; set; } = false;
    public string? Note { get; set; }
}

public class WarehouseBatchSummaryDto
{
    public int Id { get; set; }
    public string BatchName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
    public string ContractNote { get; set; } = string.Empty;
    public WarehouseBatchStatus Status { get; set; }
    public string StatusText => Status switch
    {
        WarehouseBatchStatus.Draft => "Bản nháp",
        WarehouseBatchStatus.SubmittedToXnk => "Đã bàn giao XNK",
        WarehouseBatchStatus.ProcessedByXnk => "XNK đã tiếp nhận",
        _ => Status.ToString()
    };
    public int TotalQuantity { get; set; }
    public int ItemCount { get; set; }
    public int GoCount { get; set; }
    public int ThanhHinhCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}
