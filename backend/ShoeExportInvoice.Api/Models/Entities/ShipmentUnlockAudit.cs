using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("ShipmentUnlockAudits")]
public class ShipmentUnlockAudit
{
    [Key] public int Id { get; set; }
    public int ShipmentOrderId { get; set; }
    [ForeignKey(nameof(ShipmentOrderId))] public ShipmentOrder? ShipmentOrder { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(450)] public string? UnlockedByUserId { get; set; }
    [MaxLength(256)] public string UnlockedByUserName { get; set; } = string.Empty;
    public ShipmentStatus PreviousStatus { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
