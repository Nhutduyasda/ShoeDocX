using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Models.Templates;

namespace ShoeExportInvoice.Api.Services;

public class TemplateService : ITemplateService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TemplateService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public TemplateService(
        AppDbContext context,
        IWebHostEnvironment environment,
        ILogger<TemplateService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task<List<CompanyTemplateDto>> GetAllTemplatesAsync()
    {
        var templates = await _context.CompanyTemplates
            .Include(t => t.Folder)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync();

        return templates.Select(MapToDto).ToList();
    }

    public async Task<CompanyTemplateDto?> GetTemplateByIdAsync(int id)
    {
        var template = await _context.CompanyTemplates
            .Include(t => t.Folder)
            .FirstOrDefaultAsync(t => t.Id == id);

        return template == null ? null : MapToDto(template);
    }

    public async Task<CompanyTemplateDto> GetDefaultTemplateAsync()
    {
        var template = await _context.CompanyTemplates
            .Include(t => t.Folder)
            .FirstOrDefaultAsync(t => t.IsDefault);

        if (template == null)
        {
            template = await _context.CompanyTemplates
                .Include(t => t.Folder)
                .OrderBy(t => t.Id)
                .FirstOrDefaultAsync();
        }

        if (template == null)
        {
            throw new KeyNotFoundException("Không tìm thấy template mặc định nào trong hệ thống.");
        }

        return MapToDto(template);
    }

    public async Task<CompanyTemplateDto> CreateTemplateAsync(
        IFormFile file,
        string name,
        string configJson,
        string? description = null,
        int? folderId = null)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File phôi Excel không được để trống.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
        {
            throw new ArgumentException("File phôi phải là định dạng Excel (.xlsx).", nameof(file));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tên mẫu phôi không được để trống.", nameof(name));
        }

        // Kiểm tra tính hợp lệ của configJson
        ValidateConfigJson(configJson);

        if (folderId.HasValue && !await _context.MasterDataFolders.AnyAsync(f => f.Id == folderId.Value))
        {
            throw new ArgumentException($"Thư mục với ID {folderId.Value} không tồn tại.", nameof(folderId));
        }

        var templatesDir = Path.Combine(_environment.ContentRootPath, "Templates");
        if (!Directory.Exists(templatesDir))
        {
            Directory.CreateDirectory(templatesDir);
        }

        var safeFileName = Path.GetFileName(file.FileName);
        var uniqueFileName = $"{Path.GetFileNameWithoutExtension(safeFileName)}_{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var fullPath = Path.Combine(templatesDir, uniqueFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = Path.Combine("Templates", uniqueFileName).Replace("\\", "/");
        bool isFirst = !await _context.CompanyTemplates.AnyAsync();

        var template = new CompanyTemplate
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            TemplateFileName = safeFileName,
            TemplateFilePath = relativePath,
            ConfigJson = configJson.Trim(),
            IsDefault = isFirst,
            FolderId = folderId,
            CreatedAt = DateTime.UtcNow
        };

        _context.CompanyTemplates.Add(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Đã tải lên và lưu cấu hình template mới: {Name} ({FileName})", template.Name, template.TemplateFileName);

        if (template.FolderId.HasValue)
        {
            await _context.Entry(template).Reference(t => t.Folder).LoadAsync();
        }

        return MapToDto(template);
    }

    public async Task<bool> UpdateTemplateConfigAsync(int id, string configJson)
    {
        var template = await _context.CompanyTemplates.FindAsync(id);
        if (template == null)
        {
            return false;
        }

        ValidateConfigJson(configJson);

        template.ConfigJson = configJson.Trim();
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã cập nhật cấu hình tọa độ cho template ID: {Id}", id);
        return true;
    }

    public async Task<bool> SetDefaultTemplateAsync(int id)
    {
        var target = await _context.CompanyTemplates.FindAsync(id);
        if (target == null)
        {
            return false;
        }

        var allTemplates = await _context.CompanyTemplates.ToListAsync();
        foreach (var t in allTemplates)
        {
            t.IsDefault = (t.Id == id);
            if (t.Id == id)
            {
                t.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Đã đặt template ID {Id} ({Name}) làm mẫu mặc định hệ thống.", id, target.Name);
        return true;
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetTemplateFileAsync(int id)
    {
        var template = await _context.CompanyTemplates.FindAsync(id);
        if (template == null) return null;

        var candidates = new[]
        {
            template.TemplateFilePath,
            Path.Combine(_environment.ContentRootPath, template.TemplateFilePath),
            Path.Combine(AppContext.BaseDirectory, template.TemplateFilePath),
            Path.Combine(Directory.GetCurrentDirectory(), template.TemplateFilePath),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "ShoeExportInvoice.Api", template.TemplateFilePath),
            Path.Combine(_environment.ContentRootPath, "Templates", Path.GetFileName(template.TemplateFilePath)),
            Path.Combine(AppContext.BaseDirectory, "Templates", "Shipment_Template.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Shipment_Template.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "ShoeExportInvoice.Api", "Templates", "Shipment_Template.xlsx")
        };

        string? foundPath = candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
        if (foundPath == null) return null;

        var bytes = await File.ReadAllBytesAsync(foundPath);
        var fileName = !string.IsNullOrWhiteSpace(template.TemplateFileName)
            ? template.TemplateFileName
            : Path.GetFileName(foundPath);

        return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static void ValidateConfigJson(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            throw new ArgumentException("ConfigJson không được để trống.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<DocumentTemplateConfig>(configJson, JsonOptions);
            if (parsed == null || parsed.InvSheet == null || parsed.PklSheet == null)
            {
                throw new ArgumentException("ConfigJson thiếu thông tin cấu hình InvSheet hoặc PklSheet.");
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"ConfigJson không đúng định dạng JSON hợp lệ: {ex.Message}", ex);
        }
    }

    private static CompanyTemplateDto MapToDto(CompanyTemplate entity)
    {
        DocumentTemplateConfig? config = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(entity.ConfigJson))
            {
                config = JsonSerializer.Deserialize<DocumentTemplateConfig>(entity.ConfigJson, JsonOptions);
            }
        }
        catch
        {
            // Fallback nếu JSON không hợp lệ
        }

        return new CompanyTemplateDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            TemplateFileName = entity.TemplateFileName,
            TemplateFilePath = entity.TemplateFilePath,
            ConfigJson = entity.ConfigJson,
            Config = config,
            IsDefault = entity.IsDefault,
            FolderId = entity.FolderId,
            FolderName = entity.Folder?.Name,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
