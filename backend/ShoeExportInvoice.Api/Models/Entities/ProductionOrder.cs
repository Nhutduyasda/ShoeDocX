using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

public enum ProductionOrderStatus
{
    Draft = 0,
    Calculated = 1,
    Approved = 2,
    Issued = 3,
    Cancelled = 4
}

[Table("ProductionOrders")]
public class ProductionOrder : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public Guid? TenantId { get; set; }

    [Required, MaxLength(50)]
    public string OrderNo { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string StyleCode { get; set; } = string.Empty;

    [EnumDataType(typeof(ProcessType))]
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;

    [EnumDataType(typeof(ProductionOrderStatus))]
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Draft;

    [Range(0, int.MaxValue)]
    public int TotalQuantity { get; set; }

    public ICollection<OrderSizeRun> SizeRuns { get; set; } = new List<OrderSizeRun>();
    public MaterialRequirementPlan? MaterialRequirementPlan { get; set; }
}
