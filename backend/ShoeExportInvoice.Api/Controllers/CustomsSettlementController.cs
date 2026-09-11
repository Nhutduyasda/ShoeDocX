using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/customs-settlement")]
public class CustomsSettlementController : ControllerBase
{
    private readonly ICustomsSettlementService _settlementService;
    private readonly ILogger<CustomsSettlementController> _logger;

    public CustomsSettlementController(
        ICustomsSettlementService settlementService,
        ILogger<CustomsSettlementController> logger)
    {
        _settlementService = settlementService;
        _logger = logger;
    }

    /// <summary>
    /// Tổng hợp số liệu quyết toán Mẫu 16 từ các đơn hàng E52 đã thông quan trong kỳ.
    /// </summary>
    [HttpPost("calculate")]
    [HttpPost("preview")]
    public async Task<ActionResult<SettlementReportDto>> CalculateSettlement(
        [FromBody] CalculateSettlementRequestDto request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        if (request.FromDate > request.ToDate)
        {
            return BadRequest(new { message = "Từ ngày không thể lớn hơn Đến ngày." });
        }

        try
        {
            var report = await _settlementService.CalculateSettlementAsync(request);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tổng hợp báo cáo quyết toán");
            return StatusCode(500, new
            {
                message = "Không thể tổng hợp báo cáo quyết toán.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Lưu kỳ báo cáo quyết toán Mẫu 16 vào hệ thống.
    /// </summary>
    [HttpPost("save")]
    public async Task<ActionResult<CustomsSettlementPeriod>> SaveSettlementPeriod(
        [FromBody] SaveSettlementPeriodRequestDto request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        try
        {
            var period = await _settlementService.SaveSettlementPeriodAsync(request);
            return Ok(period);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu kỳ báo cáo quyết toán năm {Year}", request?.Year);
            return StatusCode(500, new
            {
                message = "Không thể lưu kỳ báo cáo quyết toán.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Lấy danh sách các kỳ quyết toán đã lưu trong hệ thống.
    /// </summary>
    [HttpGet]
    [HttpGet("periods")]
    public async Task<ActionResult<List<SettlementPeriodSummaryDto>>> GetSettlementPeriods()
    {
        try
        {
            var list = await _settlementService.GetSettlementPeriodsAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách kỳ quyết toán");
            return StatusCode(500, new
            {
                message = "Không thể tải danh sách kỳ quyết toán.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Lấy chi tiết kỳ quyết toán theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [HttpGet("periods/{id}")]
    public async Task<ActionResult<SettlementReportDto>> GetSettlementPeriodById(int id)
    {
        try
        {
            var detail = await _settlementService.GetSettlementPeriodByIdAsync(id);
            if (detail == null)
            {
                return NotFound(new { message = $"Không tìm thấy kỳ quyết toán ID {id}" });
            }

            return Ok(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy chi tiết kỳ quyết toán ID {Id}", id);
            return StatusCode(500, new
            {
                message = "Không thể tải chi tiết kỳ quyết toán.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Xuất file Excel Báo cáo Quyết toán chuẩn Mẫu 16/BCQT-SP-GSQL (Thông tư 39/2018/TT-BTC).
    /// </summary>
    [HttpPost("export-excel")]
    public async Task<IActionResult> ExportSettlementExcel(
        [FromBody] SettlementReportDto report)
    {
        if (report == null || report.Items == null)
        {
            return BadRequest(new { message = "Dữ liệu báo cáo không hợp lệ để xuất Excel." });
        }

        try
        {
            var excelBytes = await _settlementService.ExportSettlementExcelAsync(report);
            var periodDesc = $"{report.FromDate:yyyyMMdd}_{report.ToDate:yyyyMMdd}";
            var fileName = $"Mau16_BCQT_SP_GSQL_{periodDesc}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xuất file Excel Mẫu 16");
            return StatusCode(500, new
            {
                message = "Không thể xuất file Excel Mẫu 16.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Xuất file Excel Báo cáo Quyết toán Mẫu 16 theo ID kỳ đã lưu.
    /// </summary>
    [HttpGet("export-excel/{id}")]
    public async Task<IActionResult> ExportSettlementExcelById(int id)
    {
        try
        {
            var excelBytes = await _settlementService.ExportSettlementExcelByIdAsync(id);
            var fileName = $"Mau16_BCQT_SP_GSQL_Ky_{id}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xuất file Excel Mẫu 16 ID {Id}", id);
            return StatusCode(500, new
            {
                message = "Không thể xuất file Excel Mẫu 16.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Trả về danh sách chi tiết các tờ khai tạo nên số lượng của mã sản phẩm (Drill-down)
    /// </summary>
    [HttpGet("drilldown")]
    public async Task<ActionResult<List<SettlementDrillDownItemDto>>> GetDrillDown(
        [FromQuery] string productCode,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? contractNo = null)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            return BadRequest(new { message = "Mã sản phẩm không được để trống." });
        }

        try
        {
            var list = await _settlementService.GetDrillDownAsync(productCode, from, to, contractNo);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi drill-down mã sản phẩm {ProductCode}", productCode);
            return StatusCode(500, new
            {
                message = "Không thể tải chi tiết tờ khai của mã sản phẩm.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Nạp file Excel số liệu kho (Tồn đầu, Nhập sản xuất) và tự động đối soát với danh sách xuất khẩu.
    /// </summary>
    [HttpPost("import-warehouse-data")]
    public async Task<ActionResult<WarehouseImportResultDto>> ImportWarehouseData(
        IFormFile file,
        [FromForm] string? currentItems)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file Excel để nạp dữ liệu kho." });
        }

        try
        {
            var itemsList = new List<SettlementItemDto>();
            if (!string.IsNullOrWhiteSpace(currentItems))
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                itemsList = System.Text.Json.JsonSerializer.Deserialize<List<SettlementItemDto>>(currentItems, options) ?? new List<SettlementItemDto>();
            }

            using var stream = file.OpenReadStream();
            var result = await _settlementService.ImportWarehouseExcelAsync(stream, itemsList);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi nạp file số liệu kho Excel");
            return StatusCode(500, new
            {
                message = "Không thể đọc dữ liệu từ file Excel.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Đối soát và khớp dữ liệu từ clipboard hoặc danh sách dòng nhập kho với danh sách xuất khẩu.
    /// </summary>
    [HttpPost("match-warehouse-data")]
    public async Task<ActionResult<WarehouseImportResultDto>> MatchWarehouseData(
        [FromBody] MatchWarehouseDataRequestDto request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        try
        {
            var result = await _settlementService.MatchWarehouseRowsAsync(request.Rows ?? new List<WarehouseDataRowDto>(), request.CurrentItems ?? new List<SettlementItemDto>());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đối soát số liệu kho");
            return StatusCode(500, new
            {
                message = "Không thể đối soát dữ liệu kho.",
                detail = ex.Message
            });
        }
    }
}
