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

public class OcrExtractionResponseDto
{
    public string Title { get; set; } = string.Empty;
    public List<OcrItemDto> Items { get; set; } = new();
    public int ReportedTotal { get; set; }
    public int CalculatedTotal { get; set; }
    public bool IsTotalMatched { get; set; }
    public bool IsSimulation { get; set; }
    public string? RawJsonResponse { get; set; }
    public string? Message { get; set; }
}

public class OcrSettingsOptions
{
    public string Provider { get; set; } = "Gemini"; // Gemini, OpenAI
    public string? GeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "gemini-1.5-flash";
    public string? OpenAIApiKey { get; set; }
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
}
