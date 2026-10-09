namespace ShoeExportInvoice.Api.Models.Dtos;

public class CustomsParseIssueDto
{
    public string Sheet { get; set; } = string.Empty;
    public int Row { get; set; }
    public int Column { get; set; }
    public int? LineNumber { get; set; }
    public string? StyleCode { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? RawValue { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? ExpectedAmount { get; set; }
}
