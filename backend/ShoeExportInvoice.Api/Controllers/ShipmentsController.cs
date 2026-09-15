using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;
using System.Text.Json;
using System.Security.Claims;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShipmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IExcelImportExportService _excelService;
    private readonly ISequenceService _sequenceService;
    private readonly ILogger<ShipmentsController> _logger;
    private readonly IBusinessAuditService? _audit;

    public ShipmentsController(
        AppDbContext context,
        IExcelImportExportService excelService,
        ISequenceService sequenceService,
        ILogger<ShipmentsController> logger,
        IBusinessAuditService? audit = null)
    {
        _context = context;
        _excelService = excelService;
        _sequenceService = sequenceService;
        _logger = logger;
        _audit = audit;
    }

    /// <summary>
    /// Xem trước bảng phân rã kiện đóng gói (Packing List Breakdown)
    /// Tự động chia thùng chẵn (12 đôi/thùng) và thùng lẻ, tính dải số kiện lũy kế và trọng lượng Net/Gross
    /// </summary>
    [HttpPost("preview-pkl")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<ActionResult<PklPreviewResponseDto>> PreviewPklBreakdown([FromBody] CreateShipmentRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 mặt hàng." });
        }

        try
        {
            ShipmentSizeBreakdownValidator.Validate(request.Items);
            await ApplyAuthoritativeMasterDataAsync(request);
            var preview = _excelService.CalculatePklBreakdown(request);
            return Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tính toán phân rã kiện đóng gói.");
            return StatusCode(500, new { message = "Lỗi khi tính toán phân rã đóng gói" });
        }
    }

    /// <summary>
    /// Xem trước đầy đủ chứng từ xuất khẩu (Commercial Invoice & Packing List)
    /// </summary>
    [HttpPost("preview-document")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<ActionResult<DocumentPreviewResponseDto>> PreviewDocument([FromBody] CreateShipmentRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 mặt hàng." });
        }

        try
        {
            ShipmentSizeBreakdownValidator.Validate(request.Items);
            await ApplyAuthoritativeMasterDataAsync(request);
            var preview = _excelService.CalculateDocumentPreview(request);
            return Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tính toán dữ liệu xem trước chứng từ.");
            return StatusCode(500, new { message = "Lỗi khi tính toán dữ liệu xem trước chứng từ" });
        }
    }

    /// <summary>
    /// Xuất file Excel đa sheet (INV, PKL, Sheet2) chuẩn hóa đơn xuất khẩu từ file mẫu.
    /// - Nếu đơn có CẢ HAI loại hàng (Standard + GoKhongMay): tự động tách 2 file và đóng gói ZIP.
    /// - Nếu đơn chỉ có 1 loại: trả về 1 file XLSX theo mẫu tên file của đối tác.
    /// - Tên file đồng bộ với số cuối của Invoice No.
    /// </summary>
    [HttpPost("export-excel")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> ExportExcel([FromBody] CreateShipmentRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 mặt hàng." });
        }

        // Chốt chặn nghiệp vụ (Business Rule Validation Guard):
        // Tuyệt đối không cho xuất file nếu còn bất kỳ mã nào chưa tồn tại trong Master Data (ProductMaster).
        var distinctCodes = request.Items
            .Select(i => (i.StyleCode ?? string.Empty).Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lookupCodes = distinctCodes
            .Select(c => c.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? c[..^2].Trim() : c)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingCodes = await _context.ProductMasters.Where(p => p.FolderId == request.ContractFolderId)
            .AsNoTracking()
            .Where(p => lookupCodes.Contains(p.StyleCode) || distinctCodes.Contains(p.StyleCode))
            .Select(p => p.StyleCode.ToUpper())
            .ToListAsync();

        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var missingCodes = distinctCodes.Where(code =>
        {
            var clean = code.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? code[..^2].Trim() : code;
            return !existingSet.Contains(code) && !existingSet.Contains(clean);
        }).ToList();

        if (missingCodes.Count > 0)
        {
            return BadRequest(new
            {
                success = false,
                message = $"Không thể xuất file! Các mã sau chưa được đăng ký trong Master Data: [{string.Join(", ", missingCodes)}]",
                missingCodes
            });
        }

        try
        {
            ShipmentSizeBreakdownValidator.Validate(request.Items);
            await ApplyAuthoritativeMasterDataAsync(request);
            var partnerFolder = await _context.MasterDataFolders
                .AsNoTracking()
                .FirstAsync(f => f.Id == request.ContractFolderId!.Value);
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            // Phân loại items theo loại công đoạn
            var goItems = request.Items.Where(i => i.ProcessType == ProcessType.GoKhongMay).ToList();
            var standardItems = request.Items.Where(i => i.ProcessType == ProcessType.Standard).ToList();

            bool hasBothTypes = goItems.Count > 0 && standardItems.Count > 0;

            if (hasBothTypes)
            {
                // Cấp phát nguyên tử trong DB để hai phiên xuất đồng thời không thể nhận cùng số.
                var requestedStart = request.StartInvoiceNumber ?? _sequenceService.ExtractSequenceNumber(request.InvoiceNo);
                var reserved = await _sequenceService.ReservePartnerSequenceNumbersAsync(partnerFolder.Id, 2, requestedStart);
                int firstSeq = reserved[0], secondSeq = reserved[1];

                int standardSeq, goSeq;
                if (request.Priority == ExportSequencePriority.GoFirst)
                {
                    goSeq = firstSeq;
                    standardSeq = secondSeq;
                }
                else
                {
                    // Mặc định: StandardFirst (Thành hình trước, Gò sau)
                    standardSeq = firstSeq;
                    goSeq = secondSeq;
                }

                string goInvoiceNo = PartnerDocumentPatternFormatter.InvoiceNo(partnerFolder.InvoiceNoPattern, goSeq);
                string standardInvoiceNo = PartnerDocumentPatternFormatter.InvoiceNo(partnerFolder.InvoiceNoPattern, standardSeq);
                string goFileName = PartnerDocumentPatternFormatter.FileName(partnerFolder.FileNamePattern, goSeq);
                string standardFileName = PartnerDocumentPatternFormatter.FileName(partnerFolder.FileNamePattern, standardSeq);

                // Tạo 2 request riêng cho mỗi loại hàng
                var goRequest = CloneRequestWithItems(request, goItems, goInvoiceNo);
                var standardRequest = CloneRequestWithItems(request, standardItems, standardInvoiceNo);

                // Lưu CẢ HAI đơn hàng thực tế vào DB với trạng thái Exported (Chờ thông quan)
                    await SaveOrUpdateShipmentInternalAsync(goRequest, ShipmentStatus.Exported);
                    await SaveOrUpdateShipmentInternalAsync(standardRequest, ShipmentStatus.Exported);


                // Xuất 2 file và đóng gói ZIP (thứ tự ưu tiên theo cấu hình)
                var zipBytes = request.Priority == ExportSequencePriority.GoFirst
                    ? await _excelService.ExportSplitToZipAsync(goRequest, goFileName, standardRequest, standardFileName)
                    : await _excelService.ExportSplitToZipAsync(standardRequest, standardFileName, goRequest, goFileName);

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

                // Đặt tên file ZIP theo thứ tự ưu tiên
                string zipName = $"{Path.GetFileNameWithoutExtension(PartnerDocumentPatternFormatter.FileName(partnerFolder.FileNamePattern, firstSeq))}-{secondSeq}.zip";

                Response.Headers["X-Export-Info"] = JsonSerializer.Serialize(exportResult, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                Response.Headers["Access-Control-Expose-Headers"] = "X-Export-Info";

                _logger.LogInformation("Xuất 2 file tách ({Priority}): {FirstFile} → {SecondFile} → ZIP: {ZipName}",
                    request.Priority,
                    request.Priority == ExportSequencePriority.GoFirst ? goFileName : standardFileName,
                    request.Priority == ExportSequencePriority.GoFirst ? standardFileName : goFileName,
                    zipName);

                await transaction.CommitAsync();
                return File(zipBytes, "application/zip", zipName);
            }
            else
            {
                // ===== 1 FILE DUY NHẤT =====
                var requestedStart = request.StartInvoiceNumber ?? _sequenceService.ExtractSequenceNumber(request.InvoiceNo);
                int seq = (await _sequenceService.ReservePartnerSequenceNumbersAsync(partnerFolder.Id, 1, requestedStart))[0];
                string invoiceNo = request.InvoiceNo?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(invoiceNo)
                    || int.TryParse(invoiceNo, out _)
                    || _sequenceService.ExtractSequenceNumber(invoiceNo) != seq)
                {
                    invoiceNo = PartnerDocumentPatternFormatter.InvoiceNo(partnerFolder.InvoiceNoPattern, seq);
                }
                request.InvoiceNo = invoiceNo;

                string fileName = PartnerDocumentPatternFormatter.FileName(partnerFolder.FileNamePattern, seq);

                // Lưu đơn hàng 1 file vào DB với trạng thái Exported (Chờ thông quan)
                    await SaveOrUpdateShipmentInternalAsync(request, ShipmentStatus.Exported);


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

                await transaction.CommitAsync();
                return File(
                    excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xuất file Excel đa sheet cho đơn hàng.");
            return StatusCode(500, new { message = "Lỗi khi xuất file Excel" });
        }
    }

    /// <summary>
    /// Lấy số thứ tự Invoice tiếp theo sẽ được cấp (chưa tiêu thụ)
    /// </summary>
    [HttpGet("sequence/current")]
    [Authorize(Roles = "Admin,Xnk")]
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đọc số thứ tự hiện tại.");
            return StatusCode(500, new { message = "Lỗi khi đọc số thứ tự" });
        }
    }

    /// <summary>
    /// Ghi đè số thứ tự bắt đầu. Lần xuất tiếp theo sẽ bắt đầu từ nextNumber.
    /// </summary>
    [HttpPut("sequence")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetSequence([FromBody] SetSequenceRequest body)
    {
        if (body == null || body.NextNumber <= 0 || string.IsNullOrWhiteSpace(body.Reason) || body.Reason.Trim().Length < 10)
        {
            return BadRequest(new { message = "nextNumber phải lớn hơn 0 và lý do phải có ít nhất 10 ký tự." });
        }

        try
        {
            await _sequenceService.SetNextSequenceNumberAsync(body.NextNumber);
            _audit?.Add(HttpContext, "Shipment.GlobalSequenceOverride", "SystemSetting", "LastSequenceNumber",
                next: new { body.NextNumber }, reason: body.Reason.Trim());
            if (_audit != null) await _context.SaveChangesAsync();
            return Ok(new
            {
                message = $"Đã ghi đè thành công. Lần xuất tiếp theo sẽ bắt đầu từ {body.NextNumber}.",
                nextNumber = body.NextNumber,
                previewInvoiceNo = _sequenceService.ToInvoiceNo(body.NextNumber),
                previewFileName = _sequenceService.ToFileName(body.NextNumber)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi ghi đè số thứ tự.");
            return StatusCode(500, new { message = "Lỗi khi ghi đè số thứ tự" });
        }
    }

    /// <summary>
    /// Lưu đơn hàng / hóa đơn xuất khẩu vào cơ sở dữ liệu
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Xnk")]
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
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var shipment = await SaveOrUpdateShipmentInternalAsync(request);
            await transaction.CommitAsync();

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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu đơn hàng.");
            return StatusCode(500, new { message = "Lỗi khi lưu đơn hàng" });
        }
    }

    /// <summary>
    /// Cập nhật thông tin một đơn hàng theo Id (bảo vệ bởi Lock Guard: Không cho phép sửa đơn đã thông quan hoặc bị khóa)
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> UpdateShipment(int id, [FromBody] CreateShipmentRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 mặt hàng." });
        }

        request.OrderId = id;

        var existing = await _context.ShipmentOrders.FindAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng #{id}" });
        }

        if (existing.IsLocked || existing.Status == ShipmentStatus.Cleared)
        {
            return BadRequest(new { message = "Đơn hàng đã thông quan hải quan, không thể chỉnh sửa hoặc xóa!" });
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var shipment = await SaveOrUpdateShipmentInternalAsync(request);
            await transaction.CommitAsync();

            return Ok(new
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật đơn hàng #{Id}", id);
            return StatusCode(500, new { message = "Lỗi khi cập nhật đơn hàng" });
        }
    }

    private async Task<ShipmentOrder> SaveOrUpdateShipmentInternalAsync(CreateShipmentRequestDto request, ShipmentStatus status = ShipmentStatus.Draft)
    {
            ShipmentSizeBreakdownValidator.Validate(request.Items);
            await ApplyAuthoritativeMasterDataAsync(request);
        var invoiceNo = request.InvoiceNo.Trim();
        ShipmentOrder? existing = null;
        if (request.OrderId.HasValue)
        {
            existing = await _context.ShipmentOrders
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == request.OrderId.Value)
                ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng #{request.OrderId.Value}.");

            if (await _context.ShipmentOrders.AnyAsync(s => s.Id != existing.Id && s.InvoiceNo == invoiceNo))
                throw new InvalidOperationException("Số hóa đơn đã được sử dụng bởi đơn hàng khác.");
        }
        else if (await _context.ShipmentOrders.AnyAsync(s => s.InvoiceNo == invoiceNo))
        {
            throw new InvalidOperationException("Số hóa đơn đã được sử dụng. Vui lòng cấp số mới.");
        }

        if (existing != null)
        {
            if (existing.IsLocked || existing.Status == ShipmentStatus.Cleared)
            {
                throw new InvalidOperationException("Đơn hàng đã thông quan hải quan, không thể chỉnh sửa hoặc xóa!");
            }

            existing.InvoiceNo = invoiceNo;
            existing.ContractFolderId = request.ContractFolderId;
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
                var fullCode = BuildFullItemCode(item, request.PoSuffix);

                _context.ShipmentOrderItems.Add(new ShipmentOrderItem
                {
                    ShipmentOrderId = existing.Id,
                    StyleCode = item.StyleCode.Trim().ToUpperInvariant(),
                    Description = item.Description ?? string.Empty,
                    Unit = item.Unit,
                    PairPerCarton = item.PairPerCarton!.Value,
                    FullItemCode = fullCode,
                    Quantity = item.Quantity,
                    ProcessType = item.ProcessType,
                    SizeBreakdownJson = item.SizeBreakdownJson,
                    UnitPriceCMT = item.UnitPriceCMT!.Value,
                    UnitPriceDAP = item.UnitPriceDAP!.Value
                });
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        var shipment = new ShipmentOrder
        {
            ContractFolderId = request.ContractFolderId,
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
            var fullCode = BuildFullItemCode(item, request.PoSuffix);

            shipment.Items.Add(new ShipmentOrderItem
            {
                StyleCode = item.StyleCode.Trim().ToUpperInvariant(),
                    Description = item.Description ?? string.Empty,
                    Unit = item.Unit,
                    PairPerCarton = item.PairPerCarton!.Value,
                FullItemCode = fullCode,
                Quantity = item.Quantity,
                ProcessType = item.ProcessType,
                SizeBreakdownJson = item.SizeBreakdownJson,
                UnitPriceCMT = item.UnitPriceCMT!.Value,
                UnitPriceDAP = item.UnitPriceDAP!.Value
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
    [Authorize(Roles = "Admin,Xnk,KeToan")]
    public async Task<ActionResult<PagedResultDto<object>>> GetShipments([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _context.ShipmentOrders
            .AsNoTracking()
            .Include(s => s.Items);
        var totalCount = await query.CountAsync();
        var dbShipments = await query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var shipments = dbShipments.Select(s => new
        {
            s.Id,
            s.InvoiceNo,
            s.InvoiceDate,
            s.PoSuffix,
            s.ContractFolderId,
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
            HasCustomsAttachment = !string.IsNullOrWhiteSpace(s.CustomsAttachmentFileName),
            IsLocked = s.IsLocked || s.Status == ShipmentStatus.Cleared,
            ItemCount = s.Items.Count,
            TotalQuantity = s.Items.Sum(i => i.Quantity),
            TotalAmountCMT = s.Items.Sum(i => i.UnitPriceCMT * i.Quantity),
            TotalAmountDAP = s.Items.Sum(i => i.UnitPriceDAP * i.Quantity),
            TotalCartons = s.Items.Sum(i =>
            {
                int ppc = i.PairPerCarton > 0 ? i.PairPerCarton : 12;
                return (int)Math.Ceiling((double)i.Quantity / (double)ppc);
            })
        }).ToList();

        return Ok(new PagedResultDto<object> { Items = shipments.Cast<object>().ToList(), TotalCount = totalCount, Page = page, PageSize = pageSize });
    }

    [HttpGet("by-folder")]
    [Authorize(Roles = "Admin,Xnk,KeToan")]
    public async Task<IActionResult> GetShipmentsByFolder(
        [FromQuery] int folderId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] ShipmentStatus? status)
    {
        if (!await _context.MasterDataFolders.AnyAsync(f => f.Id == folderId))
            return NotFound(new { message = "Không tìm thấy thư mục đối tác." });

        var folderIds = await GetFolderAndDescendantIdsAsync(folderId);
        var query = _context.ShipmentOrders.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.ContractFolderId.HasValue && folderIds.Contains(s.ContractFolderId.Value));

        if (fromDate.HasValue) query = query.Where(s => s.InvoiceDate.Date >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(s => s.InvoiceDate.Date <= toDate.Value.Date);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);

        var rows = await query.OrderByDescending(s => s.InvoiceDate).ThenByDescending(s => s.Id).ToListAsync();
        return Ok(rows.Select(ToShipmentSummary));
    }

    [HttpGet("folder-counts")]
    [Authorize(Roles = "Admin,Xnk,KeToan")]
    public async Task<ActionResult<IEnumerable<ShipmentFolderCountDto>>> GetFolderCounts()
    {
        var folders = await _context.MasterDataFolders.AsNoTracking()
            .Select(f => new { f.Id, f.ParentId }).ToListAsync();
        var directCounts = await _context.ShipmentOrders.AsNoTracking()
            .Where(s => s.ContractFolderId.HasValue)
            .GroupBy(s => s.ContractFolderId!.Value)
            .Select(g => new { FolderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FolderId, x => x.Count);

        var children = folders.Where(f => f.ParentId.HasValue).GroupBy(f => f.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        int CountTree(int id, HashSet<int> visited)
        {
            if (!visited.Add(id)) return 0;
            var total = directCounts.GetValueOrDefault(id);
            if (children.TryGetValue(id, out var childIds))
                total += childIds.Sum(childId => CountTree(childId, visited));
            return total;
        }

        return Ok(folders.Select(f => new ShipmentFolderCountDto
        {
            FolderId = f.Id,
            Count = CountTree(f.Id, new HashSet<int>())
        }));
    }

    [HttpPost("batch-print-data")]
    [Authorize(Roles = "Admin,Xnk,KeToan")]
    public async Task<ActionResult<BatchPrintResponseDto>> GetBatchPrintData([FromBody] BatchPrintRequestDto request)
    {
        if (request.ShipmentIds.Count == 0)
            return BadRequest(new { message = "Vui lòng chọn ít nhất một hóa đơn để in." });
        if (request.ShipmentIds.Count > 100)
            return BadRequest(new { message = "Mỗi lần chỉ in tối đa 100 hóa đơn." });

        var documentType = request.DocumentType.Trim().ToUpperInvariant();
        if (documentType is not ("INV" or "PKL" or "ALL"))
            return BadRequest(new { message = "Loại chứng từ phải là INV, PKL hoặc ALL." });

        var ids = request.ShipmentIds.Distinct().ToList();
        var shipments = await _context.ShipmentOrders.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.InvoiceNo)
            .ToListAsync();

        if (shipments.Count != ids.Count)
            return NotFound(new { message = "Một hoặc nhiều hóa đơn đã chọn không còn tồn tại." });

        var result = new BatchPrintResponseDto { DocumentType = documentType };
        foreach (var shipment in shipments)
        {
            var previewRequest = ToPreviewRequest(shipment);
            ShipmentSizeBreakdownValidator.Validate(previewRequest.Items);
            result.Documents.Add(_excelService.CalculateDocumentPreview(previewRequest));
        }

        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết một đơn hàng theo Id
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Xnk,KeToan")]
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
    /// Xóa một đơn hàng theo Id (bảo vệ bởi Lock Guard: Không cho phép xóa đơn đã thông quan)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteShipment(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var shipment = await _context.ShipmentOrders
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shipment == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn hàng #{id}" });
        }

        if (shipment.IsLocked || shipment.Status == ShipmentStatus.Cleared)
        {
            return BadRequest(new { message = "Đơn hàng đã thông quan hải quan, không thể chỉnh sửa hoặc xóa!" });
        }
        if (await _context.WarehouseBatches.AnyAsync(b => b.ShipmentOrderId == id))
            return Conflict(new { message = "Đơn hàng đang được liên kết với lô kho." });
        var businessDate = shipment.ClearanceDate?.Date ?? shipment.InvoiceDate.Date;
        if (await _context.CustomsSettlementPeriods.AnyAsync(p => p.Status == "Finalized" &&
                p.ContractFolderId == shipment.ContractFolderId && p.FromDate <= businessDate && p.ToDate >= businessDate))
            return Conflict(new { message = "Đơn hàng đã thuộc kỳ quyết toán chốt sổ." });

        _audit?.Add(HttpContext, "Shipment.Delete", "ShipmentOrder", shipment.Id,
            previous: new { shipment.InvoiceNo, shipment.Status, shipment.IsLocked });

        _context.ShipmentOrderItems.RemoveRange(shipment.Items);
        _context.ShipmentOrders.Remove(shipment);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
        return Ok(new { message = $"Đã xóa đơn hàng {shipment.InvoiceNo} thành công." });
    }

    /// <summary>
    /// Xóa toàn bộ lịch sử đơn hàng xuất khẩu (Shipment Orders)
    /// </summary>
    [NonAction]
    public IActionResult DeleteAllShipments() => NotFound();

    /// <summary>
    /// Xuất file Excel từ một đơn hàng đã lưu trong cơ sở dữ liệu
    /// </summary>
    [HttpGet("{id}/export-excel")]
    [Authorize(Roles = "Admin,Xnk,KeToan")]
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

        var distinctCodes = shipment.Items
            .Select(x => x.StyleCode.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lookupCodes = distinctCodes
            .Select(c => c.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? c[..^2].Trim() : c)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dbProductList = await _context.ProductMasters.Where(p => p.FolderId == shipment.ContractFolderId)
            .Where(p => lookupCodes.Contains(p.StyleCode) || distinctCodes.Contains(p.StyleCode))
            .ToListAsync();

        var dbProducts = new Dictionary<string, ProductMaster>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in dbProductList)
        {
            dbProducts[p.StyleCode] = p;
        }

        var missingCodes = distinctCodes.Where(code =>
        {
            var clean = code.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? code[..^2].Trim() : code;
            return !dbProducts.ContainsKey(code) && !dbProducts.ContainsKey(clean);
        }).ToList();

        var request = new CreateShipmentRequestDto
        {
            UseSavedSnapshot = true,
            ContractFolderId = shipment.ContractFolderId,
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
                    Description = i.Description,
                    Quantity = i.Quantity,
                    ProcessType = i.ProcessType,
                    SizeBreakdownJson = i.SizeBreakdownJson,
                    UnitPriceCMT = i.UnitPriceCMT,
                    UnitPriceDAP = i.UnitPriceDAP,
                    Unit = i.Unit,
                    PairPerCarton = i.PairPerCarton
                };
            }).ToList()
        };

        var excelBytes = await _excelService.ExportShipmentMultiSheetExcelAsync(request);

        // Đặt tên file chuẩn từ Invoice No
        var partnerFolder = shipment.ContractFolderId.HasValue
            ? await _context.MasterDataFolders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == shipment.ContractFolderId.Value)
            : null;
        var seqNum = _sequenceService.ExtractSequenceNumber(shipment.InvoiceNo);
        var fileName = seqNum.HasValue && partnerFolder != null
            ? PartnerDocumentPatternFormatter.FileName(partnerFolder.FileNamePattern, seqNum.Value)
            : $"{shipment.InvoiceNo.Trim().Replace("/", "-").Replace("\\", "-")}.xlsx";

        return File(
            excelBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpPost("{id:int}/unlock-cleared")]
    [Authorize(Roles = "Admin,Xnk,XnkManager")]
    public async Task<IActionResult> UnlockClearedShipment(int id, [FromBody] UnlockClearedShipmentRequest request)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            return BadRequest(new { message = "Lý do mở khóa phải có ít nhất 10 ký tự." });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var shipment = await _context.ShipmentOrders.FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return NotFound(new { message = $"Không tìm thấy đơn hàng #{id}." });
        if (shipment.Status != ShipmentStatus.Cleared || !shipment.IsLocked)
            return BadRequest(new { message = "Chỉ có thể mở khóa đơn hàng đã thông quan và đang bị khóa." });

        _context.ShipmentUnlockAudits.Add(new ShipmentUnlockAudit
        {
            ShipmentOrderId = shipment.Id,
            Reason = reason,
            PreviousStatus = shipment.Status,
            UnlockedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UnlockedByUserName = User.Identity?.Name ?? "Unknown",
            UnlockedAt = DateTime.UtcNow
        });
        shipment.IsLocked = false;
        shipment.Status = ShipmentStatus.Exported;
        _audit?.Add(HttpContext, "Shipment.Unlock", "ShipmentOrder", shipment.Id,
            previous: new { Status = ShipmentStatus.Cleared, IsLocked = true },
            next: new { Status = ShipmentStatus.Exported, IsLocked = false }, reason: reason);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new { message = "Đã mở khóa đơn hàng để khai bổ sung AMA.", shipment.Id, shipment.Status, shipment.IsLocked });
    }

    [HttpGet("{id:int}/unlock-audits")]
    [Authorize(Roles = "Admin,Xnk,XnkManager,KeToan")]
    public async Task<IActionResult> GetUnlockAudits(int id) => Ok(await _context.ShipmentUnlockAudits
        .AsNoTracking().Where(a => a.ShipmentOrderId == id).OrderByDescending(a => a.UnlockedAt).ToListAsync());

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
            OrderId = original.OrderId,
            TemplateId = original.TemplateId,
            ContractFolderId = original.ContractFolderId,
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

    private async Task ApplyAuthoritativeMasterDataAsync(CreateShipmentRequestDto request)
    {
        if (!request.ContractFolderId.HasValue ||
            !await _context.MasterDataFolders.AnyAsync(f => f.Id == request.ContractFolderId.Value))
            throw new InvalidOperationException("Phải chọn hợp đồng Master Data hợp lệ.");

        if (request.Items.Any(i => !Enum.IsDefined(i.ProcessType)))
            throw new InvalidOperationException("Đơn hàng chứa loại công đoạn không hợp lệ.");

        var requestedCodes = request.Items
            .Select(i => NormalizeRequestedStyleCode(i.StyleCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var products = await _context.ProductMasters
            .AsNoTracking()
            .Where(p => p.FolderId == request.ContractFolderId && requestedCodes.Contains(p.StyleCode))
            .ToListAsync();
        var productMap = products.ToDictionary(p => p.StyleCode, StringComparer.OrdinalIgnoreCase);

        var missingCodes = requestedCodes.Where(c => !productMap.ContainsKey(c)).ToList();
        if (missingCodes.Count > 0)
            throw new InvalidOperationException($"Các mã không thuộc hợp đồng đã chọn: [{string.Join(", ", missingCodes)}]");

        foreach (var item in request.Items)
        {
            var normalizedCode = NormalizeRequestedStyleCode(item.StyleCode);
            var product = productMap[normalizedCode];
            var isGo = item.ProcessType == ProcessType.GoKhongMay;
            var cmt = isGo && product.UnitPriceCMT_Go.GetValueOrDefault() > 0
                ? product.UnitPriceCMT_Go!.Value : product.UnitPriceCMT;
            var dap = isGo && product.UnitPriceDAP_Go.GetValueOrDefault() > 0
                ? product.UnitPriceDAP_Go!.Value : product.UnitPriceDAP;
            if (cmt < 0 || dap <= 0)
                throw new InvalidOperationException($"Mã {normalizedCode} chưa có đơn giá hợp lệ cho công đoạn đã chọn.");

            item.StyleCode = normalizedCode;
            item.Description = product.Description;
            item.Unit = product.Unit;
            item.PairPerCarton = product.PairPerCarton;
            item.UnitPriceCMT = cmt;
            item.UnitPriceDAP = dap;
            item.FullItemCode = BuildFullItemCode(item, request.PoSuffix);
        }
    }

    private static string NormalizeRequestedStyleCode(string? styleCode)
    {
        var normalized = (styleCode ?? string.Empty).Trim().ToUpperInvariant();
        return normalized.EndsWith(".G", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^2].Trim()
            : normalized;
    }

    private async Task<List<int>> GetFolderAndDescendantIdsAsync(int rootId)
    {
        var folders = await _context.MasterDataFolders.AsNoTracking()
            .Select(f => new { f.Id, f.ParentId }).ToListAsync();
        var result = new HashSet<int> { rootId };
        var pending = new Queue<int>();
        pending.Enqueue(rootId);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var child in folders.Where(f => f.ParentId == current))
                if (result.Add(child.Id)) pending.Enqueue(child.Id);
        }
        return result.ToList();
    }

    private static object ToShipmentSummary(ShipmentOrder s) => new
    {
        s.Id, s.InvoiceNo, s.InvoiceDate, s.PoSuffix, s.ContractFolderId, s.ContractNo,
        s.CustomerName, s.DeliveryTerms, s.PaymentTerms, s.CreatedAt,
        Status = (int)s.Status, StatusName = s.Status.ToString(),
        s.DeclarationNo, s.ClearanceDate, s.CustomsDeclarationType, s.CustomsChannel,
        s.CustomsOffice, s.CustomsPackageQty, s.CustomsGrossWeight, s.CustomsTotalDap,
        s.CustomsTotalCmt, s.CustomsAttachmentFileName,
        HasCustomsAttachment = !string.IsNullOrWhiteSpace(s.CustomsAttachmentFileName),
        IsLocked = s.IsLocked || s.Status == ShipmentStatus.Cleared,
        ItemCount = s.Items.Count,
        TotalQuantity = s.Items.Sum(i => i.Quantity),
        TotalAmountCMT = s.Items.Sum(i => i.UnitPriceCMT * i.Quantity),
        TotalAmountDAP = s.Items.Sum(i => i.UnitPriceDAP * i.Quantity),
        TotalCartons = s.Items.Sum(i => (int)Math.Ceiling((double)i.Quantity / Math.Max(i.PairPerCarton, 1)))
    };

    private static CreateShipmentRequestDto ToPreviewRequest(ShipmentOrder shipment) => new()
    {
        UseSavedSnapshot = true,
        ContractFolderId = shipment.ContractFolderId,
        InvoiceNo = shipment.InvoiceNo,
        InvoiceDate = shipment.InvoiceDate,
        PoSuffix = shipment.PoSuffix ?? string.Empty,
        ContractNo = shipment.ContractNo ?? string.Empty,
        CustomerName = shipment.CustomerName,
        Address = shipment.Address ?? string.Empty,
        DeliveryTerms = shipment.DeliveryTerms ?? "DAP",
        PaymentTerms = shipment.PaymentTerms ?? "T/T",
        Items = shipment.Items.Select(i => new CreateShipmentItemDto
        {
            StyleCode = i.StyleCode, FullItemCode = i.FullItemCode, Description = i.Description,
            Quantity = i.Quantity, ProcessType = i.ProcessType, SizeBreakdownJson = i.SizeBreakdownJson,
            UnitPriceCMT = i.UnitPriceCMT, UnitPriceDAP = i.UnitPriceDAP,
            Unit = i.Unit, PairPerCarton = i.PairPerCarton
        }).ToList()
    };
}

/// <summary>Request body cho PUT /api/shipments/sequence</summary>
public record SetSequenceRequest(int NextNumber, string Reason = "");
public record UnlockClearedShipmentRequest(string Reason);
