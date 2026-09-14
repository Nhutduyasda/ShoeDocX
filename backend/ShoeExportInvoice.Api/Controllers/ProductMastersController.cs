using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/product-masters")]
[Authorize]
public class ProductMastersController : ControllerBase
{
    private readonly IProductMasterService _productService;
    private readonly IExcelImportExportService _excelService;
    private readonly ILogger<ProductMastersController> _logger;
    private readonly IBusinessAuditService? _audit;

    public ProductMastersController(
        IProductMasterService productService,
        IExcelImportExportService excelService,
        ILogger<ProductMastersController> logger, IBusinessAuditService? audit = null)
    {
        _productService = productService;
        _excelService = excelService;
        _logger = logger;
        _audit = audit;
    }

    /// <summary>
    /// Lấy danh sách sản phẩm phân trang và tìm kiếm theo mã hoặc mô tả
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Xnk,Kho,KeToan")]
    public async Task<ActionResult<PagedResultDto<ProductMasterDto>>> GetPaged(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? folderId = null)
    {
        var result = await _productService.GetPagedAsync(search, page, pageSize, folderId);
        return Ok(result);
    }

    /// <summary>
    /// Lấy toàn bộ danh sách sản phẩm (cho việc chọn mã hàng nhanh)
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin,Xnk,Kho,KeToan")]
    public async Task<ActionResult<IEnumerable<ProductMasterDto>>> GetAll()
    {
        var list = await _productService.GetAllAsync();
        return Ok(list);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một mã sản phẩm theo ID
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Xnk,Kho,KeToan")]
    public async Task<ActionResult<ProductMasterDto>> GetById(int id)
    {
        var item = await _productService.GetByIdAsync(id);
        if (item == null)
        {
            return NotFound(new { message = $"Không tìm thấy sản phẩm có ID = {id}" });
        }
        return Ok(item);
    }

    /// <summary>
    /// Thêm mới một mã sản phẩm gốc
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<ActionResult<ProductMasterDto>> Create([FromBody] CreateProductMasterDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _productService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo mới sản phẩm");
            return StatusCode(500, new { message = "Không thể tạo sản phẩm.", traceId = HttpContext.TraceIdentifier });
        }
    }

