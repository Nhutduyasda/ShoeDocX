using ShoeExportInvoice.Api.Models.Templates;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class CompanyTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateFileName { get; set; } = string.Empty;
    public string TemplateFilePath { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public DocumentTemplateConfig? Config { get; set; }
    public bool IsDefault { get; set; }
    public int? FolderId { get; set; }
    public string? FolderName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCompanyTemplateRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ConfigJson { get; set; } = string.Empty;
    public int? FolderId { get; set; }
}

public class UpdateTemplateConfigRequestDto
{
    public string ConfigJson { get; set; } = string.Empty;
}

public class CreateCompanyTemplateForm
{
    public Microsoft.AspNetCore.Http.IFormFile File { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? FolderId { get; set; }
}

