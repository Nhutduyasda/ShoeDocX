namespace ShoeExportInvoice.Api.Models.Dtos;

public class PagedResultDto<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 1));
    public decimal? AvgUnitPriceCMT { get; set; }
    public decimal? AvgUnitPriceDAP { get; set; }
}
