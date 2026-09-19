using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

public enum MaterialType
{
    Common = 0,
    SizeDependent = 1
}

[Table("Materials")]
public class Material : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public Guid? TenantId { get; set; }

    [Required, MaxLength(50)]
    public string MaterialCode { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string MaterialName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Unit { get; set; } = string.Empty;

    [EnumDataType(typeof(MaterialType))]
    public MaterialType MaterialType { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal CurrentStock { get; set; }

    public ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();
}
