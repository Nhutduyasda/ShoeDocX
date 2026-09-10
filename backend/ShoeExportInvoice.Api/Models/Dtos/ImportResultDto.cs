namespace ShoeExportInvoice.Api.Models.Dtos;

public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int FailedCount => Errors.Count;
    public List<ImportErrorDetail> Errors { get; set; } = new();
}

public class ImportErrorDetail
{
    public int RowNumber { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
