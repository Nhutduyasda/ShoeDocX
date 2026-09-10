namespace ShoeExportInvoice.Api.Models.Dtos;

public class ProductMasterDto
{
    public int Id { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    public string HsCode { get; set; } = string.Empty;
    public string Unit { get; set; } = "đôi";
    public int PairPerCarton { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
