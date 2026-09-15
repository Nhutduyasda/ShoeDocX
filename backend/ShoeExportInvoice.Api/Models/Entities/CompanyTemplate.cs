using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("CompanyTemplates")]
public class CompanyTemplate
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(255)]
    public string TemplateFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string TemplateFilePath { get; set; } = string.Empty; // Đường dẫn lưu trữ file .xlsx phôi

    [Required]
    public string ConfigJson { get; set; } = string.Empty;       // Lưu DocumentTemplateConfig dạng JSON

    public bool IsDefault { get; set; } = false;

    public int? FolderId { get; set; }                          // Liên kết với MasterDataFolder (nếu có)

    [ForeignKey(nameof(FolderId))]
    public MasterDataFolder? Folder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
