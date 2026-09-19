using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Services;

public class BomCalculationService : IBomCalculationService
{
    private readonly AppDbContext _context;

    public BomCalculationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MaterialRequirementResultDto> CalculateAsync(
        int productionOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.ProductionOrders
            .Include(o => o.SizeRuns)
            .Include(o => o.MaterialRequirementPlan)
                .ThenInclude(p => p!.Items)
                    .ThenInclude(i => i.SizeBreakdown)
            .SingleOrDefaultAsync(o => o.Id == productionOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy lệnh sản xuất #{productionOrderId}.");

        if (order.Status is ProductionOrderStatus.Approved or ProductionOrderStatus.Issued or ProductionOrderStatus.Cancelled)
            throw new InvalidOperationException("Chỉ lệnh ở trạng thái Nháp hoặc Đã tính BOM mới có thể tính lại.");

        if (order.SizeRuns.Count == 0)
            throw new InvalidOperationException("Lệnh sản xuất chưa có phân bổ size.");

        var sizeRunTotal = order.SizeRuns.Sum(s => s.Quantity);
        if (sizeRunTotal != order.TotalQuantity)
            throw new InvalidOperationException(
                $"Tổng số lượng theo size ({sizeRunTotal}) không khớp tổng số lượng lệnh sản xuất ({order.TotalQuantity}).");

        var bom = await _context.BomMasters
            .AsNoTracking()
            .Where(b => b.StyleCode == order.StyleCode && b.ProcessType == order.ProcessType)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Include(b => b.Items)
                .ThenInclude(i => i.Material)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy BOM cho mã giày '{order.StyleCode}' và công đoạn '{order.ProcessType}'.");

        if (bom.Items.Count == 0)
            throw new InvalidOperationException($"BOM '{bom.StyleCode}' phiên bản '{bom.Version}' chưa có vật tư.");

        var result = new MaterialRequirementResultDto
        {
            ProductionOrderId = order.Id,
            OrderNo = order.OrderNo,
            StyleCode = order.StyleCode,
            BomMasterId = bom.Id,
            BomVersion = bom.Version,
            TotalQuantity = sizeRunTotal
        };

        foreach (var item in bom.Items.OrderBy(i => i.Material.MaterialCode))
        {
            var wastageMultiplier = 1m + item.WastageRatePercent / 100m;
            var materialResult = new MaterialRequirementItemDto
            {
                MaterialId = item.MaterialId,
                MaterialCode = item.Material.MaterialCode,
                MaterialName = item.Material.MaterialName,
                Unit = item.Material.Unit,
                MaterialType = item.Material.MaterialType,
                NetConsumption = item.NetConsumption,
                WastageRatePercent = item.WastageRatePercent
            };
            materialResult.CurrentStock = item.Material.CurrentStock;

            if (item.Material.MaterialType == MaterialType.Common)
            {
                materialResult.TotalRequiredQuantity = sizeRunTotal * item.NetConsumption * wastageMultiplier;
            }
            else
            {
                materialResult.SizeBreakdown = order.SizeRuns
                    .OrderBy(s => s.SizeName)
                    .Select(s => new SizeMaterialRequirementDto
                    {
                        SizeName = s.SizeName,
                        OrderQuantity = s.Quantity,
                        RequiredQuantity = s.Quantity * item.NetConsumption * wastageMultiplier
                    })
                    .ToList();
                materialResult.TotalRequiredQuantity = materialResult.SizeBreakdown.Sum(s => s.RequiredQuantity);
            }

            result.Materials.Add(materialResult);
            materialResult.ShortageQuantity = Math.Max(0, materialResult.TotalRequiredQuantity - materialResult.CurrentStock);
        }

        if (order.MaterialRequirementPlan != null)
            _context.MaterialRequirementPlans.Remove(order.MaterialRequirementPlan);

        order.MaterialRequirementPlan = new MaterialRequirementPlan
        {
            BomMasterId = bom.Id,
            BomVersion = bom.Version,
            CalculatedAt = DateTime.UtcNow,
            Items = result.Materials.Select(material => new MaterialRequirementPlanItem
            {
                MaterialId = material.MaterialId,
                MaterialCode = material.MaterialCode,
                MaterialName = material.MaterialName,
                Unit = material.Unit,
                MaterialType = material.MaterialType,
                NetConsumption = material.NetConsumption,
                WastageRatePercent = material.WastageRatePercent,
                RequiredQuantity = material.TotalRequiredQuantity,
                StockAtCalculation = material.CurrentStock,
                SizeBreakdown = material.SizeBreakdown.Select(size => new MaterialRequirementPlanSize
                {
                    SizeName = size.SizeName,
                    OrderQuantity = size.OrderQuantity,
                    RequiredQuantity = size.RequiredQuantity
                }).ToList()
            }).ToList()
        };
        order.Status = ProductionOrderStatus.Calculated;
        await _context.SaveChangesAsync(cancellationToken);

        return result;
    }
}
