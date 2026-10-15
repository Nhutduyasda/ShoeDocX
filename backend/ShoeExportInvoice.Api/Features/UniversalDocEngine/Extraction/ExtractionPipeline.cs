using System.Collections.Immutable;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

public sealed class ExtractionPipeline
{
    public ExtractionResult Extract(ExtractionContext context, ExtractionBatch observations) =>
        new DocumentExtractionAssembler().Assemble(context, observations);

    public async Task<ExtractionResult> ExtractAsync(ExtractionContext context, IExtractionProvider provider,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new ExtractionObservationValidator().ValidateContext(context);
        if (!issues.IsEmpty) return new(ImmutableArray<ExtractedDocument>.Empty, issues);
        ArgumentNullException.ThrowIfNull(provider);
        // Provider exceptions propagate without logging payloads; cancellation never yields a partial draft.
        var batch = await provider.ObserveAsync(context, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = Extract(context, batch);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
