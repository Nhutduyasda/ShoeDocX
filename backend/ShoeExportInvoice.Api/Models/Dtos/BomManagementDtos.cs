using System.ComponentModel.DataAnnotations;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class BomMasterOptionDto
{
    public int Id { get; set; }
    public string StyleCode { get; set; } = string.Empty;

    [EnumDataType(typeof(ProcessType))]
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class SaveProductionOrderDto
{
    [Required, MaxLength(50)]
    public string OrderNo { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string StyleCode { get; set; } = string.Empty;

    [EnumDataType(typeof(ProcessType))]
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;

    [MinLength(1)]
    public List<SaveOrderSizeRunDto> SizeRuns { get; set; } = new();
}

public class SaveOrderSizeRunDto
{
    [Required, MaxLength(30)]
    public string SizeName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class SavedProductionOrderDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string StyleCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; }
    public ProductionOrderStatus Status { get; set; }
    public int TotalQuantity { get; set; }
}

public class ProductionOrderPlanDto
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string StyleCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; }
    public ProductionOrderStatus Status { get; set; }
    public int TotalQuantity { get; set; }
    public int? BomMasterId { get; set; }
    public string? BomVersion { get; set; }
    public DateTime? CalculatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public List<ProductionOrderPlanItemDto> Materials { get; set; } = new();
}

public class ProductionOrderPlanItemDto
{
    public int MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal ShortageQuantity => Math.Max(0, RequiredQuantity - CurrentStock);
    public List<SizeMaterialRequirementDto> SizeBreakdown { get; set; } = new();
}

public class MaterialDto
{
    public int Id { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public decimal CurrentStock { get; set; }
}

public class SaveMaterialDto
{
    [Required, MaxLength(50)] public string MaterialCode { get; set; } = string.Empty;
    [Required, MaxLength(255)] public string MaterialName { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Unit { get; set; } = string.Empty;
    [EnumDataType(typeof(MaterialType))] public MaterialType MaterialType { get; set; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal CurrentStock { get; set; }
}

public class BomDefinitionDto
{
    public int Id { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BomDefinitionItemDto> Items { get; set; } = new();
}

public class BomDefinitionItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public decimal NetConsumption { get; set; }
    public decimal WastageRatePercent { get; set; }
}

public class SaveBomDefinitionDto
{
    [Required, MaxLength(50)] public string StyleCode { get; set; } = string.Empty;
    [EnumDataType(typeof(ProcessType))] public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    [Required, MaxLength(30)] public string Version { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    [MinLength(1)] public List<SaveBomDefinitionItemDto> Items { get; set; } = new();
}

public class SaveBomDefinitionItemDto
{
    [Range(1, int.MaxValue)] public int MaterialId { get; set; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal NetConsumption { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal WastageRatePercent { get; set; }
}
