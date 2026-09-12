using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Kho,Xnk")]
public class WarehouseController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<WarehouseController> _logger;

    public WarehouseController(AppDbContext context, ILogger<WarehouseController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách các lô hàng xuất kho (Warehouse Batches)
    /// Hỗ trợ lọc theo trạng thái, khoảng ngày xuất và tìm kiếm từ khóa
    /// </summary>
    [HttpGet("batches")]
    public async Task<ActionResult<List<WarehouseBatchSummaryDto>>> GetBatches(
        [FromQuery] WarehouseBatchStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null)
    {
        var query = _context.WarehouseBatches
            .Include(b => b.Items)
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            var f = fromDate.Value.Date;
            query = query.Where(b => b.ExportDate >= f);
        }

        if (toDate.HasValue)
        {
            var t = toDate.Value.Date.AddDays(1);
            query = query.Where(b => b.ExportDate < t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b => b.BatchName.ToLower().Contains(s) ||
                                     b.BatchNumber.ToLower().Contains(s) ||
                                     b.ContractNote.ToLower().Contains(s));
        }

        var batches = await query
            .OrderByDescending(b => b.ExportDate)
            .ThenByDescending(b => b.Id)
            .Select(b => new WarehouseBatchSummaryDto
            {
                Id = b.Id,
                BatchName = b.BatchName,
                BatchNumber = b.BatchNumber,
                ExportDate = b.ExportDate,
                ContractNote = b.ContractNote,
                Status = b.Status,
                TotalQuantity = b.TotalQuantity,
                ItemCount = b.Items.Count,
                GoCount = b.Items.Count(i => i.ProcessType == ProcessType.GoKhongMay),
                ThanhHinhCount = b.Items.Count(i => i.ProcessType == ProcessType.Standard),
                CreatedAt = b.CreatedAt,
                SubmittedAt = b.SubmittedAt
            })
            .ToListAsync();

        return Ok(batches);
    }

    /// <summary>
    /// Lấy chi tiết 1 lô xuất kho kèm toàn bộ danh sách mặt hàng
    /// </summary>
    [HttpGet("batches/{id:int}")]
    public async Task<ActionResult<WarehouseBatchDto>> GetBatchById(int id)
    {
        var batch = await _context.WarehouseBatches
            .Include(b => b.ContractFolder)
            .Include(b => b.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(new { message = $"Không tìm thấy lô xuất kho #{id}." });
        }

        var dto = new WarehouseBatchDto
        {
            Id = batch.Id,
            BatchName = batch.BatchName,
            BatchNumber = batch.BatchNumber,
            ExportDate = batch.ExportDate,
            ContractNote = batch.ContractNote,
            ContractFolderId = batch.ContractFolderId,
            ContractFolderName = batch.ContractFolder?.Name,
            Status = batch.Status,
            TotalQuantity = batch.TotalQuantity,
            ShipmentOrderId = batch.ShipmentOrderId,
            CreatedBy = batch.CreatedBy,
            CreatedAt = batch.CreatedAt,
            SubmittedAt = batch.SubmittedAt,
            Items = batch.Items
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new WarehouseBatchItemDto
                {
                    Id = i.Id,
                    StyleCode = i.StyleCode,
                    Quantity = i.Quantity,
                    ProcessType = i.ProcessType,
                    IsPendingReview = i.IsPendingReview,
                    DisplayOrder = i.DisplayOrder,
                    Note = i.Note
                })
                .ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// Lưu hoặc cập nhật lô hàng xuất kho (Bản nháp hoặc Bàn giao trực tiếp cho XNK)
    /// Tự động kiểm tra Master Data: Nếu mã chưa có, đánh dấu IsPendingReview=true mà KHÔNG chặn thủ kho.
    /// </summary>
    [HttpPost("batches")]
    public async Task<ActionResult<WarehouseBatchDto>> SaveBatch([FromBody] SaveWarehouseBatchRequestDto request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Lô hàng phải có ít nhất 1 dòng mã giày." });
        }

        // Lấy danh sách mã giày cần kiểm tra (bao gồm cả mã bỏ đuôi .G)
        var rawCodes = request.Items
            .Select(i => (i.StyleCode ?? string.Empty).Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lookupCodes = rawCodes
            .Select(c => c.EndsWith(".G", StringComparison.OrdinalIgnoreCase) ? c[..^2].Trim() : c)
            .Concat(rawCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Lấy các mã đã có trong ProductMaster kèm thông tin giá để đối chiếu và bảo toàn logic
        var masterProducts = await _context.ProductMasters
            .AsNoTracking()
            .Where(p => lookupCodes.Contains(p.StyleCode))
            .ToListAsync();

        var productMap = masterProducts
            .GroupBy(p => p.StyleCode.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Sinh tên lô nếu chưa có
        var batchNum = string.IsNullOrWhiteSpace(request.BatchNumber) ? "LẦN X" : request.BatchNumber.Trim();
        var contractNote = string.IsNullOrWhiteSpace(request.ContractNote) ? "XUẤT KHO" : request.ContractNote.Trim();
        var batchName = $"{batchNum} {request.ExportDate:dd/MM} {contractNote}".Trim();

        var username = User?.Identity?.Name ?? "Thủ kho";

        WarehouseBatch batch;
        if (request.Id.HasValue && request.Id.Value > 0)
        {
            var existing = await _context.WarehouseBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == request.Id.Value);

            if (existing == null)
            {
                return NotFound(new { message = $"Không tìm thấy lô #{request.Id.Value} để cập nhật." });
            }

            if (existing.Status == WarehouseBatchStatus.ProcessedByXnk)
            {
                return BadRequest(new { message = "Lô hàng này đã được XNK tiếp nhận xử lý, không thể chỉnh sửa." });
            }

            batch = existing;
            batch.BatchName = batchName;
            batch.BatchNumber = batchNum;
            batch.ExportDate = request.ExportDate;
            batch.ContractNote = contractNote;
            batch.ContractFolderId = request.ContractFolderId;

            // Xóa items cũ để thêm lại
            _context.WarehouseBatchItems.RemoveRange(batch.Items);
            batch.Items.Clear();
        }
        else
        {
            batch = new WarehouseBatch
            {
                BatchName = batchName,
                BatchNumber = batchNum,
                ExportDate = request.ExportDate,
                ContractNote = contractNote,
                ContractFolderId = request.ContractFolderId,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow,
                Status = WarehouseBatchStatus.Draft
            };
            _context.WarehouseBatches.Add(batch);
        }

        int totalQty = 0;
        int order = 1;
        foreach (var item in request.Items)
        {
            var styleCode = (item.StyleCode ?? string.Empty).Trim().ToUpper();
            if (string.IsNullOrEmpty(styleCode)) continue;

            var baseCode = styleCode.EndsWith(".G", StringComparison.OrdinalIgnoreCase)
                ? styleCode[..^2].Trim()
                : styleCode;

            productMap.TryGetValue(styleCode, out var matchedProduct);
            if (matchedProduct == null)
            {
                productMap.TryGetValue(baseCode, out matchedProduct);
            }

            var isKnown = matchedProduct != null;
            var isPendingReview = !isKnown || item.IsPendingReview;

            // Failsafe Auto-Correction:
            // 1. Có đuôi .G -> Chắc chắn là Gò không may
            // 2. Không có đuôi .G nhưng Master Data CHỈ có đơn giá Gò -> Tự động sửa về Gò không may
            // 3. Master Data CHỈ có đơn giá Thành hình -> Tự động sửa về Standard (Thành hình)
            var processType = item.ProcessType;
            if (styleCode.EndsWith(".G", StringComparison.OrdinalIgnoreCase))
            {
                processType = ProcessType.GoKhongMay;
            }
            else if (matchedProduct != null)
            {
                bool hasStandard = matchedProduct.UnitPriceCMT > 0 || matchedProduct.UnitPriceDAP > 0;
                bool hasGo = (matchedProduct.UnitPriceCMT_Go.HasValue && matchedProduct.UnitPriceCMT_Go.Value > 0) ||
                             (matchedProduct.UnitPriceDAP_Go.HasValue && matchedProduct.UnitPriceDAP_Go.Value > 0);

                if (!hasStandard && hasGo)
                {
                    processType = ProcessType.GoKhongMay;
                }
                else if (hasStandard && !hasGo)
                {
                    processType = ProcessType.Standard;
                }
            }

            var batchItem = new WarehouseBatchItem
            {
                StyleCode = styleCode,
                Quantity = item.Quantity,
                ProcessType = processType,
                IsPendingReview = isPendingReview,
                DisplayOrder = order++,
                Note = item.Note
            };

            totalQty += item.Quantity;
            batch.Items.Add(batchItem);
        }

        batch.TotalQuantity = totalQty;

        if (request.SubmitImmediately)
        {
            batch.Status = WarehouseBatchStatus.SubmittedToXnk;
            batch.SubmittedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Lô xuất kho #{BatchId} ({BatchName}) đã được lưu thành công bởi {User}. Trạng thái: {Status}",
            batch.Id, batch.BatchName, username, batch.Status);

        return await GetBatchById(batch.Id);
    }

    /// <summary>
    /// Bàn giao lô hàng cho bộ phận XNK
    /// </summary>
    [HttpPost("batches/{id:int}/submit")]
    public async Task<ActionResult<WarehouseBatchDto>> SubmitBatch(int id)
    {
        var batch = await _context.WarehouseBatches
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(new { message = $"Không tìm thấy lô #{id}." });
        }

        if (batch.Items.Count == 0 || batch.TotalQuantity == 0)
        {
            return BadRequest(new { message = "Lô hàng chưa có dữ liệu mặt hàng hợp lệ để bàn giao." });
        }

        batch.Status = WarehouseBatchStatus.SubmittedToXnk;
        batch.SubmittedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Lô #{BatchId} ({BatchName}) đã được bàn giao cho XNK lúc {Time}",
            batch.Id, batch.BatchName, batch.SubmittedAt);

        return await GetBatchById(batch.Id);
    }

    /// <summary>
    /// Xóa lô hàng xuất kho (chỉ áp dụng cho bản nháp Draft)
    /// </summary>
    [HttpDelete("batches/{id:int}")]
    public async Task<IActionResult> DeleteBatch(int id)
    {
        var batch = await _context.WarehouseBatches.FirstOrDefaultAsync(b => b.Id == id);
        if (batch == null)
        {
            return NotFound(new { message = $"Không tìm thấy lô #{id}." });
        }

        if (batch.Status == WarehouseBatchStatus.ProcessedByXnk)
        {
            return BadRequest(new { message = "Không thể xóa lô hàng đã được XNK tiếp nhận và lên hóa đơn." });
        }

        _context.WarehouseBatches.Remove(batch);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = $"Đã xóa lô hàng #{id} thành công." });
    }

    /// <summary>
    /// Tìm kiếm nhanh mã giày cho thủ kho khi đang nhập phím
    /// Trả về StyleCode, Customer, ModelName để hiển thị gợi ý
    /// </summary>
    [HttpGet("product-lookup")]
    public async Task<ActionResult<List<object>>> LookupProducts(
        [FromQuery] string? q = null,
        [FromQuery] int? folderId = null)
    {
        var query = _context.ProductMasters.AsNoTracking();

        if (folderId.HasValue)
        {
            query = query.Where(p => p.FolderId == folderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim().ToUpper();
            query = query.Where(p => p.StyleCode.ToUpper().Contains(keyword) || (p.Description != null && p.Description.ToUpper().Contains(keyword)));
        }

        var products = await query
            .OrderBy(p => p.StyleCode)
            .Take(folderId.HasValue ? 500 : 50)
            .Select(p => new
            {
                p.Id,
                p.StyleCode,
                Description = p.Description,
                Customer = p.Folder != null ? p.Folder.CustomerName : null,
                p.FolderId,
                FolderName = p.Folder != null ? p.Folder.Name : null,
                UnitPriceCMT = p.UnitPriceCMT,
                UnitPriceDAP = p.UnitPriceDAP,
                UnitPriceCMT_Go = p.UnitPriceCMT_Go,
                UnitPriceDAP_Go = p.UnitPriceDAP_Go,
                UnitPriceGoKhongMay = p.UnitPriceCMT_Go,
                HasStandardPrice = p.UnitPriceCMT > 0 || p.UnitPriceDAP > 0,
                HasGoPrice = (p.UnitPriceCMT_Go.HasValue && p.UnitPriceCMT_Go.Value > 0) || (p.UnitPriceDAP_Go.HasValue && p.UnitPriceDAP_Go.Value > 0),
                PairPerCarton = p.PairPerCarton,
                Unit = p.Unit
            })
            .ToListAsync();

        return Ok(products);
    }

    /// <summary>
    /// Đánh dấu lô hàng đã được XNK tiếp nhận và liên kết mã hóa đơn ShipmentOrderId
    /// </summary>
    [HttpPost("batches/{id:int}/mark-processed")]
    public async Task<IActionResult> MarkProcessed(int id, [FromQuery] int shipmentOrderId)
    {
        var batch = await _context.WarehouseBatches.FirstOrDefaultAsync(b => b.Id == id);
        if (batch == null)
        {
            return NotFound(new { message = $"Không tìm thấy lô #{id}." });
        }

        batch.Status = WarehouseBatchStatus.ProcessedByXnk;
        batch.ShipmentOrderId = shipmentOrderId;

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = $"Lô #{id} đã được đánh dấu hoàn tất xử lý." });
    }
}
