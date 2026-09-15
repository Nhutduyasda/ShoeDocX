using Microsoft.AspNetCore.Http;
using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface ITemplateService
{
    Task<List<CompanyTemplateDto>> GetAllTemplatesAsync();
    Task<CompanyTemplateDto?> GetTemplateByIdAsync(int id);
    Task<CompanyTemplateDto> GetDefaultTemplateAsync();
    Task<CompanyTemplateDto> CreateTemplateAsync(IFormFile file, string name, string configJson, string? description = null, int? folderId = null);
    Task<bool> UpdateTemplateConfigAsync(int id, string configJson);
    Task<bool> SetDefaultTemplateAsync(int id);
    Task<(byte[] Bytes, string ContentType, string FileName)?> GetTemplateFileAsync(int id);
}
