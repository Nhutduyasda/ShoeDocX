using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomsController : ControllerBase
{
    private readonly ICustomsDeclarationService _customsService;
    private readonly ILogger<CustomsController> _logger;

    public CustomsController(
        ICustomsDeclarationService customsService,
        ILogger<CustomsController> logger)
    {
        _customsService = customsService;
        _logger = logger;
    }

    /// <summary>
    /// Nhận file .xls / .xlsx kết xuất từ phần mềm VNACCS, tự động bóc tách và đối soát chéo với đơn hàng trong CSDL.
    /// Không lưu vào database ở bước này.
    /// </summary>
    [HttpPost("parse-and-compare")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CustomsReconciliationResultDto>> ParseAndCompare(
        [FromForm] IFormFile file,
        [FromQuery] int? orderId = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file tờ khai hải quan (.xls hoặc .xlsx)." });
        }

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xls" && ext != ".xlsx")
        {
            return BadRequest(new { message = "Định dạng file không được hỗ trợ. Vui lòng tải file Excel kết xuất từ VNACCS (.xls hoặc .xlsx)." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var parsedDeclaration = _customsService.ParseDeclarationFile(stream, file.FileName);

            var reconciliation = await _customsService.ReconcileAsync(parsedDeclaration, orderId);
            return Ok(reconciliation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi bóc tách và đối soát file tờ khai: {FileName}", file.FileName);
            return StatusCode(500, new
            {
                message = "Không thể bóc tách file tờ khai hải quan. Vui lòng kiểm tra định dạng file kết xuất từ VNACCS.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Xác nhận đồng bộ dữ liệu hải quan vào đơn hàng và lưu trữ file tờ khai thực tế vào server.
    /// Cập nhật trạng thái đơn hàng sang Cleared (Đã thông quan) hoặc Discrepancy (Sai lệch).
    /// </summary>
    [HttpPost("confirm-sync/{orderId:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ConfirmSync(
        int orderId,
        [FromForm] ConfirmCustomsSyncRequestDto request,
        [FromForm] IFormFile? file = null)
    {
        try
        {
            Stream? fileStream = null;
            string? fileName = null;

            if (file != null && file.Length > 0)
            {
                fileStream = file.OpenReadStream();
                fileName = file.FileName;
            }

            var updatedOrder = await _customsService.ConfirmSyncAsync(orderId, request, fileStream, fileName);

            return Ok(new
            {
                message = request.IsFullyMatched
                    ? "Đã đồng bộ hồ sơ hải quan và thông quan đơn hàng thành công."
                    : "Đã cập nhật thông tin tờ khai. Đơn hàng được ghi nhận có sai lệch số liệu so với tờ khai hải quan.",
                orderId = updatedOrder.Id,
                invoiceNo = updatedOrder.InvoiceNo,
                declarationNo = updatedOrder.DeclarationNo,
                status = updatedOrder.Status,
                statusName = updatedOrder.Status.ToString()
            });
        }
        catch (KeyNotFoundException knf)
        {
            return NotFound(new { message = knf.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xác nhận đồng bộ hải quan cho đơn hàng #{OrderId}", orderId);
            return StatusCode(500, new
            {
                message = "Lỗi khi lưu thông tin hải quan vào đơn hàng.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Tải xuống file tờ khai gốc đã đính kèm theo đơn hàng
    /// </summary>
    [HttpGet("download/{orderId:int}")]
    public async Task<IActionResult> DownloadAttachment(int orderId)
    {
        try
        {
            var attachment = await _customsService.GetAttachmentAsync(orderId);
            if (attachment == null)
            {
                return NotFound(new { message = "Đơn hàng chưa có file tờ khai hải quan đính kèm hoặc file không tồn tại trên server." });
            }

            return File(attachment.Value.Bytes, attachment.Value.ContentType, attachment.Value.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải file tờ khai đính kèm đơn hàng #{OrderId}", orderId);
            return StatusCode(500, new { message = "Lỗi khi tải file tờ khai.", detail = ex.Message });
        }
    }
}
