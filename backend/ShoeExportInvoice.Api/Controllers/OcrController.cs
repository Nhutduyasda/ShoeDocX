using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Xnk")]
[EnableRateLimiting("ocr")]
public class OcrController : ControllerBase
{
    private const int MaxBatchFiles = 20;
    private const long MaxImageBytes = 20L * 1024 * 1024;
    private const long MaxBatchBytes = 100L * 1024 * 1024;
    private readonly IOcrExtractionService _ocrService;
    private readonly ISequenceService _sequenceService;
    private readonly IExcelImportExportService _excelService;
    private readonly AppDbContext _context;
    private readonly ILogger<OcrController> _logger;
    private readonly XnkOptions _options;

    public OcrController(
        IOcrExtractionService ocrService,
        ISequenceService sequenceService,
        IExcelImportExportService excelService,
        AppDbContext context,
        ILogger<OcrController> logger,
        Microsoft.Extensions.Options.IOptions<XnkOptions>? options = null)
    {
        _ocrService = ocrService;
        _sequenceService = sequenceService;
        _excelService = excelService;
        _context = context;
        _logger = logger;
        _options = options?.Value ?? new XnkOptions();
    }

    /// <summary>
    /// Bóc tách bảng số liệu giao hàng từ ảnh chụp phiếu kho đơn lẻ (Vision AI OCR)
    /// </summary>
    [HttpPost("extract")]
    public async Task<ActionResult<OcrExtractionResponseDto>> ExtractFromImage(
        [FromForm] IFormFile? file,
        [FromForm] IFormFile? image,
        CancellationToken cancellationToken)
    {
        var targetFile = file ?? image;

        if (targetFile == null || targetFile.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn hoặc dán file ảnh phiếu kho hợp lệ." });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(targetFile.FileName).ToLowerInvariant();

        var isImageContentType = targetFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var hasValidExtension = allowedExtensions.Contains(extension);

        if (!isImageContentType && !hasValidExtension)
        {
            return BadRequest(new { message = "Định dạng file không được hỗ trợ. Vui lòng tải lên ảnh PNG, JPG hoặc WEBP." });
        }

        if (targetFile.Length > MaxImageBytes)
        {
            return BadRequest(new { message = "Kích thước ảnh vượt quá giới hạn cho phép (20MB)." });
        }

        try
        {
            var mimeType = !string.IsNullOrWhiteSpace(targetFile.ContentType)
                ? targetFile.ContentType
                : (extension == ".png" ? "image/png" : "image/jpeg");

            using var stream = targetFile.OpenReadStream();
            var result = await _ocrService.ExtractFromImageAsync(stream, mimeType, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Chưa cấu hình API Key: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi từ OpenAI API: {Message}", ex.Message);
            return StatusCode(502, new { message = "Dịch vụ nhận dạng ảnh đang tạm thời không khả dụng. Vui lòng thử lại sau." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi trích xuất dữ liệu OCR từ ảnh phiếu kho.");
            return StatusCode(500, new { message = "Không thể bóc tách ảnh phiếu kho." });
        }
    }

    /// <summary>
    /// Bóc tách hàng loạt ảnh phiếu kho theo lô (Batch Upload / Multi-Scan OCR).
    /// Áp dụng Semaphore(3) để kiểm soát số lượng tiến trình song song, tránh nghẽn và vượt rate-limit AI.
    /// </summary>
    [HttpPost("batch-extract")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<List<BatchOcrScanResultDto>>> BatchExtract(
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ít nhất một ảnh phiếu kho." });
        }

        if (files.Count > MaxBatchFiles)
            return BadRequest(new { message = $"Mỗi lần chỉ được tải tối đa {MaxBatchFiles} ảnh." });

        if (files.Sum(f => f.Length) > MaxBatchBytes)
            return BadRequest(new { message = "Tổng dung lượng lô ảnh vượt quá giới hạn 100MB." });

        // Tải danh mục ProductMaster vào bộ nhớ để đối chiếu giá và thông số đóng thùng
        var masterRows = await _context.ProductMasters
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var productMasters = masterRows.GroupBy(p => p.StyleCode.Trim().ToUpperInvariant()).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());

        var results = new List<BatchOcrScanResultDto>();
        var semaphore = new SemaphoreSlim(1, 1); // Scoped EF context must not be used concurrently.

        var tasks = files.Select(async file =>
        {
            await semaphore.WaitAsync(cancellationToken);
            var batchResult = new BatchOcrScanResultDto
            {
                BatchId = Guid.NewGuid().ToString(),
                FileName = file.FileName,
                Title = Path.GetFileNameWithoutExtension(file.FileName)
            };

            try
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension) && !file.ContentType.StartsWith("image/"))
                {
                    batchResult.IsSuccess = false;
                    batchResult.ErrorMessage = "Định dạng file không được hỗ trợ (chỉ chấp nhận PNG, JPG, WEBP).";
                    return batchResult;
                }

                if (file.Length > MaxImageBytes)
                {
                    batchResult.IsSuccess = false;
                    batchResult.ErrorMessage = "Dung lượng ảnh vượt quá 20MB.";
                    return batchResult;
                }

                var mimeType = !string.IsNullOrWhiteSpace(file.ContentType)
                    ? file.ContentType
                    : (extension == ".png" ? "image/png" : "image/jpeg");

                using var stream = file.OpenReadStream();
                var ocrRes = await _ocrService.ExtractFromImageAsync(stream, mimeType, cancellationToken);

                batchResult.Title = !string.IsNullOrWhiteSpace(ocrRes.Title) ? ocrRes.Title : batchResult.Title;
                batchResult.ReportedTotal = ocrRes.ReportedTotal;

                // Chuẩn hóa và làm giàu dữ liệu từ Master Data
                var enrichedItems = new List<OcrItemDto>();
                foreach (var item in ocrRes.Items)
                {
                    string normalizedStyleCode = OcrExtractionService.NormalizeStyleCode(item.StyleCode);
                    string cleanCode = normalizedStyleCode.Trim().ToUpperInvariant();
                    if (cleanCode.EndsWith(".G"))
                    {
                        cleanCode = cleanCode[..^2].Trim();
                    }

                    productMasters.TryGetValue(cleanCode, out var pm);

                    enrichedItems.Add(new OcrItemDto
                    {
                        StyleCode = normalizedStyleCode,
                        Quantity = item.Quantity,
                        Note = item.Note ?? string.Empty,
                        ProcessType = item.ProcessType,
                        UnitPriceCMT = item.UnitPriceCMT > 0 ? item.UnitPriceCMT : (item.ProcessType == ProcessType.GoKhongMay ? (pm?.UnitPriceCMT_Go ?? pm?.UnitPriceCMT ?? 0) : (pm?.UnitPriceCMT ?? 0)),
                        UnitPriceDAP = item.UnitPriceDAP > 0 ? item.UnitPriceDAP : (item.ProcessType == ProcessType.GoKhongMay ? (pm?.UnitPriceDAP_Go ?? pm?.UnitPriceDAP ?? 0) : (pm?.UnitPriceDAP ?? 0)),
                        PairPerCarton = (pm?.PairPerCarton > 0) ? pm.PairPerCarton : (item.PairPerCarton > 0 ? item.PairPerCarton : 12),
                        Description = !string.IsNullOrWhiteSpace(item.Description) ? item.Description : (pm?.Description ?? string.Empty),
                        Unit = !string.IsNullOrWhiteSpace(pm?.Unit) ? pm.Unit : "đôi",
                        IsMatched = pm != null || item.IsMatched
                    });
                }

                batchResult.Items = enrichedItems;
                batchResult.CalculatedTotal = enrichedItems.Sum(i => i.Quantity);
                batchResult.HasStandardItems = enrichedItems.Any(i => i.ProcessType == ProcessType.Standard);
                batchResult.HasGoItems = enrichedItems.Any(i => i.ProcessType == ProcessType.GoKhongMay);
                batchResult.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi bóc tách ảnh {FileName} trong lô", file.FileName);
                batchResult.IsSuccess = false;
                batchResult.ErrorMessage = "Không thể nhận dạng ảnh này. Vui lòng kiểm tra ảnh hoặc thử lại sau.";
            }
            finally
            {
                semaphore.Release();
            }

            return batchResult;
        });

        var taskResults = await Task.WhenAll(tasks);
        results.AddRange(taskResults);

        return Ok(results);
    }

