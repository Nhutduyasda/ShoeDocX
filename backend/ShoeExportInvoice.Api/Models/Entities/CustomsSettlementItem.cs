using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("CustomsSettlementItems")]
public class CustomsSettlementItem
{
    [Key]
    public int Id { get; set; }

    public int SettlementPeriodId { get; set; }

    [ForeignKey(nameof(SettlementPeriodId))]
    public CustomsSettlementPeriod? SettlementPeriod { get; set; }

    [Required]
    [MaxLength(50)]
    public string ProductCode { get; set; } = string.Empty; // Mã hình thể (vd: 42072-030)

    [MaxLength(255)]
    public string ProductName { get; set; } = string.Empty; // Tên sản phẩm / Mô tả hải quan

    [MaxLength(50)]
    public string Unit { get; set; } = "đôi";               // ĐVT

    [Column(TypeName = "decimal(18, 2)")]
    public decimal OpeningBalance { get; set; } = 0;        // Tồn đầu kỳ

    [Column(TypeName = "decimal(18, 2)")]
    public decimal InPeriodProduction { get; set; } = 0;    // Nhập trong kỳ (từ sản xuất)

    [Column(TypeName = "decimal(18, 2)")]
    public decimal InPeriodExport { get; set; } = 0;        // Xuất trong kỳ (tự động tính từ E52 đã thông quan)

    [Column(TypeName = "decimal(18, 2)")]
    public decimal OtherExport { get; set; } = 0;           // Xuất khác

    [Column(TypeName = "decimal(18, 2)")]
    public decimal ClosingBalance { get; set; } = 0;        // Tồn cuối kỳ = (OpeningBalance + InPeriodProduction) - (InPeriodExport + OtherExport)

    [MaxLength(500)]
    public string? Note { get; set; }
}
