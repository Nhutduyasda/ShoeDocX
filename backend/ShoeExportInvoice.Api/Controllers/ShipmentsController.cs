using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using System.Text.Json;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShipmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IExcelImportExportService _excelService;
    private readonly ISequenceService _sequenceService;
    private readonly ILogger<ShipmentsController> _logger;

    public ShipmentsController(
        AppDbContext context,
        IExcelImportExportService excelService,
        ISequenceService sequenceService,
        ILogger<ShipmentsController> logger)
    {
        _context = context;
        _excelService = excelService;
        _sequenceService = sequenceService;
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
    /// Xuất file Excel đa sheet (INV, PKL, Sheet2) chuẩn hóa đơn xuất khẩu từ file mẫu.
    /// - Nếu đơn có CẢ HAI loại hàng (Standard + GoKhongMay): tự động tách 2 file và đóng gói ZIP.
    /// - Nếu đơn chỉ có 1 loại: trả về 1 file XLSX với tên KM3-26-DH{XXX}.xlsx.
    /// - Tên file đồng bộ với số cuối của Invoice No.
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
            // Phân loại items theo loại công đoạn
            var goItems = request.Items.Where(i => i.ProcessType == ProcessType.GoKhongMay).ToList();
            var standardItems = request.Items.Where(i => i.ProcessType == ProcessType.Standard).ToList();

            bool hasBothTypes = goItems.Count > 0 && standardItems.Count > 0;

            if (hasBothTypes)
            {
                // ===== TÁCH 2 FILE: Lấy 2 số thứ tự liên tiếp =====
                var seqNumbers = await _sequenceService.GetNextSequenceNumbersAsync(2);
                int goSeq = seqNumbers[0];      // File Gò lấy số nhỏ hơn
                int standardSeq = seqNumbers[1]; // File Thành hình lấy số tiếp theo

                string goInvoiceNo = _sequenceService.ToInvoiceNo(goSeq);
                string standardInvoiceNo = _sequenceService.ToInvoiceNo(standardSeq);
                string goFileName = _sequenceService.ToFileName(goSeq);
                string standardFileName = _sequenceService.ToFileName(standardSeq);

                // Tạo 2 request riêng cho mỗi loại hàng
                var goRequest = CloneRequestWithItems(request, goItems, goInvoiceNo);
                var standardRequest = CloneRequestWithItems(request, standardItems, standardInvoiceNo);

                // Lưu CẢ HAI đơn hàng thực tế vào DB với trạng thái Exported (Chờ thông quan)
                try
                {
                    await SaveOrUpdateShipmentInternalAsync(goRequest, ShipmentStatus.Exported);
                    await SaveOrUpdateShipmentInternalAsync(standardRequest, ShipmentStatus.Exported);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tự động lưu 2 đơn hàng tách khi xuất Excel: {Message}", ex.Message);
                }

                // Xuất 2 file và đóng gói ZIP
                var zipBytes = await _excelService.ExportSplitToZipAsync(goRequest, goFileName, standardRequest, standardFileName);

                // Thông tin tổng kết để frontend hiển thị
                var exportResult = new ExportResultDto
                {
                    HasTwoFiles = true,
                    GoFileName = goFileName,
                    GoInvoiceNo = goInvoiceNo,
                    GoTotalQuantity = goItems.Sum(i => i.Quantity),
                    GoSequenceNumber = goSeq,
                    StandardFileName = standardFileName,
                    StandardInvoiceNo = standardInvoiceNo,
                    StandardTotalQuantity = standardItems.Sum(i => i.Quantity),
                    StandardSequenceNumber = standardSeq,
                };

                // Đặt tên file ZIP theo cặp số thứ tự
                string zipName = $"KM3-26-DH{goSeq}-{standardSeq}.zip";

                Response.Headers["X-Export-Info"] = JsonSerializer.Serialize(exportResult, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                Response.Headers["Access-Control-Expose-Headers"] = "X-Export-Info";

                _logger.LogInformation("Xuất 2 file tách: {GoFile} ({GoQty} đôi) + {StdFile} ({StdQty} đôi) → ZIP: {ZipName}",
                    goFileName, exportResult.GoTotalQuantity, standardFileName, exportResult.StandardTotalQuantity, zipName);

                return File(zipBytes, "application/zip", zipName);
            }
            else
            {
                // ===== 1 FILE DUY NHẤT =====
                // Ưu tiên dùng Invoice No từ request nếu hợp lệ, ngược lại cấp số mới
                string invoiceNo = request.InvoiceNo?.Trim() ?? string.Empty;
                string fileName;

                var extractedSeq = _sequenceService.ExtractSequenceNumber(invoiceNo);
                if (extractedSeq.HasValue)
                {
                    // Invoice No đã có số hợp lệ → dùng luôn, không tiêu thụ số mới
                    fileName = _sequenceService.ToFileName(extractedSeq.Value);
                }
                else
                {
                    // Invoice No không theo chuẩn → cấp 1 số mới
                    var seqNumbers = await _sequenceService.GetNextSequenceNumbersAsync(1);
                    int seq = seqNumbers[0];
                    invoiceNo = _sequenceService.ToInvoiceNo(seq);
                    request.InvoiceNo = invoiceNo;
                    fileName = _sequenceService.ToFileName(seq);
                }

                // Lưu đơn hàng 1 file vào DB với trạng thái Exported (Chờ thông quan)
                try
                {
                    await SaveOrUpdateShipmentInternalAsync(request, ShipmentStatus.Exported);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tự động lưu đơn hàng 1 file khi xuất Excel: {Message}", ex.Message);
                }

                var excelBytes = await _excelService.ExportShipmentMultiSheetExcelAsync(request);

                var exportResult = new ExportResultDto
                {
                    HasTwoFiles = false,
                    SingleFileName = fileName,
                    SingleInvoiceNo = invoiceNo,
                    SingleTotalQuantity = request.Items.Sum(i => i.Quantity),
                };

                Response.Headers["X-Export-Info"] = JsonSerializer.Serialize(exportResult, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                Response.Headers["Access-Control-Expose-Headers"] = "X-Export-Info";

                _logger.LogInformation("Xuất 1 file: {FileName} ({Qty} đôi)", fileName, exportResult.SingleTotalQuantity);

                return File(
                    excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xuất file Excel đa sheet cho đơn hàng.");
            return StatusCode(500, new { message = "Lỗi khi xuất file Excel", detail = ex.Message });
        }
    }

    /// <summary>
    /// Lấy số thứ tự Invoice tiếp theo sẽ được cấp (chưa tiêu thụ)
    /// </summary>
    [HttpGet("sequence/current")]
    public async Task<IActionResult> GetCurrentSequence()
    {
        try
        {
            var nextNumber = await _sequenceService.GetCurrentNextNumberAsync();
            return Ok(new
            {
                nextNumber,
                previewInvoiceNo = _sequenceService.ToInvoiceNo(nextNumber),
                previewFileName = _sequenceService.ToFileName(nextNumber)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đọc số thứ tự hiện tại.");
            return StatusCode(500, new { message = "Lỗi khi đọc số thứ tự", detail = ex.Message });
        }
    }

    /// <summary>
    /// Ghi đè số thứ tự bắt đầu. Lần xuất tiếp theo sẽ bắt đầu từ nextNumber.
    /// </summary>
    [HttpPut("sequence")]
    public async Task<IActionResult> SetSequence([FromBody] SetSequenceRequest body)
    {
        if (body == null || body.NextNumber <= 0)
        {
            return BadRequest(new { message = "nextNumber phải lớn hơn 0." });
        }

        try
        {
            await _sequenceService.SetNextSequenceNumberAsync(body.NextNumber);
            return Ok(new
            {
                message = $"Đã ghi đè thành công. Lần xuất tiếp theo sẽ bắt đầu từ {body.NextNumber}.",
                nextNumber = body.NextNumber,
                previewInvoiceNo = _sequenceService.ToInvoiceNo(body.NextNumber),
                previewFileName = _sequenceService.ToFileName(body.NextNumber)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi ghi đè số thứ tự.");
            return StatusCode(500, new { message = "Lỗi khi ghi đè số thứ tự", detail = ex.Message });
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

    private async Task<ShipmentOrder> SaveOrUpdateShipmentInternalAsync(CreateShipmentRequestDto request, ShipmentStatus status = ShipmentStatus.Draft)
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

            // Cập nhật trạng thái nếu đơn cũ đang là Draft hoặc status mới được chỉ định là Exported
            if (status == ShipmentStatus.Exported || existing.Status == ShipmentStatus.Draft)
            {
                existing.Status = status;
            }

            _context.ShipmentOrderItems.RemoveRange(existing.Items);

            foreach (var item in request.Items)
            {
                var key = item.StyleCode.Trim().ToUpperInvariant();
                dbProducts.TryGetValue(key, out var pm);

                bool isGo = item.ProcessType == ProcessType.GoKhongMay;
                var cmt = ResolvePrice(item.UnitPriceCMT, isGo, pm?.UnitPriceCMT_Go, pm?.UnitPriceCMT);
                var dap = ResolvePrice(item.UnitPriceDAP, isGo, pm?.UnitPriceDAP_Go, pm?.UnitPriceDAP);

                var fullCode = BuildFullItemCode(item, request.PoSuffix);

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
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            var key = item.StyleCode.Trim().ToUpperInvariant();
            dbProducts.TryGetValue(key, out var pm);

            bool isGo = item.ProcessType == ProcessType.GoKhongMay;
            var cmt = ResolvePrice(item.UnitPriceCMT, isGo, pm?.UnitPriceCMT_Go, pm?.UnitPriceCMT);
            var dap = ResolvePrice(item.UnitPriceDAP, isGo, pm?.UnitPriceDAP_Go, pm?.UnitPriceDAP);

            var fullCode = BuildFullItemCode(item, request.PoSuffix);

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
            Status = (int)s.Status,
            StatusName = s.Status.ToString(),
            s.DeclarationNo,
            s.ClearanceDate,
            s.CustomsDeclarationType,
            s.CustomsChannel,
            s.CustomsOffice,
            s.CustomsPackageQty,
            s.CustomsGrossWeight,
            s.CustomsTotalDap,
            s.CustomsTotalCmt,
            s.CustomsAttachmentFileName,
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

        // Đặt tên file chuẩn từ Invoice No
        var seqNum = _sequenceService.ExtractSequenceNumber(shipment.InvoiceNo);
        var fileName = seqNum.HasValue
            ? _sequenceService.ToFileName(seqNum.Value)
            : $"{shipment.InvoiceNo.Trim().Replace("/", "-").Replace("\\", "-")}.xlsx";

        return File(
            excelBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ====== Helper methods ======

    /// <summary>
    /// Clone request với danh sách items mới và Invoice No mới
    /// </summary>
    private static CreateShipmentRequestDto CloneRequestWithItems(
        CreateShipmentRequestDto original,
        List<CreateShipmentItemDto> newItems,
        string newInvoiceNo)
    {
        return new CreateShipmentRequestDto
        {
            InvoiceNo = newInvoiceNo,
            InvoiceDate = original.InvoiceDate,
            PoSuffix = original.PoSuffix,
            ContractNo = original.ContractNo,
            CustomerName = original.CustomerName,
            Address = original.Address,
            DeliveryTerms = original.DeliveryTerms,
            PaymentTerms = original.PaymentTerms,
            Items = newItems
        };
    }

    /// <summary>
    /// Xây dựng FullItemCode từ item và PoSuffix
    /// </summary>
    private static string BuildFullItemCode(CreateShipmentItemDto item, string? poSuffix)
    {
        if (!string.IsNullOrWhiteSpace(item.FullItemCode)) return item.FullItemCode;

        return item.ProcessType == ProcessType.GoKhongMay
            ? $"{item.StyleCode}.G {poSuffix}".Trim()
            : $"{item.StyleCode} {poSuffix}".Trim();
    }

    /// <summary>
    /// Giải quyết đơn giá: ưu tiên giá người dùng nhập, sau đó giá Gò nếu là hàng Gò, cuối cùng giá thường
    /// </summary>
    private static decimal ResolvePrice(decimal? userPrice, bool isGo, decimal? goPricePm, decimal? standardPricePm)
    {
        if (userPrice.HasValue && userPrice.Value > 0) return userPrice.Value;
        if (isGo && goPricePm.HasValue && goPricePm.Value > 0) return goPricePm.Value;
        return standardPricePm ?? 0m;
    }
}

/// <summary>Request body cho PUT /api/shipments/sequence</summary>
public record SetSequenceRequest(int NextNumber);
