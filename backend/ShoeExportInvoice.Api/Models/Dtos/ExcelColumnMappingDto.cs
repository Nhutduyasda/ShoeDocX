namespace ShoeExportInvoice.Api.Models.Dtos;

public class ExcelColumnInfoDto
{
    public int Index { get; set; } // 1-based index
    public string ColumnLetter { get; set; } = string.Empty; // "A", "B", ...
    public string? HeaderName { get; set; }
    public List<string> SampleValues { get; set; } = new();
}

public class DetectedMappingDto
{
    public int StyleCodeCol { get; set; } = 1;
    public int? PoSuffixCol { get; set; }
    public int CmtPriceCol { get; set; } = 4;
    public int DapPriceCol { get; set; } = 5;
    public int DescriptionCol { get; set; } = 6;
    public int? HsCodeCol { get; set; }
    public int? UnitCol { get; set; }
    public int? PairsPerCartonCol { get; set; }
}

public class PreviewRowDto
{
    public int RowNumber { get; set; }
    public string StyleCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    public string? HsCode { get; set; }
    public string? Unit { get; set; }
    public int PairsPerCarton { get; set; }
    public bool IsGo { get; set; }
}

public class ImportPreviewResponseDto
{
    public int TotalRows { get; set; }
    public int StartRowIndex { get; set; }
    public DetectedMappingDto DetectedMapping { get; set; } = new();
    public List<ExcelColumnInfoDto> AvailableColumns { get; set; } = new();
    public List<PreviewRowDto> PreviewRows { get; set; } = new();
}

public class ColumnMappingOverrideDto
{
    public int? StyleCodeCol { get; set; }
    public int? PoSuffixCol { get; set; }
    public int? CmtPriceCol { get; set; }
    public int? DapPriceCol { get; set; }
    public int? DescriptionCol { get; set; }
    public int? HsCodeCol { get; set; }
    public int? UnitCol { get; set; }
    public int? PairsPerCartonCol { get; set; }
}
