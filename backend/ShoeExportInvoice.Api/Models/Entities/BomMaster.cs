using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("BomMasters")]
public class BomMaster : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public Guid? TenantId { get; set; }

    [Required, MaxLength(50)]
    public string StyleCode { get; set; } = string.Empty;

    [EnumDataType(typeof(ProcessType))]
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;

    [Required, MaxLength(30)]
    public string Version { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
}
