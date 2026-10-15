namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

// Future adapters must return only observations; authorization, normalization and review stay separate.
// The adapter receives metadata, not filesystem paths/URLs to fetch. No real adapter is included.
public interface IExtractionProvider
{
    Task<ExtractionBatch> ObserveAsync(ExtractionContext context, CancellationToken cancellationToken);
}
