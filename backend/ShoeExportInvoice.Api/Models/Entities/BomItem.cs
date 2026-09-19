using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("BomItems")]
public class BomItem : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public Guid? TenantId { get; set; }

    [Required]
    public int BomMasterId { get; set; }

    [ForeignKey(nameof(BomMasterId))]
    public BomMaster BomMaster { get; set; } = null!;

    [Required]
    public int MaterialId { get; set; }

    [ForeignKey(nameof(MaterialId))]
    public Material Material { get; set; } = null!;

    [Column(TypeName = "decimal(18, 4)")]
    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal NetConsumption { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    [Range(typeof(decimal), "0", "100")]
    public decimal WastageRatePercent { get; set; }
}