    /// <summary>
    /// Cập nhật thông tin mã sản phẩm
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<ActionResult<ProductMasterDto>> Update(int id, [FromBody] UpdateProductMasterDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updated = await _productService.UpdateAsync(id, dto);
            if (updated == null)
            {
                return NotFound(new { message = $"Không tìm thấy sản phẩm có ID = {id}" });
            }
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cập nhật sản phẩm");
            return StatusCode(500, new { message = "Không thể cập nhật sản phẩm.", traceId = HttpContext.TraceIdentifier });
        }
    }

    /// <summary>
    /// Xóa mã sản phẩm khỏi hệ thống
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _productService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Không tìm thấy sản phẩm có ID = {id}" });
        }
        return NoContent();
    }

    /// <summary>
    /// Xóa toàn bộ sản phẩm trong một thư mục (hoặc toàn bộ danh mục nếu không truyền folderId)
    /// </summary>
    [HttpDelete("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAll([FromQuery] int? folderId = null)
    {
        if (!folderId.HasValue || folderId.Value <= 0)
            return BadRequest(new { message = "folderId hợp lệ là bắt buộc; không hỗ trợ xóa toàn bộ hệ thống." });
        var count = await _productService.DeleteAllAsync(folderId);
        _audit?.Add(HttpContext, "ProductMaster.BulkDelete", "MasterDataFolder", folderId.Value,
            previous: new { DeletedCount = count });
        if (_audit != null)
            await HttpContext.RequestServices.GetRequiredService<ShoeExportInvoice.Api.Data.AppDbContext>().SaveChangesAsync();
        var msg = folderId.HasValue
            ? $"Đã xóa thành công {count} sản phẩm trong thư mục được chọn."
            : $"Đã xóa thành công toàn bộ {count} sản phẩm trong danh mục.";
        return Ok(new { message = msg, deletedCount = count });
    }

    /// <summary>
    /// Cập nhật ĐVT (Đơn vị tính) đồng loạt cho toàn bộ danh mục sản phẩm
    /// </summary>
    [HttpPut("bulk-update-unit")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> BulkUpdateUnit([FromBody] BulkUpdateUnitDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Unit))
        {
            return BadRequest(new { message = "Đơn vị tính không được để trống." });
        }

        var count = await _productService.BulkUpdateUnitAsync(dto.Unit);
        _audit?.Add(HttpContext, "ProductMaster.BulkUpdateUnit", "ProductMaster", "all", next: new { dto.Unit, UpdatedCount = count });
        if (_audit != null) await HttpContext.RequestServices.GetRequiredService<ShoeExportInvoice.Api.Data.AppDbContext>().SaveChangesAsync();
        return Ok(new { message = $"Đã cập nhật ĐVT thành '{dto.Unit}' cho toàn bộ {count} sản phẩm.", updatedCount = count });
    }

    /// <summary>
    /// Tải file Excel mẫu để chuẩn bị dữ liệu import
    /// </summary>
    [HttpGet("template")]
    [Authorize(Roles = "Admin,Xnk")]
    public IActionResult DownloadTemplate()
    {
        var fileBytes = _excelService.GenerateProductMasterTemplate();
        var fileName = $"ProductMaster_Template_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Xuất hóa đơn Commercial Invoice (INV) và Packing List (PKL) ra file Excel trực tiếp từ file mẫu chuẩn công ty
    /// </summary>
    [HttpGet("export")]
    [Authorize(Roles = "Admin,Xnk,Kho,KeToan")]
    public async Task<IActionResult> ExportExcel()
    {
        var fileBytes = await _excelService.ExportProductMastersToExcelAsync();
        var fileName = $"Commercial_Invoice_PKL_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Xuất Commercial Invoice (INV) và Packing List (PKL) theo dữ liệu lô hàng cụ thể
    /// </summary>
    [NonAction]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> ExportShipment([FromBody] ShipmentExportModel model)
    {
        var fileBytes = await _excelService.ExportShipmentToExcelAsync(model);
        var fileName = $"INV_PKL_{model.InvoiceNo}_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Di chuyển danh sách sản phẩm sang thư mục khác
    /// </summary>
    [HttpPost("bulk-move")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> BulkMove([FromBody] BulkMoveProductsDto dto)
    {
        var success = await _productService.BulkMoveProductsAsync(dto.ProductIds, dto.TargetFolderId);
        return Ok(new { success, message = $"Đã chuyển {dto.ProductIds.Count} sản phẩm sang thư mục mới." });
    }

    /// <summary>
    /// Xem trước cấu trúc và tự động nhận diện cột của file Excel Master Data
    /// </summary>
    [HttpPost("preview-import")]
    [HttpPost("/api/master-data/preview-import")]
    [Authorize(Roles = "Admin,Xnk")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportPreviewResponseDto>> PreviewImport(
        IFormFile? file,
        [FromQuery] int? folderId = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file Excel để tải lên." });
        }

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext != ".xlsx")
        {
            return BadRequest(new { message = "Chỉ chấp nhận định dạng file Excel (.xlsx)." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var preview = await _excelService.PreviewProductMastersFromExcelAsync(stream, folderId);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi phân tích file Excel xem trước: {FileName}", file.FileName);
            return StatusCode(500, new { message = "Không thể phân tích file Excel. Vui lòng kiểm tra định dạng." });
        }
    }

    /// <summary>
    /// Import danh mục sản phẩm từ file Excel (.xlsx) với tùy chọn ghi đè cột ánh xạ
    /// </summary>
    [HttpPost("import")]
    [HttpPost("/api/master-data/import-excel")]
    [Authorize(Roles = "Admin,Xnk")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportResultDto>> ImportExcel(
        IFormFile? file,
        [FromQuery] bool updateExisting = true,
        [FromQuery] int? folderId = null,
        [FromQuery] int? styleCodeCol = null,
        [FromQuery] int? cmtCol = null,
        [FromQuery] int? dapCol = null,
        [FromQuery] int? descCol = null,
        [FromQuery] int? hsCol = null,
        [FromQuery] int? unitCol = null,
        [FromQuery] int? pairCol = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file Excel để tải lên." });
        }

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext != ".xlsx")
        {
            return BadRequest(new { message = "Chỉ chấp nhận định dạng file Excel (.xlsx)." });
        }

        try
        {
            ColumnMappingOverrideDto? mappingOverride = null;
            if (styleCodeCol.HasValue || cmtCol.HasValue || dapCol.HasValue || descCol.HasValue || hsCol.HasValue || unitCol.HasValue || pairCol.HasValue)
            {
                mappingOverride = new ColumnMappingOverrideDto
                {
                    StyleCodeCol = styleCodeCol,
                    CmtPriceCol = cmtCol,
                    DapPriceCol = dapCol,
                    DescriptionCol = descCol,
                    HsCodeCol = hsCol,
                    UnitCol = unitCol,
                    PairsPerCartonCol = pairCol
                };
            }

            using var stream = file.OpenReadStream();
            var result = await _excelService.ImportProductMastersFromExcelAsync(stream, updateExisting, folderId, mappingOverride);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi import file Excel: {FileName}", file.FileName);
            return StatusCode(500, new { message = "Không thể xử lý file Excel.", traceId = HttpContext.TraceIdentifier });
        }
    }

    /// <summary>
    /// Tra cứu & kiểm tra chéo mã hàng trên toàn bộ Master Data để phát hiện nhầm lẫn đối tác (Partner Mismatch Detection)
    /// </summary>
    [HttpPost("validate-items")]
    [HttpPost("/api/master-data/validate-items")]
    [Authorize(Roles = "Admin,Xnk,Kho,KeToan")]
    public async Task<ActionResult<ValidateItemsResult>> ValidateItems([FromBody] ValidateItemsRequest request)
    {
        try
        {
            var result = await _productService.ValidateItemsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra chéo mã hàng Master Data");
            return StatusCode(500, new { message = "Không thể kiểm tra mã hàng.", traceId = HttpContext.TraceIdentifier });
        }
    }
}
