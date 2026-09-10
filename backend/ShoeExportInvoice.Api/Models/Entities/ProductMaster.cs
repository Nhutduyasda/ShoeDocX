using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("ProductMasters")]
public class ProductMaster
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string StyleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18, 4)")]
    [Range(0, 999999999.9999)]
    public decimal UnitPriceCMT { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    [Range(0, 999999999.9999)]
    public decimal UnitPriceDAP { get; set; }

    /// <summary>
    /// Đơn giá CMT dành riêng cho hàng Gò không may. Nếu = 0 hoặc null, fallback về UnitPriceCMT.
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    [Range(0, 999999999.9999)]
    public decimal? UnitPriceCMT_Go { get; set; }

    /// <summary>
    /// Đơn giá DAP dành riêng cho hàng Gò không may. Nếu = 0 hoặc null, fallback về UnitPriceDAP.
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    [Range(0, 999999999.9999)]
    public decimal? UnitPriceDAP_Go { get; set; }

    [Required]
    [MaxLength(30)]
    public string HsCode { get; set; } = "64041990";

    [Required]
    [MaxLength(30)]
    public string Unit { get; set; } = "đôi";

    [Range(1, 1000)]
    public int PairPerCarton { get; set; } = 12;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
