using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;

public enum FieldType { Text, Decimal, Date, Boolean }
public enum FieldSource { Extracted, Enriched, Computed, Manual }
public enum FieldReviewStatus { Unreviewed, Accepted, Rejected }
public enum FieldScope { Header, LineItem }
public enum DocumentReviewState { Draft, NeedsReview, ReadyForApproval, Approved, Rejected }
public sealed record FieldReference(FieldScope Scope, string Key);
public sealed record FieldDefinition(string Key, FieldScope Scope, FieldType Type, bool Required = false,
    decimal? Minimum = null, decimal? Maximum = null, bool ExclusiveMinimum = false);
public sealed record NormalizedRegion(decimal X, decimal Y, decimal Width, decimal Height);
// Provider confidence is raw metadata, never used as certainty or to grant export.
public sealed record SourceEvidence(string SourceFileId, int? PageNumber = null,
    NormalizedRegion? Region = null, string? Provider = null, decimal? ProviderConfidence = null);

public sealed record TypedFieldValue
{
    public FieldType Type { get; }
    public string? Text { get; }
    public decimal? Number { get; }
    public DateOnly? Date { get; }
    public bool? Boolean { get; }
    public FieldSource Source { get; init; } = FieldSource.Extracted;
    public FieldReviewStatus Review { get; init; } = FieldReviewStatus.Unreviewed;
    public SourceEvidence? Evidence { get; init; }
    public bool IsMissing => Type switch { FieldType.Text => string.IsNullOrWhiteSpace(Text),
        FieldType.Decimal => Number is null, FieldType.Date => Date is null,
        FieldType.Boolean => Boolean is null, _ => true };
    private TypedFieldValue(FieldType type, string? text = null, decimal? number = null,
        DateOnly? date = null, bool? boolean = null)
        => (Type, Text, Number, Date, Boolean) = (type, text, number, date, boolean);
    public static TypedFieldValue FromText(string? value) => new(FieldType.Text, text: value);
    public static TypedFieldValue FromDecimal(decimal? value) => new(FieldType.Decimal, number: value);
    public static TypedFieldValue FromDate(DateOnly? value) => new(FieldType.Date, date: value);
    public static TypedFieldValue FromBoolean(bool? value) => new(FieldType.Boolean, boolean: value);
}
public sealed record DocumentWorkflowProfile(string ProfileId, string TenantId, int Version,
    ExtractionSchema ExtractionSchema, ImmutableArray<EnrichmentRule> EnrichmentRules,
    ImmutableArray<ComputationRule> ComputationRules, ValidationPolicy ValidationPolicy,
    ExcelProjectionMap ExcelProjectionMap);
public sealed record HumanReview(string ReviewerId, long DataRevision, int ProfileVersion, string ProfileFingerprint);
public sealed record HumanApproval(string ReviewerId, long DataRevision, int ProfileVersion, string ProfileFingerprint);
// Bind approval to configuration content too, guarding against accidental version reuse.
public static class WorkflowProfileIdentity
{
    public static string Fingerprint(DocumentWorkflowProfile profile) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(profile)));
}

