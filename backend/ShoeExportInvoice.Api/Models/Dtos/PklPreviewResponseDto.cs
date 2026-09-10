namespace ShoeExportInvoice.Api.Models.Dtos;

public class PklPreviewResponseDto
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string PoSuffix { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int TotalCartons { get; set; }
    public decimal TotalNetWeight { get; set; }
    public decimal TotalGrossWeight { get; set; }
    public List<PklBreakdownItemDto> BreakdownItems { get; set; } = new();
}
