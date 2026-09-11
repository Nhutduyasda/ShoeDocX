using System.ComponentModel.DataAnnotations;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class MasterDataFolderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? CustomerName { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? ContractNo { get; set; }
    public string? PoSuffix { get; set; }
    public int DefaultPairsPerCarton { get; set; } = 12;
    public string DefaultUnit { get; set; } = "đôi";
    public int DisplayOrder { get; set; } = 0;
    public int ProductCount { get; set; }
    public int TotalProductCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MasterDataFolderDto> Children { get; set; } = new();
}

public class CreateFolderDto
{
    [Required(ErrorMessage = "Tên thư mục không được để trống")]
    [MaxLength(150, ErrorMessage = "Tên thư mục tối đa 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    [MaxLength(150)]
    public string? CustomerName { get; set; }

    [MaxLength(255)]
    public string? DeliveryAddress { get; set; }

    [MaxLength(100)]
    public string? ContractNo { get; set; }

    [MaxLength(50)]
    public string? PoSuffix { get; set; }

    [Range(1, 1000, ErrorMessage = "Quy cách đóng gói phải từ 1 đến 1000")]
    public int DefaultPairsPerCarton { get; set; } = 12;

    [MaxLength(30)]
    public string DefaultUnit { get; set; } = "đôi";

    public int DisplayOrder { get; set; } = 0;
}

public class UpdateFolderDto
{
    [Required(ErrorMessage = "Tên thư mục không được để trống")]
    [MaxLength(150, ErrorMessage = "Tên thư mục tối đa 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    [MaxLength(150)]
    public string? CustomerName { get; set; }

    [MaxLength(255)]
    public string? DeliveryAddress { get; set; }

    [MaxLength(100)]
    public string? ContractNo { get; set; }

    [MaxLength(50)]
    public string? PoSuffix { get; set; }

    [Range(1, 1000, ErrorMessage = "Quy cách đóng gói phải từ 1 đến 1000")]
    public int DefaultPairsPerCarton { get; set; } = 12;

    [MaxLength(30)]
    public string DefaultUnit { get; set; } = "đôi";

    public int DisplayOrder { get; set; } = 0;
}

public class MoveFolderDto
{
    public int? TargetParentId { get; set; }
    public int DisplayOrder { get; set; } = 0;
}

public class BulkMoveProductsDto
{
    public List<int> ProductIds { get; set; } = new();
    public int? TargetFolderId { get; set; }
}
