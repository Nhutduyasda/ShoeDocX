using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/product-masters")]
public class ProductMastersController : ControllerBase
{
    private readonly IProductMasterService _productService;
    private readonly IExcelImportExportService _excelService;
    private readonly ILogger<ProductMastersController> _logger;

    public ProductMastersController(
        IProductMasterService productService,
        IExcelImportExportService excelService,
        ILogger<ProductMastersController> logger)
    {
        _productService = productService;
        _excelService = excelService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách sản phẩm phân trang và tìm kiếm theo mã hoặc mô tả
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ProductMasterDto>>> GetPaged(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _productService.GetPagedAsync(search, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Lấy toàn bộ danh sách sản phẩm (cho việc chọn mã hàng nhanh)
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<ProductMasterDto>>> GetAll()
    {
        var list = await _productService.GetAllAsync();
        return Ok(list);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một mã sản phẩm theo ID
    /// </summary>
    [HttpGet("{id:int}")]
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
            return StatusCode(500, new { message = "Lỗi hệ thống khi tạo sản phẩm: " + ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật thông tin mã sản phẩm
    /// </summary>
    [HttpPut("{id:int}")]
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
            return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật sản phẩm: " + ex.Message });
        }
    }

    /// <summary>
    /// Xóa mã sản phẩm khỏi hệ thống
    /// </summary>
    [HttpDelete("{id:int}")]
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
    /// Xóa toàn bộ danh mục sản phẩm trong Master Data
    /// </summary>
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAll()
    {
        var count = await _productService.DeleteAllAsync();
        return Ok(new { message = $"Đã xóa thành công toàn bộ {count} sản phẩm trong danh mục.", deletedCount = count });
    }

    /// <summary>
    /// Cập nhật ĐVT (Đơn vị tính) đồng loạt cho toàn bộ danh mục sản phẩm
    /// </summary>
    [HttpPut("bulk-update-unit")]
    public async Task<IActionResult> BulkUpdateUnit([FromBody] BulkUpdateUnitDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Unit))
        {
            return BadRequest(new { message = "Đơn vị tính không được để trống." });
        }

        var count = await _productService.BulkUpdateUnitAsync(dto.Unit);
        return Ok(new { message = $"Đã cập nhật ĐVT thành '{dto.Unit}' cho toàn bộ {count} sản phẩm.", updatedCount = count });
    }

    /// <summary>
    /// Tải file Excel mẫu để chuẩn bị dữ liệu import
    /// </summary>
    [HttpGet("template")]
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
    public async Task<IActionResult> ExportExcel()
    {
        var fileBytes = await _excelService.ExportProductMastersToExcelAsync();
        var fileName = $"Commercial_Invoice_PKL_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Xuất Commercial Invoice (INV) và Packing List (PKL) theo dữ liệu lô hàng cụ thể
    /// </summary>
    [HttpPost("export-shipment")]
    public async Task<IActionResult> ExportShipment([FromBody] ShipmentExportModel model)
    {
        var fileBytes = await _excelService.ExportShipmentToExcelAsync(model);
        var fileName = $"INV_PKL_{model.InvoiceNo}_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Import danh mục sản phẩm từ file Excel (.xlsx)
    /// </summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImportResultDto>> ImportExcel(
        IFormFile? file,
        [FromQuery] bool updateExisting = true)
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
            var result = await _excelService.ImportProductMastersFromExcelAsync(stream, updateExisting);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi import file Excel: {FileName}", file.FileName);
            return StatusCode(500, new { message = "Không thể xử lý file Excel: " + ex.Message });
        }
    }
}
