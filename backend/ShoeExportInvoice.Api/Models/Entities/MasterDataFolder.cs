using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeExportInvoice.Api.Models.Entities;

[Table("MasterDataFolders")]
public class MasterDataFolder : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    [ForeignKey(nameof(ParentId))]
    public MasterDataFolder? Parent { get; set; }

    public List<MasterDataFolder> Children { get; set; } = new();

    [MaxLength(150)]
    public string? CustomerName { get; set; }

    [MaxLength(255)]
    public string? DeliveryAddress { get; set; }

    [MaxLength(100)]
    public string? ContractNo { get; set; }

    [MaxLength(50)]
    public string? PoSuffix { get; set; }

    [Range(1, 1000)]
    public int DefaultPairsPerCarton { get; set; } = 12;

    [MaxLength(30)]
    public string DefaultUnit { get; set; } = "đôi";

    public int DisplayOrder { get; set; } = 0;

    [Required]
    [MaxLength(150)]
    public string InvoiceNoPattern { get; set; } = "KMHD-NEW2026-{SEQ:4}";

    [Required]
    [MaxLength(150)]
    public string FileNamePattern { get; set; } = "KM3-26-DH{SEQ}.xlsx";

    [Range(1, int.MaxValue)]
    public int CurrentSequenceNumber { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public List<ProductMaster> Products { get; set; } = new();
}
