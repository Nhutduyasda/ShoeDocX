using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class MaterialRequirementResultDto
{
    public int ProductionOrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string StyleCode { get; set; } = string.Empty;
    public int BomMasterId { get; set; }
    public string BomVersion { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public List<MaterialRequirementItemDto> Materials { get; set; } = new();
}

public class MaterialRequirementItemDto
{
    public int MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public decimal NetConsumption { get; set; }
    public decimal WastageRatePercent { get; set; }
    public decimal TotalRequiredQuantity { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal ShortageQuantity { get; set; }
    public List<SizeMaterialRequirementDto> SizeBreakdown { get; set; } = new();
}

public class SizeMaterialRequirementDto
{
    public string SizeName { get; set; } = string.Empty;
    public int OrderQuantity { get; set; }
    public decimal RequiredQuantity { get; set; }
}
