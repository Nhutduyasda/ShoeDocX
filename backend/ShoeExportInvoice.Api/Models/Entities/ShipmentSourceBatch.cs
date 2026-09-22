using System.ComponentModel.DataAnnotations;

namespace ShoeExportInvoice.Api.Models.Entities;

public enum ShipmentSourceRelationType
{
    MergeSource = 1,
    SplitSource = 2
}

public sealed class ShipmentSourceBatch
{
    public int ShipmentOrderId { get; set; }
    public ShipmentOrder ShipmentOrder { get; set; } = null!;
    public int SourceBatchId { get; set; }
    public WarehouseBatch SourceBatch { get; set; } = null!;
    public ShipmentSourceRelationType RelationType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
