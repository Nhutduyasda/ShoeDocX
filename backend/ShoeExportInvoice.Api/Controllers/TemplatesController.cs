using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Services;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        ITemplateService templateService,
        ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các mẫu phôi template Excel
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<CompanyTemplateDto>>> GetAll()
    {
        var templates = await _templateService.GetAllTemplatesAsync();
        return Ok(templates);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một mẫu phôi template theo ID
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<CompanyTemplateDto>> GetById(int id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
        {
            return NotFound(new { message = $"Không tìm thấy template có ID = {id}" });
        }
        return Ok(template);
    }

    /// <summary>
    /// Lấy mẫu phôi template mặc định hệ thống
    /// </summary>
    [HttpGet("default")]
    [AllowAnonymous]
    public async Task<ActionResult<CompanyTemplateDto>> GetDefault()
    {
        try
        {
            var template = await _templateService.GetDefaultTemplateAsync();
            return Ok(template);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Tải lên file phôi .xlsx và cấu hình tọa độ cho template mới (BYOT)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Xnk")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CompanyTemplateDto>> Create([FromForm] CreateCompanyTemplateForm form)
    {
        if (form == null || form.File == null || form.File.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn file Excel .xlsx phôi mẫu." });
        }

        if (string.IsNullOrWhiteSpace(form.Name))
        {
            return BadRequest(new { message = "Tên mẫu phôi không được để trống." });
        }

        if (string.IsNullOrWhiteSpace(form.ConfigJson))
        {
            return BadRequest(new { message = "Cấu hình tọa độ (configJson) không được để trống." });
        }

        try
        {
            var created = await _templateService.CreateTemplateAsync(form.File, form.Name, form.ConfigJson, form.Description, form.FolderId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra khi tạo template mới");
            return StatusCode(500, new { message = "Lỗi khi lưu trữ template.", details = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật cấu hình JSON tọa độ cho template
    /// </summary>
    [HttpPut("{id:int}/config")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> UpdateConfig(int id, [FromBody] JsonElement body)
    {
        string rawConfig;
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("configJson", out var configProp))
        {
            rawConfig = configProp.ValueKind == JsonValueKind.String ? configProp.GetString()! : configProp.GetRawText();
        }
        else if (body.ValueKind == JsonValueKind.String)
        {
            rawConfig = body.GetString()!;
        }
        else
        {
            rawConfig = body.GetRawText();
        }

        if (string.IsNullOrWhiteSpace(rawConfig))
        {
            return BadRequest(new { message = "ConfigJson không được để trống." });
        }

        try
        {
            var success = await _templateService.UpdateTemplateConfigAsync(id, rawConfig);
            if (!success)
            {
                return NotFound(new { message = $"Không tìm thấy template có ID = {id}" });
            }

            return Ok(new { message = "Cập nhật cấu hình template thành công." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cập nhật cấu hình template ID: {Id}", id);
            return StatusCode(500, new { message = "Lỗi khi cập nhật cấu hình template." });
        }
    }

    /// <summary>
    /// Đặt template làm mẫu mặc định của hệ thống
    /// </summary>
    [HttpPatch("{id:int}/set-default")]
    [Authorize(Roles = "Admin,Xnk")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var success = await _templateService.SetDefaultTemplateAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Không tìm thấy template có ID = {id}" });
        }

        return Ok(new { message = $"Đã đặt template ID {id} làm mẫu mặc định." });
    }
}
