using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShipmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IExcelImportExportService _excelService;
    private readonly ILogger<ShipmentsController> _logger;

    public ShipmentsController(
        AppDbContext context,
        IExcelImportExportService excelService,
        ILogger<ShipmentsController> logger)
    {
        _context = context;
        _excelService = excelService;
        _logger = logger;
    }

    /// <summary>
    /// Xem trước bảng phân rã kiện đóng gói (Packing List Breakdown)
    /// Tự động chia thùng chẵn (12 đôi/thùng) và thùng lẻ, tính dải số kiện lũy kế và trọng lượng Net/Gross
    /// </summary>
    [HttpPost("preview-pkl")]
    public ActionResult<PklPreviewResponseDto> PreviewPklBreakdown([FromBody] CreateShipmentRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 mặt hàng." });
        }

        try
        {
            var preview = _excelService.CalculatePklBreakdown(request);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tính toán phân rã kiện đóng gói.");
            return StatusCode(500, new { message = "Lỗi khi tính toán phân rã đóng gói", detail = ex.Message });
        }
    }

    /// <summary>
    /// Xuất file Excel đa sheet (INV, PKL, Sheet2) chuẩn hóa đơn xuất khẩu từ file mẫu
    /// </summary>
    [HttpPost("export-excel")]
    public async Task<IActionResult> ExportExcel([FromBody] CreateShipmentRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 mặt hàng." });
        }

        try
        {
            // Tự động lưu hoặc cập nhật đơn hàng vào SQLite khi xuất file Excel
            if (!string.IsNullOrWhiteSpace(request.InvoiceNo))
            {
                try
                {
                    await SaveOrUpdateShipmentInternalAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể tự động lưu đơn hàng khi xuất Excel: {Message}", ex.Message);
                }
            }

            var excelBytes = await _excelService.ExportShipmentMultiSheetExcelAsync(request);
            var safeInvoiceNo = string.IsNullOrWhiteSpace(request.InvoiceNo) ? "Shipment" : request.InvoiceNo.Trim().Replace("/", "-").Replace("\\", "-");
            var fileName = $"{safeInvoiceNo}_INV_PKL.xlsx";

            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xuất file Excel đa sheet cho đơn hàng.");
            return StatusCode(500, new { message = "Lỗi khi xuất file Excel", detail = ex.Message });
        }
    }

    /// <summary>
    /// Lưu đơn hàng / hóa đơn xuất khẩu vào cơ sở dữ liệu
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ShipmentOrder>> CreateShipment([FromBody] CreateShipmentRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 mặt hàng." });
        }

        try
        {
            var shipment = await SaveOrUpdateShipmentInternalAsync(request);

            return CreatedAtAction(nameof(GetShipmentById), new { id = shipment.Id }, new
            {
                shipment.Id,
                shipment.InvoiceNo,
                shipment.InvoiceDate,
                shipment.PoSuffix,
                shipment.ContractNo,
                shipment.CustomerName,
                shipment.CreatedAt,
                ItemCount = shipment.Items.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu đơn hàng.");
            return StatusCode(500, new { message = "Lỗi khi lưu đơn hàng", detail = ex.Message });
        }
    }

    private async Task<ShipmentOrder> SaveOrUpdateShipmentInternalAsync(CreateShipmentRequestDto request)
    {
        var invoiceNo = request.InvoiceNo.Trim();
        var existing = await _context.ShipmentOrders
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.InvoiceNo == invoiceNo);

        // Kiểm tra và bổ sung giá trị từ Master Data nếu chưa có
        var styleCodes = request.Items.Select(x => x.StyleCode.Trim().ToUpperInvariant()).Distinct().ToList();
        var dbProducts = await _context.ProductMasters
            .Where(p => styleCodes.Contains(p.StyleCode.ToUpper()))
            .ToDictionaryAsync(p => p.StyleCode.ToUpper(), p => p);

        if (existing != null)
        {
            existing.InvoiceDate = request.InvoiceDate;
            existing.PoSuffix = request.PoSuffix?.Trim();
            existing.ContractNo = request.ContractNo.Trim();
            existing.CustomerName = request.CustomerName.Trim();
            existing.Address = request.Address?.Trim();
            existing.DeliveryTerms = request.DeliveryTerms?.Trim() ?? "DAP";
            existing.PaymentTerms = request.PaymentTerms?.Trim() ?? "T/T";

            _context.ShipmentOrderItems.RemoveRange(existing.Items);

            foreach (var item in request.Items)
            {
                var key = item.StyleCode.Trim().ToUpperInvariant();
                dbProducts.TryGetValue(key, out var pm);

                var cmt = (item.UnitPriceCMT.HasValue && item.UnitPriceCMT > 0)
                    ? item.UnitPriceCMT.Value
                    : (pm?.UnitPriceCMT ?? 0m);

                var dap = (item.UnitPriceDAP.HasValue && item.UnitPriceDAP > 0)
                    ? item.UnitPriceDAP.Value
                    : (pm?.UnitPriceDAP ?? 0m);

                var fullCode = !string.IsNullOrWhiteSpace(item.FullItemCode)
                    ? item.FullItemCode
                    : (item.ProcessType == ProcessType.GoKhongMay
                        ? $"{item.StyleCode}.G {request.PoSuffix}".Trim()
                        : $"{item.StyleCode} {request.PoSuffix}".Trim());

                _context.ShipmentOrderItems.Add(new ShipmentOrderItem
                {
                    ShipmentOrderId = existing.Id,
                    StyleCode = item.StyleCode.Trim(),
                    FullItemCode = fullCode,
                    Quantity = item.Quantity,
                    ProcessType = item.ProcessType,
                    UnitPriceCMT = cmt,
                    UnitPriceDAP = dap
                });
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        var shipment = new ShipmentOrder
        {
            InvoiceNo = invoiceNo,
            InvoiceDate = request.InvoiceDate,
            PoSuffix = request.PoSuffix?.Trim(),
            ContractNo = request.ContractNo.Trim(),
            CustomerName = request.CustomerName.Trim(),
            Address = request.Address?.Trim(),
            DeliveryTerms = request.DeliveryTerms?.Trim() ?? "DAP",
            PaymentTerms = request.PaymentTerms?.Trim() ?? "T/T",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            var key = item.StyleCode.Trim().ToUpperInvariant();
            dbProducts.TryGetValue(key, out var pm);

            var cmt = (item.UnitPriceCMT.HasValue && item.UnitPriceCMT > 0)
                ? item.UnitPriceCMT.Value
                : (pm?.UnitPriceCMT ?? 0m);

            var dap = (item.UnitPriceDAP.HasValue && item.UnitPriceDAP > 0)
                ? item.UnitPriceDAP.Value
                : (pm?.UnitPriceDAP ?? 0m);

            var fullCode = !string.IsNullOrWhiteSpace(item.FullItemCode)
                ? item.FullItemCode
                : (item.ProcessType == ProcessType.GoKhongMay
                    ? $"{item.StyleCode}.G {request.PoSuffix}".Trim()
                    : $"{item.StyleCode} {request.PoSuffix}".Trim());

            shipment.Items.Add(new ShipmentOrderItem
            {
                StyleCode = item.StyleCode.Trim(),
                FullItemCode = fullCode,
                Quantity = item.Quantity,
                ProcessType = item.ProcessType,
                UnitPriceCMT = cmt,
                UnitPriceDAP = dap
            });
        }

        _context.ShipmentOrders.Add(shipment);
        await _context.SaveChangesAsync();
        return shipment;
    }

    /// <summary>
    /// Lấy danh sách các đơn hàng đã lưu
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetShipments()
    {
        var dbShipments = await _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var allStyleCodes = dbShipments
            .SelectMany(s => s.Items.Select(i => i.StyleCode.ToUpper()))
            .Distinct()
            .ToList();

        var productsMap = await _context.ProductMasters
            .AsNoTracking()
            .Where(p => allStyleCodes.Contains(p.StyleCode.ToUpper()))
            .ToDictionaryAsync(p => p.StyleCode.ToUpper(), p => p.PairPerCarton > 0 ? p.PairPerCarton : 12);

        var shipments = dbShipments.Select(s => new
        {
            s.Id,
            s.InvoiceNo,
            s.InvoiceDate,
            s.PoSuffix,
            s.ContractNo,
            s.CustomerName,
            s.DeliveryTerms,
            s.PaymentTerms,
            s.CreatedAt,
            ItemCount = s.Items.Count,
            TotalQuantity = s.Items.Sum(i => i.Quantity),
            TotalAmountCMT = s.Items.Sum(i => i.UnitPriceCMT * i.Quantity),
            TotalAmountDAP = s.Items.Sum(i => i.UnitPriceDAP * i.Quantity),
            TotalCartons = s.Items.Sum(i =>
            {
                int ppc = productsMap.TryGetValue(i.StyleCode.ToUpper(), out var ctn) ? ctn : 12;
                return (int)Math.Ceiling((double)i.Quantity / (double)ppc);
            })
        }).ToList();

        return Ok(shipments);
    }

    /// <summary>
    /// Lấy chi tiết một đơn hàng theo Id
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ShipmentOrder>> GetShipmentById(int id)
    {
        var shipment = await _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shipment == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng #{id}" });
        }

        return Ok(shipment);
    }

    /// <summary>
    /// Xuất file Excel từ một đơn hàng đã lưu trong cơ sở dữ liệu
    /// </summary>
    [HttpGet("{id}/export-excel")]
    public async Task<IActionResult> ExportShipmentById(int id)
    {
        var shipment = await _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shipment == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng #{id}" });
        }

        var styleCodes = shipment.Items.Select(x => x.StyleCode.Trim().ToUpperInvariant()).Distinct().ToList();
        var dbProducts = await _context.ProductMasters
            .Where(p => styleCodes.Contains(p.StyleCode.ToUpper()))
            .ToDictionaryAsync(p => p.StyleCode.ToUpper(), p => p);

        var request = new CreateShipmentRequestDto
        {
            InvoiceNo = shipment.InvoiceNo,
            InvoiceDate = shipment.InvoiceDate,
            PoSuffix = shipment.PoSuffix ?? string.Empty,
            ContractNo = shipment.ContractNo ?? string.Empty,
            CustomerName = shipment.CustomerName,
            Address = shipment.Address ?? string.Empty,
            DeliveryTerms = shipment.DeliveryTerms ?? "DAP",
            PaymentTerms = shipment.PaymentTerms ?? "T/T",
            Items = shipment.Items.Select(i =>
            {
                dbProducts.TryGetValue(i.StyleCode.Trim().ToUpperInvariant(), out var pm);
                return new CreateShipmentItemDto
                {
                    StyleCode = i.StyleCode,
                    FullItemCode = i.FullItemCode,
                    Description = pm?.Description ?? string.Empty,
                    Quantity = i.Quantity,
                    ProcessType = i.ProcessType,
                    UnitPriceCMT = i.UnitPriceCMT,
                    UnitPriceDAP = i.UnitPriceDAP,
                    Unit = pm?.Unit ?? "đôi",
                    PairPerCarton = pm?.PairPerCarton ?? 12
                };
            }).ToList()
        };

        var excelBytes = await _excelService.ExportShipmentMultiSheetExcelAsync(request);
        var safeInvoiceNo = shipment.InvoiceNo.Trim().Replace("/", "-").Replace("\\", "-");
        var fileName = $"{safeInvoiceNo}_INV_PKL.xlsx";

        return File(
            excelBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
