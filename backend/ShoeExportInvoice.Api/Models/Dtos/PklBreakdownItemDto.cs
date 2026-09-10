using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class PklBreakdownItemDto
{
    public string StyleCode { get; set; } = string.Empty;
    public string FullItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    public string ProcessTypeName { get; set; } = "Thành phẩm";
    public string CartonRange { get; set; } = string.Empty;
    public int FromCarton { get; set; }
    public int ToCarton { get; set; }
    public int CartonCount { get; set; }
    public int PairsPerCarton { get; set; }
    public int StandardPairPerCarton { get; set; } = 12;
    public int Quantity { get; set; }
    public bool IsOddCarton { get; set; }
    public decimal NetWeight { get; set; }
    public decimal GrossWeight { get; set; }
}
