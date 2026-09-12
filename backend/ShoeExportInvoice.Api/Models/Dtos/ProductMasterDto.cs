namespace ShoeExportInvoice.Api.Models.Dtos;

public class ProductMasterDto
{
    public int Id { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    /// <summary>Đơn giá CMT riêng cho hàng Gò không may (0 = dùng chung UnitPriceCMT)</summary>
    public decimal? UnitPriceCMT_Go { get; set; }
    /// <summary>Đơn giá DAP riêng cho hàng Gò không may (0 = dùng chung UnitPriceDAP)</summary>
    public decimal? UnitPriceDAP_Go { get; set; }
    /// <summary>Cờ nhận diện mã có đơn giá riêng cho Gò không may hay không</summary>
    public bool HasGoOption => (UnitPriceCMT_Go.HasValue && UnitPriceCMT_Go.Value > 0) || (UnitPriceDAP_Go.HasValue && UnitPriceDAP_Go.Value > 0);
    public string HsCode { get; set; } = string.Empty;
    public string Unit { get; set; } = "đôi";
    public int PairPerCarton { get; set; }
    public int? FolderId { get; set; }
    public string? FolderName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class BulkUpdateUnitDto
{
    public string Unit { get; set; } = string.Empty;
}
