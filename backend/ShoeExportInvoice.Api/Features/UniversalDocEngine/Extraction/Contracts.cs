using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

// Metadata supplied by a trusted caller AFTER authorization and hashing actual source bytes.
// Identifiers are opaque: this module never opens files or URLs and cannot authenticate callers.
public sealed record VerifiedExtractionSource(string SourceId, string Sha256, long ByteLength, int? PageCount);
public sealed record ExtractionContext(DocumentWorkflowProfile Profile, ImmutableArray<VerifiedExtractionSource> Sources,
    NormalizationPolicy Normalization);
public sealed record ExtractionObservation(string ObservationId, string SourceId, string SourceSha256,
    string DocumentGroupId, string? TableGroupId, string? RowId, int? RowPosition,
    FieldReference Field, string? RawText, int? PageNumber = null, NormalizedRegion? Region = null,
    decimal? ProviderConfidence = null, int? CandidateOrder = null);
public sealed record ExtractionBatch(string TenantId, string ProfileId, int ProfileVersion, int SchemaVersion,
    string ProfileFingerprint, string ProviderId, ImmutableArray<ExtractionObservation> Observations);
public enum DecimalConvention { DotDecimal, CommaDecimal }
// Multiple accepted conventions/formats are tried independently. Different parsed values are ambiguity.
public sealed record FieldNormalizationRule(FieldReference Field, ImmutableArray<DecimalConvention> DecimalConventions,
    ImmutableArray<string> DateFormats, ImmutableArray<string> TrueTokens, ImmutableArray<string> FalseTokens);
public sealed record NormalizationPolicy(ImmutableArray<FieldNormalizationRule> Rules)
{
    public static NormalizationPolicy Conservative { get; } = new(ImmutableArray<FieldNormalizationRule>.Empty);
}
public enum NormalizedValueState { Value, ObservedEmpty, Invalid, Ambiguous, NotObserved }
// Diagnostics use fixed messages/codes, never raw document text. Candidate IDs reference the sidecar.
public sealed record ExtractionIssue(string Code, string Message, string? ObservationId = null,
    string? GroupId = null, FieldReference? Field = null, ImmutableArray<string> CandidateIds = default);
public sealed record NormalizedValue(NormalizedValueState State, TypedFieldValue? Value, string? IssueCode);
public sealed record ExtractedDocument(string SourceId, string DocumentGroupId, string? TableGroupId,
    CanonicalDocument Document, ValidationResult Validation, ImmutableArray<ExtractionIssue> Issues,
    ImmutableArray<ExtractionObservation> Observations)
{
    public bool RequiresManualCorrection => !Issues.IsEmpty || !Validation.Issues.IsEmpty;
}
public sealed record ExtractionResult(ImmutableArray<ExtractedDocument> Documents,
    ImmutableArray<ExtractionIssue> Issues)
{
    public bool RequiresManualCorrection => Documents.IsEmpty || !Issues.IsEmpty || Documents.Any(d => d.RequiresManualCorrection);
}
public static class ExtractionLimits
{
    public const int Sources = 20, PagesPerSource = 1000, TotalPages = 2000, Observations = 5000,
        Documents = 100, RowsPerTable = 1000, TextLength = 10000, TotalTextLength = 1000000,
        IdentifierLength = 128, Fields = 256;
    public const long SourceBytes = 50L * 1024 * 1024, TotalSourceBytes = 100L * 1024 * 1024;
}