    /// <summary>
    /// Xác nhận xuất toàn bộ các đợt trong lô ra gói file Excel nén (.ZIP) duy nhất.
    /// Tự động sinh số hóa đơn tiếp theo liên tục, lưu toàn bộ đơn hàng vào CSDL với trạng thái Exported.
    /// </summary>
    [HttpPost("batch-export-zip")]
    public async Task<IActionResult> BatchExportZip([FromBody] BatchOcrConfirmRequestDto request)
    {
        if (request == null || request.Batches == null || request.Batches.Count == 0)
        {
            return BadRequest(new { message = "Dữ liệu yêu cầu xuất lô không hợp lệ." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Tải Master Data để tính toán giá và carton
        var productMasters = await _context.ProductMasters.Where(p => p.FolderId == request.ContractFolderId)
            .AsNoTracking()
            .ToDictionaryAsync(p => p.StyleCode.Trim().ToUpperInvariant(), p => p);

        if (request.Batches.Any(b => b.Items.Count == 0 || b.Items.Any(i => i.Quantity <= 0 ||
            !productMasters.ContainsKey(CustomsDeclarationService.NormalizeStyleCode(i.StyleCode)))))
            return BadRequest(new { message = "Lô hàng chứa số lượng không hợp lệ hoặc mã chưa có trong hợp đồng." });
        // 1. Tính toán tổng số lượng số thứ tự hóa đơn cần cấp phát
        int totalSeqNeeded = 0;
        foreach (var batch in request.Batches)
        {
            bool hasGo = batch.Items.Any(i => i.ProcessType == ProcessType.GoKhongMay);
            bool hasStd = batch.Items.Any(i => i.ProcessType == ProcessType.Standard);

            if (hasGo && hasStd)
            {
                totalSeqNeeded += 2; // Tách 2 hóa đơn
            }
            else if (hasGo || hasStd)
            {
                totalSeqNeeded += 1; // 1 hóa đơn
            }
        }

        if (totalSeqNeeded == 0)
        {
            return BadRequest(new { message = "Không tìm thấy mặt hàng nào để xuất hóa đơn." });
        }

        // 2. Lấy dãy số thứ tự liên tục từ SequenceService
        int[] seqNumbers;
        if (request.StartInvoiceNumber.HasValue && request.StartInvoiceNumber.Value > 0)
        {
            int startNum = request.StartInvoiceNumber.Value;
            seqNumbers = Enumerable.Range(startNum, totalSeqNeeded).ToArray();
            await _sequenceService.SetNextSequenceNumberAsync(startNum + totalSeqNeeded);
        }
        else
        {
            seqNumbers = await _sequenceService.GetNextSequenceNumbersAsync(totalSeqNeeded);
        }
        int seqCursor = 0;

        var summary = new BatchExportSummaryDto
        {
            TotalBatches = request.Batches.Count,
            TotalFiles = totalSeqNeeded,
            ZipFileName = $"KM3-Batch-OCR-Export-{DateTime.Now:yyyyMMddHHmmss}.zip"
        };

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            foreach (var batch in request.Batches)
            {
                var goItems = batch.Items.Where(i => i.ProcessType == ProcessType.GoKhongMay).ToList();
                var stdItems = batch.Items.Where(i => i.ProcessType == ProcessType.Standard).ToList();

                if (goItems.Count > 0 && stdItems.Count > 0)
                {
                    // === TÁCH 2 FILE CHO ĐỢT NÀY ===
                    int firstSeq = seqNumbers[seqCursor++];
                    int secondSeq = seqNumbers[seqCursor++];

                    int standardSeq, goSeq;
                    if (request.Priority == ExportSequencePriority.GoFirst)
                    {
                        goSeq = firstSeq;
                        standardSeq = secondSeq;
                    }
                    else
                    {
                        standardSeq = firstSeq;
                        goSeq = secondSeq;
                    }

                    string goInvoiceNo = _sequenceService.ToInvoiceNo(goSeq);
                    string stdInvoiceNo = _sequenceService.ToInvoiceNo(standardSeq);
                    string goFileName = _sequenceService.ToFileName(goSeq);
                    string stdFileName = _sequenceService.ToFileName(standardSeq);

                    var goReq = BuildShipmentRequest(request, goItems, goInvoiceNo);
                    var stdReq = BuildShipmentRequest(request, stdItems, stdInvoiceNo);

                    // Lưu DB
                    await SaveShipmentToDbAsync(goReq, productMasters);
                    await SaveShipmentToDbAsync(stdReq, productMasters);

                    // Sinh Excel và ghi vào ZIP
                    var goExcelBytes = await _excelService.ExportShipmentMultiSheetExcelAsync(goReq);
                    var goEntry = archive.CreateEntry(goFileName, CompressionLevel.Optimal);
                    using (var entryStream = goEntry.Open())
                    {
                        await entryStream.WriteAsync(goExcelBytes);
                    }

                    var stdExcelBytes = await _excelService.ExportShipmentMultiSheetExcelAsync(stdReq);
                    var stdEntry = archive.CreateEntry(stdFileName, CompressionLevel.Optimal);
                    using (var entryStream = stdEntry.Open())
                    {
                        await entryStream.WriteAsync(stdExcelBytes);
                    }

                    summary.ExportedShipments.Add(new BatchExportItemSummaryDto
                    {
                        BatchId = batch.BatchId,
                        Title = batch.Title,
                        InvoiceNo = goInvoiceNo,
                        FileName = goFileName,
                        TotalQuantity = goItems.Sum(i => i.Quantity),
                        ProcessType = ProcessType.GoKhongMay
                    });

                    summary.ExportedShipments.Add(new BatchExportItemSummaryDto
                    {
                        BatchId = batch.BatchId,
                        Title = batch.Title,
                        InvoiceNo = stdInvoiceNo,
                        FileName = stdFileName,
                        TotalQuantity = stdItems.Sum(i => i.Quantity),
                        ProcessType = ProcessType.Standard
                    });
                }
                else
                {
                    // === 1 FILE DUY NHẤT CHO ĐỢT NÀY ===
                    var validItems = goItems.Count > 0 ? goItems : stdItems;
                    if (validItems.Count == 0) continue;

                    int seq = seqNumbers[seqCursor++];
                    string invoiceNo = _sequenceService.ToInvoiceNo(seq);
                    string fileName = _sequenceService.ToFileName(seq);

                    var shipReq = BuildShipmentRequest(request, validItems, invoiceNo);

                    await SaveShipmentToDbAsync(shipReq, productMasters);

                    var excelBytes = await _excelService.ExportShipmentMultiSheetExcelAsync(shipReq);
                    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    using (var entryStream = entry.Open())
                    {
                        await entryStream.WriteAsync(excelBytes);
                    }

                    summary.ExportedShipments.Add(new BatchExportItemSummaryDto
                    {
                        BatchId = batch.BatchId,
                        Title = batch.Title,
                        InvoiceNo = invoiceNo,
                        FileName = fileName,
                        TotalQuantity = validItems.Sum(i => i.Quantity),
                        ProcessType = goItems.Count > 0 ? ProcessType.GoKhongMay : ProcessType.Standard
                    });
                }
            }
        }

        summary.TotalQuantity = summary.ExportedShipments.Sum(s => s.TotalQuantity);

        Response.Headers["X-Batch-Export-Info"] = JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Response.Headers["Access-Control-Expose-Headers"] = "X-Batch-Export-Info";

        await transaction.CommitAsync();
        return File(zipStream.ToArray(), "application/zip", summary.ZipFileName);
    }

    private CreateShipmentRequestDto BuildShipmentRequest(
        BatchOcrConfirmRequestDto batchReq,
        List<CreateShipmentItemDto> items,
        string invoiceNo)
    {
        return new CreateShipmentRequestDto
        {
            ContractFolderId = batchReq.ContractFolderId,
            InvoiceNo = invoiceNo,
            InvoiceDate = batchReq.InvoiceDate,
            PoSuffix = batchReq.PoSuffix ?? _options.DefaultPoSuffix,
            ContractNo = batchReq.ContractNo ?? _options.DefaultContractNo,
            CustomerName = batchReq.CustomerName ?? _options.DefaultCustomerName,
            Address = batchReq.Address ?? string.Empty,
            DeliveryTerms = batchReq.DeliveryTerms ?? "DAP",
            PaymentTerms = batchReq.PaymentTerms ?? "T/T",
            Items = items
        };
    }

    private async Task SaveShipmentToDbAsync(
        CreateShipmentRequestDto request,
        Dictionary<string, ProductMaster> productMasters)
    {
        var shipment = new ShipmentOrder
        {
            ContractFolderId = request.ContractFolderId,
            InvoiceNo = request.InvoiceNo.Trim(),
            InvoiceDate = request.InvoiceDate,
            PoSuffix = request.PoSuffix?.Trim(),
            ContractNo = request.ContractNo.Trim(),
            CustomerName = request.CustomerName.Trim(),
            Address = request.Address?.Trim(),
            DeliveryTerms = request.DeliveryTerms?.Trim() ?? "DAP",
            PaymentTerms = request.PaymentTerms?.Trim() ?? "T/T",
            CustomsDeclarationType = "E52",
            Status = ShipmentStatus.Exported,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            var key = item.StyleCode.Trim().ToUpperInvariant();
            productMasters.TryGetValue(key, out var pm);

            bool isGo = item.ProcessType == ProcessType.GoKhongMay;
            decimal cmt = isGo ? (pm?.UnitPriceCMT_Go ?? pm?.UnitPriceCMT ?? 0) : (pm?.UnitPriceCMT ?? 0);
            decimal dap = isGo ? (pm?.UnitPriceDAP_Go ?? pm?.UnitPriceDAP ?? 0) : (pm?.UnitPriceDAP ?? 0);

            shipment.Items.Add(new ShipmentOrderItem
            {
                StyleCode = CustomsDeclarationService.NormalizeStyleCode(item.StyleCode),
                Description = pm?.Description ?? string.Empty,
                Unit = pm?.Unit ?? "đôi",
                PairPerCarton = pm?.PairPerCarton ?? 12,
                FullItemCode = isGo ? $"{CustomsDeclarationService.NormalizeStyleCode(item.StyleCode)}.G {request.PoSuffix}".Trim() : $"{CustomsDeclarationService.NormalizeStyleCode(item.StyleCode)} {request.PoSuffix}".Trim(),
                Quantity = item.Quantity,
                ProcessType = item.ProcessType,
                UnitPriceCMT = cmt,
                UnitPriceDAP = dap
            });
        }

        _context.ShipmentOrders.Add(shipment);
        await _context.SaveChangesAsync();
    }
}
