using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Templates;

namespace ShoeExportInvoice.Api.Services;

public record AiTemplateAnalysisResult(
    string DetectedName,
    DocumentTemplateConfig Config,
    string TextGridSummary,
    bool IsAiAnalyzed
);

public interface ITemplateAiParserService
{
    string ExtractTextGrid(Stream excelStream);
    Task<AiTemplateAnalysisResult> AnalyzeTemplateAsync(Stream excelStream, string fileName, CancellationToken cancellationToken = default);
    Task<DocumentPreviewResponseDto> GenerateDummyPreviewAsync(Stream excelStream, DocumentTemplateConfig config, CancellationToken cancellationToken = default);
}
