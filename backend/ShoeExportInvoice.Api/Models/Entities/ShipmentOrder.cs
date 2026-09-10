using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("ShipmentOrders")]
public class ShipmentOrder
{
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

    public ICollection<ShipmentOrderItem> Items { get; set; } = new List<ShipmentOrderItem>();
}
