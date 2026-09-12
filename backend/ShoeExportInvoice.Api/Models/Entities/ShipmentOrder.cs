using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("ShipmentOrders")]
public class ShipmentOrder
{
    public int? ContractFolderId { get; set; }
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string InvoiceNo { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? PoSuffix { get; set; }

    [MaxLength(100)]
    public string? ContractNo { get; set; }

    [Required]
    [MaxLength(255)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? DeliveryTerms { get; set; }

    [MaxLength(100)]
    public string? PaymentTerms { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // === THÔNG TIN HẢI QUAN & THÔNG QUAN (VNACCS) ===
    [MaxLength(50)]
    public string? DeclarationNo { get; set; }           // Số tờ khai (vd: 308883922820)

    public DateTime? ClearanceDate { get; set; }        // Ngày đăng ký/thông quan (vd: 2026-08-24 13:04:12)

    [MaxLength(20)]
    public string? CustomsDeclarationType { get; set; } // Mã loại hình (vd: E52)

    public int? CustomsChannel { get; set; }             // 1: Luồng Xanh, 2: Luồng Vàng, 3: Luồng Đỏ

    [MaxLength(100)]
    public string? CustomsOffice { get; set; }           // Chi cục HQ (vd: HQHGCT)

    public int? CustomsPackageQty { get; set; }          // Số lượng kiện trên tờ khai (vd: 140 PK)

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? CustomsGrossWeight { get; set; }     // Tổng Gross Weight (vd: 456 KGM)

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? CustomsTotalDap { get; set; }        // Tổng trị giá hóa đơn USD (vd: 13,585.20)

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? CustomsTotalCmt { get; set; }        // Tổng tiền gia công USD (vd: 5,043.96)

    [MaxLength(255)]
    public string? CustomsAttachmentFileName { get; set; } // Tên file .xls đính kèm

    [MaxLength(500)]
    public string? CustomsAttachmentFilePath { get; set; } // Đường dẫn lưu file trên máy chủ

    public bool IsLocked { get; set; } = false; // Khóa chỉnh sửa hồ sơ sau khi thông quan

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Exported; // Trạng thái đơn hàng

    public ICollection<ShipmentOrderItem> Items { get; set; } = new List<ShipmentOrderItem>();
}
