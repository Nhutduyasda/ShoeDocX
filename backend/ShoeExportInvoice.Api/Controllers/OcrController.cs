using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly IOcrExtractionService _ocrService;
    private readonly ILogger<OcrController> _logger;

    public OcrController(
        IOcrExtractionService ocrService,
        ILogger<OcrController> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    /// <summary>
    /// Bóc tách bảng số liệu giao hàng từ ảnh chụp phiếu kho (Vision AI OCR)
    /// Hỗ trợ định dạng: .jpg, .jpeg, .png, .webp, dung lượng tối đa 20MB
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

        if (targetFile.Length > 20 * 1024 * 1024)
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
            return StatusCode(502, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi trích xuất dữ liệu OCR từ ảnh phiếu kho.");
            return StatusCode(500, new { message = $"Lỗi khi bóc tách ảnh: {ex.Message}", detail = ex.Message });
        }
    }
}