// Immutable content snapshots: every supported content edit advances revision and clears review.
public sealed class CanonicalDocument
{
    public string DocumentId { get; }
    public string TenantId { get; }
    public string ProfileId { get; }
    public int ProfileVersion { get; }
    public long DataRevision { get; }
    public ImmutableDictionary<string, TypedFieldValue> Header { get; }
    public ImmutableArray<ImmutableDictionary<string, TypedFieldValue>> LineItems { get; }
    public DocumentReviewState ReviewState { get; }
    public HumanReview? ReviewEvidence { get; }
    public HumanApproval? Approval { get; }
    public ImmutableHashSet<string> AcknowledgedWarnings { get; }
    public CanonicalDocument(string documentId, string tenantId, string profileId, int profileVersion,
        ImmutableDictionary<string, TypedFieldValue> header,
        ImmutableArray<ImmutableDictionary<string, TypedFieldValue>> lineItems)
        : this(documentId, tenantId, profileId, profileVersion, header, lineItems, 1,
            DocumentReviewState.Draft, null, null, ImmutableHashSet<string>.Empty) { }
    private CanonicalDocument(string id, string tenant, string profile, int version,
        ImmutableDictionary<string, TypedFieldValue> header,
        ImmutableArray<ImmutableDictionary<string, TypedFieldValue>> lines, long revision,
        DocumentReviewState state, HumanReview? review, HumanApproval? approval, ImmutableHashSet<string> warnings)
    {
        DocumentId = id; TenantId = tenant; ProfileId = profile; ProfileVersion = version;
        Header = header; LineItems = lines; DataRevision = revision; ReviewState = state;
        ReviewEvidence = review; Approval = approval; AcknowledgedWarnings = warnings;
    }
    public CanonicalDocument Edit(ImmutableDictionary<string, TypedFieldValue> header,
        ImmutableArray<ImmutableDictionary<string, TypedFieldValue>> lines) => new(DocumentId,
        TenantId, ProfileId, ProfileVersion, header, lines, checked(DataRevision + 1),
        DocumentReviewState.NeedsReview, null, null, ImmutableHashSet<string>.Empty);
    public CanonicalDocument WithProfileVersion(int version) => new(DocumentId, TenantId, ProfileId,
        version, Header, LineItems, checked(DataRevision + 1), DocumentReviewState.NeedsReview,
        null, null, ImmutableHashSet<string>.Empty);
    // State assignment may start/reopen review or reject, but cannot manufacture completion/approval.
    // Rejection is terminal for this revision; only a new Edit/profile revision can restart review.
    public CanonicalDocument Review(DocumentReviewState state)
    {
        var allowed = state switch
        {
            DocumentReviewState.NeedsReview => ReviewState is DocumentReviewState.Draft
                or DocumentReviewState.ReadyForApproval or DocumentReviewState.Approved,
            DocumentReviewState.Rejected => ReviewState is DocumentReviewState.Draft
                or DocumentReviewState.NeedsReview or DocumentReviewState.ReadyForApproval or DocumentReviewState.Approved,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"Unsupported review transition: {ReviewState} -> {state}.");
        return new(DocumentId, TenantId, ProfileId, ProfileVersion, Header, LineItems, DataRevision,
            state, null, null, ImmutableHashSet<string>.Empty);
    }
    // Reviewer identity is trusted here; integration MUST authenticate/authorize the caller.
    public CanonicalDocument CompleteReview(string reviewerId, DocumentWorkflowProfile profile)
    {
        if (ReviewState != DocumentReviewState.NeedsReview || string.IsNullOrWhiteSpace(reviewerId))
            throw new InvalidOperationException("A named reviewer and NeedsReview state are required.");
        EnsureProfileIdentity(profile);
        return new(DocumentId, TenantId, ProfileId, ProfileVersion, Header, LineItems, DataRevision,
            DocumentReviewState.ReadyForApproval, new(reviewerId, DataRevision, ProfileVersion,
                WorkflowProfileIdentity.Fingerprint(profile)), null, AcknowledgedWarnings);
    }
    public bool HasCurrentReview(DocumentWorkflowProfile profile) => ReviewEvidence is { } review &&
        !string.IsNullOrWhiteSpace(review.ReviewerId) && review.DataRevision == DataRevision &&
        review.ProfileVersion == ProfileVersion && profile.Version == ProfileVersion &&
        profile.ProfileId == ProfileId && profile.TenantId == TenantId &&
        review.ProfileFingerprint == WorkflowProfileIdentity.Fingerprint(profile);
    private void EnsureProfileIdentity(DocumentWorkflowProfile profile)
    {
        if (profile.TenantId != TenantId || profile.ProfileId != ProfileId || profile.Version != ProfileVersion)
            throw new InvalidOperationException("Review/approval profile must match the document identity.");
    }
    public CanonicalDocument Acknowledge(string checkId) => new(DocumentId, TenantId, ProfileId,
        ProfileVersion, Header, LineItems, DataRevision, ReviewState, ReviewEvidence, Approval,
        AcknowledgedWarnings.Add(checkId));
    public CanonicalDocument Approve(string reviewerId, DocumentWorkflowProfile profile)
    {
        if (ReviewState != DocumentReviewState.ReadyForApproval || string.IsNullOrWhiteSpace(reviewerId))
            throw new InvalidOperationException("A named reviewer and ReadyForApproval state are required.");
        EnsureProfileIdentity(profile);
        if (!HasCurrentReview(profile))
            throw new InvalidOperationException("Current completed review evidence is required before approval.");
        return new(DocumentId, TenantId, ProfileId, ProfileVersion, Header, LineItems, DataRevision,
            DocumentReviewState.Approved, ReviewEvidence, new(reviewerId, DataRevision, ProfileVersion,
                WorkflowProfileIdentity.Fingerprint(profile)), AcknowledgedWarnings);
    }
}
