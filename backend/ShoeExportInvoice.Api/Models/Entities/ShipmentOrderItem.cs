using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("ShipmentOrderItems")]
public class ShipmentOrderItem
{
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "đôi";
    public int PairPerCarton { get; set; } = 12;
    [Key]
    public int Id { get; set; }

    public int ShipmentOrderId { get; set; }

    [ForeignKey(nameof(ShipmentOrderId))]
    public ShipmentOrder? ShipmentOrder { get; set; }

    [Required]
    [MaxLength(50)]
    public string StyleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullItemCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    public ProcessType ProcessType { get; set; } = ProcessType.Standard;

    [Column(TypeName = "decimal(18, 4)")]
    [Range(0, 999999999.9999)]
    public decimal UnitPriceCMT { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    [Range(0, 999999999.9999)]
    public decimal UnitPriceDAP { get; set; }
}
