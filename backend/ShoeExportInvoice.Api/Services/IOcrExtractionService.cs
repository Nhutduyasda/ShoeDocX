using ShoeExportInvoice.Api.Models.Dtos;

namespace ShoeExportInvoice.Api.Services;

public interface IOcrExtractionService
{
    Task<OcrExtractionResponseDto> ExtractFromImageAsync(Stream imageStream, string mimeType, CancellationToken cancellationToken = default);
}
