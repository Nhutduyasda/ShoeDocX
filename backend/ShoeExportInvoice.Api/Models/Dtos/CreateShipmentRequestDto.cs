using System.ComponentModel.DataAnnotations;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class CreateShipmentRequestDto
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UseSavedSnapshot { get; set; }
    public int? OrderId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn hợp đồng/danh mục áp dụng")]
    [Range(1, int.MaxValue)]
    public int? ContractFolderId { get; set; }
    [Required(ErrorMessage = "Số hóa đơn (Invoice No) không được để trống")]
    [MaxLength(100)]
    public string InvoiceNo { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string PoSuffix { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContractNo { get; set; } = string.Empty;

    [MaxLength(255)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DeliveryTerms { get; set; } = "DAP";

    [MaxLength(100)]
    public string PaymentTerms { get; set; } = "T/T";

    /// <summary>Số thứ tự hóa đơn bắt đầu cấp phát (nếu người dùng chỉ định cụ thể, ví dụ: 233)</summary>
    public int? StartInvoiceNumber { get; set; }

    /// <summary>Thứ tự ưu tiên cấp số khi đơn hàng có cả Thành hình và Gò không may</summary>
    [EnumDataType(typeof(ExportSequencePriority))]
    public ExportSequencePriority Priority { get; set; } = ExportSequencePriority.StandardFirst;

    [Required]
    [MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 mặt hàng")]
    public List<CreateShipmentItemDto> Items { get; set; } = new();
}

public enum ExportSequencePriority
{
    StandardFirst = 1, // Thành hình trước, Gò sau
    GoFirst = 2        // Gò trước, Thành hình sau
}

public class CreateShipmentItemDto
{
    [Required(ErrorMessage = "Mã hình thể gốc không được để trống")]
    [MaxLength(50)]
    public string StyleCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FullItemCode { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
    public int Quantity { get; set; }

    [EnumDataType(typeof(ProcessType))]
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;

    [Range(typeof(decimal), "0", "999999999.9999")]
    public decimal? UnitPriceCMT { get; set; }

    [Range(typeof(decimal), "0", "999999999.9999")]
    public decimal? UnitPriceDAP { get; set; }

    [MaxLength(30)]
    public string Unit { get; set; } = "đôi";

    [Range(1, 1000)]
    public int? PairPerCarton { get; set; }
}
