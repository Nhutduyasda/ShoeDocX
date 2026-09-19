using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using ClosedXML.Excel;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/bom")]
[Authorize(Roles = "Admin,Kho")]
public class BomController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IBomCalculationService _calculationService;
    private readonly IBusinessAuditService? _audit;

    public BomController(AppDbContext context, IBomCalculationService calculationService, IBusinessAuditService? audit = null)
    {
        _context = context;
        _calculationService = calculationService;
        _audit = audit;
    }

    [HttpGet("masters")]
    public async Task<ActionResult<IReadOnlyList<BomMasterOptionDto>>> GetBomMasters(
        CancellationToken cancellationToken)
    {
        var latest = await _context.BomMasters
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .ToListAsync(cancellationToken);

        return Ok(latest
            .GroupBy(b => new { StyleCode = b.StyleCode.ToUpperInvariant(), b.ProcessType })
            .Select(group => group.First())
            .OrderBy(b => b.StyleCode)
            .Select(b => new BomMasterOptionDto
            {
                Id = b.Id,
                StyleCode = b.StyleCode,
                Version = b.Version,
                Description = b.Description,
                ProcessType = b.ProcessType
            })
            .ToList());
    }

    [HttpGet("definitions")]
    public async Task<ActionResult<IReadOnlyList<BomDefinitionDto>>> GetDefinitions(CancellationToken cancellationToken)
    {
        var entities = await _context.BomMasters.AsNoTracking()
            .Include(b => b.Items).ThenInclude(i => i.Material)
            .OrderBy(b => b.StyleCode).ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(entities.Select(MapDefinition).ToList());
    }

    [HttpPost("definitions")]
    public async Task<ActionResult<BomDefinitionDto>> CreateDefinition(
        SaveBomDefinitionDto request, CancellationToken cancellationToken)
    {
        var entity = new BomMaster { CreatedAt = DateTime.UtcNow };
        await ApplyDefinitionAsync(entity, request, null, cancellationToken);
        _context.BomMasters.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(entity).Collection(b => b.Items).Query().Include(i => i.Material).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetDefinitions), new { id = entity.Id }, MapDefinition(entity));
    }

    [HttpPut("definitions/{id:int}")]
    public async Task<ActionResult<BomDefinitionDto>> UpdateDefinition(
        int id, SaveBomDefinitionDto request, CancellationToken cancellationToken)
    {
        var entity = await _context.BomMasters.Include(b => b.Items)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy BOM #{id}.");
        await ApplyDefinitionAsync(entity, request, id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(entity).Collection(b => b.Items).Query().Include(i => i.Material).LoadAsync(cancellationToken);
        return Ok(MapDefinition(entity));
    }

    [HttpDelete("definitions/{id:int}")]
    public async Task<IActionResult> DeleteDefinition(int id, CancellationToken cancellationToken)
    {
        var entity = await _context.BomMasters.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity == null) return NotFound(new { message = $"Không tìm thấy BOM #{id}." });
        _context.BomMasters.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("production-orders")]
    public async Task<ActionResult<SavedProductionOrderDto>> CreateProductionOrder(
        [FromBody] SaveProductionOrderDto request,
        CancellationToken cancellationToken)
    {
        var order = await SaveOrderAsync(null, request, cancellationToken);
        return CreatedAtAction(nameof(Calculate), new { id = order.Id }, Map(order));
    }

    [HttpPut("production-orders/{id:int}")]
    public async Task<ActionResult<SavedProductionOrderDto>> UpdateProductionOrder(
        int id,
        [FromBody] SaveProductionOrderDto request,
        CancellationToken cancellationToken)
    {
        var order = await SaveOrderAsync(id, request, cancellationToken);
        return Ok(Map(order));
    }

    [HttpPost("production-orders/{id:int}/calculate")]
    public async Task<ActionResult<MaterialRequirementResultDto>> Calculate(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _calculationService.CalculateAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("production-orders")]
    public async Task<ActionResult<IReadOnlyList<ProductionOrderPlanDto>>> GetProductionOrders(CancellationToken cancellationToken)
    {
        var orders = await _context.ProductionOrders.AsNoTracking()
            .Include(o => o.MaterialRequirementPlan)!.ThenInclude(p => p!.Items).ThenInclude(i => i.Material)
            .Include(o => o.MaterialRequirementPlan)!.ThenInclude(p => p!.Items).ThenInclude(i => i.SizeBreakdown)
            .OrderByDescending(o => o.Id).ToListAsync(cancellationToken);
        return Ok(orders.Select(MapPlan).ToList());
    }

    [HttpPost("production-orders/{id:int}/approve")]
    public async Task<ActionResult<ProductionOrderPlanDto>> Approve(int id, CancellationToken cancellationToken)
    {
        var order = await LoadOrderPlanAsync(id, cancellationToken);
        if (order.Status != ProductionOrderStatus.Calculated || order.MaterialRequirementPlan == null)
            return BadRequest(new { message = "Chỉ lệnh đã tính BOM mới có thể duyệt." });
        order.Status = ProductionOrderStatus.Approved;
        order.MaterialRequirementPlan.ApprovedAt = DateTime.UtcNow;
        order.MaterialRequirementPlan.Version++;
        _audit?.Add(HttpContext, "ProductionOrder.Approve", nameof(ProductionOrder), id,
            previous: new { Status = ProductionOrderStatus.Calculated }, next: new { Status = order.Status });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(MapPlan(order));
    }

    [HttpPost("production-orders/{id:int}/issue")]
    public async Task<ActionResult<ProductionOrderPlanDto>> Issue(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var order = await LoadOrderPlanAsync(id, cancellationToken);
        if (order.Status != ProductionOrderStatus.Approved || order.MaterialRequirementPlan == null)
            return BadRequest(new { message = "Chỉ lệnh đã duyệt mới có thể xuất kho." });

        var materialIds = order.MaterialRequirementPlan.Items.Select(i => i.MaterialId).ToList();
        var materials = await _context.Materials.Where(m => materialIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, cancellationToken);
        var shortages = order.MaterialRequirementPlan.Items
            .Where(i => !materials.TryGetValue(i.MaterialId, out var material) || material.CurrentStock < i.RequiredQuantity)
            .Select(i => i.MaterialCode).ToList();
        if (shortages.Count > 0)
            return Conflict(new { message = $"Không đủ tồn kho cho các vật tư: {string.Join(", ", shortages)}." });

        foreach (var item in order.MaterialRequirementPlan.Items)
            materials[item.MaterialId].CurrentStock -= item.RequiredQuantity;
        order.Status = ProductionOrderStatus.Issued;
        order.MaterialRequirementPlan.IssuedAt = DateTime.UtcNow;
        order.MaterialRequirementPlan.Version++;
        _audit?.Add(HttpContext, "ProductionOrder.IssueMaterials", nameof(ProductionOrder), id,
            previous: new { Status = ProductionOrderStatus.Approved },
            next: new { Status = order.Status, Materials = order.MaterialRequirementPlan.Items.Select(i => new { i.MaterialCode, i.RequiredQuantity }) });
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(MapPlan(order));
    }

    [HttpPost("production-orders/{id:int}/cancel")]
    public async Task<ActionResult<ProductionOrderPlanDto>> Cancel(int id, CancellationToken cancellationToken)
    {
        var order = await LoadOrderPlanAsync(id, cancellationToken);
        if (order.Status is ProductionOrderStatus.Approved or ProductionOrderStatus.Issued)
            return BadRequest(new { message = "Không thể hủy lệnh đã duyệt hoặc đã xuất kho." });
        order.Status = ProductionOrderStatus.Cancelled;
        _audit?.Add(HttpContext, "ProductionOrder.Cancel", nameof(ProductionOrder), id,
            next: new { Status = order.Status });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(MapPlan(order));
    }

    [HttpGet("production-orders/{id:int}/export")]
    public async Task<IActionResult> ExportMaterialPlan(int id, CancellationToken cancellationToken)
    {
        var order = await LoadOrderPlanAsync(id, cancellationToken);
        if (order.MaterialRequirementPlan == null)
            return BadRequest(new { message = "Lệnh chưa có kết quả bóc tách vật tư." });

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PHIEU_CAP_PHAT");
        sheet.Cell("A1").Value = "PHIẾU NHU CẦU / CẤP PHÁT NGUYÊN PHỤ LIỆU";
        sheet.Range("A1:H1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell("A3").Value = "Lệnh sản xuất"; sheet.Cell("B3").Value = order.OrderNo;
        sheet.Cell("D3").Value = "Mã hình thể"; sheet.Cell("E3").Value = order.StyleCode;
        sheet.Cell("A4").Value = "Công đoạn"; sheet.Cell("B4").Value = order.ProcessType == ProcessType.GoKhongMay ? "Gò không may" : "Thành hình";
        sheet.Cell("D4").Value = "BOM"; sheet.Cell("E4").Value = order.MaterialRequirementPlan.BomVersion;
        sheet.Cell("A6").InsertData(new[] { new[] { "STT", "Mã vật tư", "Tên NPL", "ĐVT", "Nhu cầu", "Tồn hiện tại", "Thiếu", "Trạng thái" } });
        var row = 7;
        var index = 1;
        foreach (var item in order.MaterialRequirementPlan.Items.OrderBy(i => i.MaterialCode))
        {
            var currentStock = item.Material.CurrentStock;
            sheet.Cell(row, 1).Value = index++;
            sheet.Cell(row, 2).Value = item.MaterialCode;
            sheet.Cell(row, 3).Value = item.MaterialName;
            sheet.Cell(row, 4).Value = item.Unit;
            sheet.Cell(row, 5).Value = item.RequiredQuantity;
            sheet.Cell(row, 6).Value = currentStock;
            sheet.Cell(row, 7).Value = Math.Max(0, item.RequiredQuantity - currentStock);
            sheet.Cell(row, 8).Value = currentStock >= item.RequiredQuantity ? "Đủ" : "Thiếu";
            row++;
        }
        sheet.Range("A6:H6").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        sheet.Columns().AdjustToContents();
        sheet.Column(3).Width = Math.Min(45, sheet.Column(3).Width);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Phieu_cap_phat_{order.OrderNo}.xlsx");
    }

    private async Task<ProductionOrder> SaveOrderAsync(
        int? id,
        SaveProductionOrderDto request,
        CancellationToken cancellationToken)
    {
        var orderNo = request.OrderNo.Trim();
        var styleCode = request.StyleCode.Trim();
        var normalizedSizes = request.SizeRuns
            .Where(s => s.Quantity > 0)
            .Select(s => new SaveOrderSizeRunDto
            {
                SizeName = s.SizeName.Trim(),
                Quantity = s.Quantity
            })
            .ToList();

        if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(styleCode))
            throw new InvalidOperationException("Số lệnh và mã hình thể không được để trống.");
        if (normalizedSizes.Count == 0)
            throw new InvalidOperationException("Phải nhập số lượng cho ít nhất một size.");
        if (normalizedSizes.Any(s => string.IsNullOrWhiteSpace(s.SizeName)) ||
            normalizedSizes.GroupBy(s => s.SizeName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Dải size chứa size trống hoặc bị trùng.");
        if (!await _context.BomMasters.AnyAsync(b => b.StyleCode == styleCode && b.ProcessType == request.ProcessType, cancellationToken))
            throw new InvalidOperationException($"Không tìm thấy BOM cho mã giày '{styleCode}' và công đoạn '{request.ProcessType}'.");

        ProductionOrder order;
        if (id.HasValue)
        {
            order = await _context.ProductionOrders
                .Include(o => o.SizeRuns)
                .SingleOrDefaultAsync(o => o.Id == id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy lệnh sản xuất #{id.Value}.");
            if (order.Status is ProductionOrderStatus.Approved or ProductionOrderStatus.Issued or ProductionOrderStatus.Cancelled)
                throw new InvalidOperationException("Lệnh đã duyệt, đã xuất hoặc đã hủy không thể chỉnh sửa.");
            if (await _context.ProductionOrders.AnyAsync(
                    o => o.OrderNo == orderNo && o.Id != id.Value, cancellationToken))
                throw new InvalidOperationException($"Lệnh sản xuất '{orderNo}' đã tồn tại.");
            _context.OrderSizeRuns.RemoveRange(order.SizeRuns);
            order.SizeRuns.Clear();
        }
        else
        {
            if (await _context.ProductionOrders.AnyAsync(o => o.OrderNo == orderNo, cancellationToken))
                throw new InvalidOperationException($"Lệnh sản xuất '{orderNo}' đã tồn tại.");
            order = new ProductionOrder();
            _context.ProductionOrders.Add(order);
        }

        order.OrderNo = orderNo;
        order.StyleCode = styleCode;
        order.ProcessType = request.ProcessType;
        order.Status = ProductionOrderStatus.Draft;
        order.TotalQuantity = normalizedSizes.Sum(s => s.Quantity);
        order.SizeRuns = normalizedSizes.Select(s => new OrderSizeRun
        {
            SizeName = s.SizeName,
            Quantity = s.Quantity
        }).ToList();
        await _context.SaveChangesAsync(cancellationToken);
        return order;
    }

    private static SavedProductionOrderDto Map(ProductionOrder order) => new()
    {
        Id = order.Id,
        OrderNo = order.OrderNo,
        StyleCode = order.StyleCode,
        ProcessType = order.ProcessType,
        Status = order.Status,
        TotalQuantity = order.TotalQuantity
    };

    private async Task<ProductionOrder> LoadOrderPlanAsync(int id, CancellationToken cancellationToken) =>
        await _context.ProductionOrders
            .Include(o => o.MaterialRequirementPlan)!.ThenInclude(p => p!.Items).ThenInclude(i => i.Material)
            .Include(o => o.MaterialRequirementPlan)!.ThenInclude(p => p!.Items).ThenInclude(i => i.SizeBreakdown)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Không tìm thấy lệnh sản xuất #{id}.");

    private static ProductionOrderPlanDto MapPlan(ProductionOrder order) => new()
    {
        Id = order.Id,
        OrderNo = order.OrderNo,
        StyleCode = order.StyleCode,
        ProcessType = order.ProcessType,
        Status = order.Status,
        TotalQuantity = order.TotalQuantity,
        BomMasterId = order.MaterialRequirementPlan?.BomMasterId,
        BomVersion = order.MaterialRequirementPlan?.BomVersion,
        CalculatedAt = order.MaterialRequirementPlan?.CalculatedAt,
        ApprovedAt = order.MaterialRequirementPlan?.ApprovedAt,
        IssuedAt = order.MaterialRequirementPlan?.IssuedAt,
        Materials = order.MaterialRequirementPlan?.Items.Select(i => new ProductionOrderPlanItemDto
        {
            MaterialId = i.MaterialId,
            MaterialCode = i.MaterialCode,
            MaterialName = i.MaterialName,
            Unit = i.Unit,
            MaterialType = i.MaterialType,
            RequiredQuantity = i.RequiredQuantity,
            CurrentStock = i.Material.CurrentStock,
            SizeBreakdown = i.SizeBreakdown.Select(s => new SizeMaterialRequirementDto
            {
                SizeName = s.SizeName,
                OrderQuantity = s.OrderQuantity,
                RequiredQuantity = s.RequiredQuantity
            }).ToList()
        }).ToList() ?? new()
    };

    private async Task ApplyDefinitionAsync(
        BomMaster entity, SaveBomDefinitionDto request, int? currentId, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("BOM phải có ít nhất một dòng vật tư.");
        if (request.Items.GroupBy(i => i.MaterialId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Một vật tư không thể xuất hiện nhiều lần trong cùng BOM.");

        var styleCode = request.StyleCode.Trim();
        var version = request.Version.Trim();
        if (!await _context.ProductMasters.AnyAsync(p => p.StyleCode == styleCode, cancellationToken))
            throw new InvalidOperationException($"Mã hình thể '{styleCode}' chưa tồn tại trong Product Master.");
        var duplicateQuery = _context.BomMasters.Where(b => b.StyleCode == styleCode &&
            b.ProcessType == request.ProcessType && b.Version == version);
        if (currentId.HasValue) duplicateQuery = duplicateQuery.Where(b => b.Id != currentId.Value);
        if (await duplicateQuery.AnyAsync(cancellationToken))
            throw new InvalidOperationException("BOM cùng mã hình thể, công đoạn và phiên bản đã tồn tại.");

        var materialIds = request.Items.Select(i => i.MaterialId).Distinct().ToList();
        var existingMaterialIds = await _context.Materials.Where(m => materialIds.Contains(m.Id))
            .Select(m => m.Id).ToListAsync(cancellationToken);
        if (existingMaterialIds.Count != materialIds.Count)
            throw new InvalidOperationException("Danh sách BOM chứa vật tư không tồn tại.");

        if (entity.Id != 0)
        {
            _context.BomItems.RemoveRange(entity.Items);
            entity.Items.Clear();
        }
        entity.StyleCode = styleCode;
        entity.ProcessType = request.ProcessType;
        entity.Version = version;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Items = request.Items.Select(i => new BomItem
        {
            MaterialId = i.MaterialId,
            NetConsumption = i.NetConsumption,
            WastageRatePercent = i.WastageRatePercent
        }).ToList();
    }

    private static BomDefinitionDto MapDefinition(BomMaster b) => new()
    {
        Id = b.Id,
        StyleCode = b.StyleCode,
        ProcessType = b.ProcessType,
        Version = b.Version,
        Description = b.Description,
        CreatedAt = b.CreatedAt,
        Items = b.Items.OrderBy(i => i.Material.MaterialCode).Select(i => new BomDefinitionItemDto
        {
            Id = i.Id,
            MaterialId = i.MaterialId,
            MaterialCode = i.Material.MaterialCode,
            MaterialName = i.Material.MaterialName,
            Unit = i.Material.Unit,
            MaterialType = i.Material.MaterialType,
            NetConsumption = i.NetConsumption,
            WastageRatePercent = i.WastageRatePercent
        }).ToList()
    };
}
