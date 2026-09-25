using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IOcrExtractionService
{
    Task<OcrExtractionResponseDto> ExtractFromImageAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default);
    Task<List<OcrDetectedDocumentDto>> ReEnrichOcrDocumentsForPartnerAsync(List<OcrDetectedDocumentDto> documents, int? partnerFolderId, CancellationToken cancellationToken = default);
}
