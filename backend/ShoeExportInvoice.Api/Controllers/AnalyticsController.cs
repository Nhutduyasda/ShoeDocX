using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly ICustomsSettlementService _settlementService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        ICustomsSettlementService settlementService,
        ILogger<AnalyticsController> logger)
    {
        _settlementService = settlementService;
        _logger = logger;
    }

    /// <summary>
    /// Thống kê tổng sản lượng, tổng kim ngạch DAP, tổng doanh thu gia công CMT theo 12 tháng trong năm
    /// kèm phân tích luồng thông quan (Xanh/Vàng/Đỏ) và Top 5 mã giày xuất khẩu nhiều nhất.
    /// </summary>
    [HttpGet("export-stats")]
    public async Task<ActionResult<AnalyticsExportStatsDto>> GetExportStats([FromQuery] int? year)
    {
        int targetYear = year ?? DateTime.UtcNow.Year;
        try
        {
            var stats = await _settlementService.GetExportAnalyticsAsync(targetYear);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thống kê phân tích xuất khẩu năm {Year}", targetYear);
            return StatusCode(500, new
            {
                message = "Không thể lấy thống kê xuất khẩu."
            });
        }
    }
}
