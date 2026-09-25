using System.Text.Json.Serialization;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class OcrItemDto
{
    public string StyleCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Note { get; set; } = string.Empty;
    public ProcessType ProcessType { get; set; } = ProcessType.Standard;
    public decimal UnitPriceCMT { get; set; }
    public decimal UnitPriceDAP { get; set; }
    public int PairPerCarton { get; set; } = 12;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "đôi";
    public bool IsMatched { get; set; }
}

public class OcrSourceRegionDto
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
}

public class OcrDetectedDocumentDto
{
    public string DocumentId { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public List<OcrItemDto> Items { get; set; } = new();
    public int? ReportedTotal { get; set; }
    public int CalculatedTotal { get; set; }
    public bool HasReportedTotal => ReportedTotal.HasValue;
    public bool IsTotalMatched => ReportedTotal.HasValue && ReportedTotal.Value == CalculatedTotal;
    public int? Discrepancy => ReportedTotal.HasValue ? CalculatedTotal - ReportedTotal.Value : null;
    public OcrSourceRegionDto? SourceRegion { get; set; }
    public bool HasStandardItems => Items.Any(i => i.ProcessType == ProcessType.Standard);
    public bool HasGoItems => Items.Any(i => i.ProcessType == ProcessType.GoKhongMay);
    public bool IsManuallyConfirmed { get; set; }
    public string? ConfirmationReason { get; set; }
}

public class OcrMismatchConfirmRequestDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int? ReportedTotal { get; set; }
    public int CalculatedTotal { get; set; }
    public int Discrepancy => CalculatedTotal - (ReportedTotal ?? 0);
    public int? ContractFolderId { get; set; }
    public string? Reason { get; set; }
}

public class ReEnrichOcrDocumentsRequestDto
{
    public List<OcrDetectedDocumentDto> Documents { get; set; } = new();
    public int? ContractFolderId { get; set; }
}

public class OcrExtractionResponseDto
{
    public List<OcrDetectedDocumentDto> Documents { get; set; } = new();
    public bool IsSimulation { get; set; }
    [JsonIgnore] public string? RawJsonResponse { get; set; }
    public string? Message { get; set; }

    [JsonIgnore] public string Title => Documents.FirstOrDefault()?.Title ?? string.Empty;
    [JsonIgnore] public List<OcrItemDto> Items => Documents.FirstOrDefault()?.Items ?? new();
    [JsonIgnore] public int? ReportedTotal => Documents.FirstOrDefault()?.ReportedTotal;
    [JsonIgnore] public int CalculatedTotal => Documents.FirstOrDefault()?.CalculatedTotal ?? 0;
    [JsonIgnore] public bool IsTotalMatched => Documents.FirstOrDefault()?.IsTotalMatched ?? false;
}

public class OcrSettingsOptions
{
    public string Provider { get; set; } = "Gemini"; // Gemini, OpenAI
    public string? GeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "gemini-1.5-flash";
    public string? OpenAIApiKey { get; set; }
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
}
