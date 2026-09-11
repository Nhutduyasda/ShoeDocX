namespace ShoeExportInvoice.Api.Models.Dtos;

public class ImportResultDto
{
    public bool Success { get; set; } = true;
    public int TotalRowsRead { get; set; }
    public int ImportedCount { get; set; }
    public string Message { get; set; } = string.Empty;

    // Tương thích ngược với giao diện hiện tại
    public int TotalRows { get => TotalRowsRead; set => TotalRowsRead = value; }
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
