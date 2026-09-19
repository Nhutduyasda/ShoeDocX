using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("OrderSizeRuns")]
public class OrderSizeRun : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public Guid? TenantId { get; set; }

    [Required]
    public int ProductionOrderId { get; set; }

    [ForeignKey(nameof(ProductionOrderId))]
    public ProductionOrder ProductionOrder { get; set; } = null!;

    [Required, MaxLength(30)]
    public string SizeName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
