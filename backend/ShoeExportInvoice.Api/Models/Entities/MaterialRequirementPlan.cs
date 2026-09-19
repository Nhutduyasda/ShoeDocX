using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("MaterialRequirementPlans")]
public class MaterialRequirementPlan : ITenantEntity
{
    [Key] public int Id { get; set; }
    public Guid? TenantId { get; set; }
    public int ProductionOrderId { get; set; }
    public ProductionOrder ProductionOrder { get; set; } = null!;
    public int BomMasterId { get; set; }
    [Required, MaxLength(30)] public string BomVersion { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public long Version { get; set; } = 1;
    public ICollection<MaterialRequirementPlanItem> Items { get; set; } = new List<MaterialRequirementPlanItem>();
}

[Table("MaterialRequirementPlanItems")]
public class MaterialRequirementPlanItem : ITenantEntity
{
    [Key] public int Id { get; set; }
    public Guid? TenantId { get; set; }
    public int MaterialRequirementPlanId { get; set; }
    public MaterialRequirementPlan Plan { get; set; } = null!;
    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    [Required, MaxLength(50)] public string MaterialCode { get; set; } = string.Empty;
    [Required, MaxLength(255)] public string MaterialName { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Unit { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    [Column(TypeName = "decimal(18, 4)")] public decimal NetConsumption { get; set; }
    [Column(TypeName = "decimal(18, 4)")] public decimal WastageRatePercent { get; set; }
    [Column(TypeName = "decimal(18, 4)")] public decimal RequiredQuantity { get; set; }
    [Column(TypeName = "decimal(18, 4)")] public decimal StockAtCalculation { get; set; }
    public ICollection<MaterialRequirementPlanSize> SizeBreakdown { get; set; } = new List<MaterialRequirementPlanSize>();
}

[Table("MaterialRequirementPlanSizes")]
public class MaterialRequirementPlanSize : ITenantEntity
{
    [Key] public int Id { get; set; }
    public Guid? TenantId { get; set; }
    public int MaterialRequirementPlanItemId { get; set; }
    public MaterialRequirementPlanItem PlanItem { get; set; } = null!;
    [Required, MaxLength(30)] public string SizeName { get; set; } = string.Empty;
    public int OrderQuantity { get; set; }
    [Column(TypeName = "decimal(18, 4)")] public decimal RequiredQuantity { get; set; }
}
