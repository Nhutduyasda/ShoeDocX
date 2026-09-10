using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

/// <summary>
/// Bảng cài đặt hệ thống dạng key-value, dùng để lưu LastSequenceNumber và các tham số toàn cục.
/// </summary>
[Table("SystemSettings")]
public class SystemSetting
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
