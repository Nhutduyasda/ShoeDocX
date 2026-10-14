using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
public sealed record ExportEligibilityResult(bool CanExport, ImmutableArray<string> DenialReasons);
public sealed class ExportEligibilityPolicy
{
    // Recompute checks against the exact immutable snapshot; callers cannot supply stale/forged results.
    public ExportEligibilityResult Evaluate(CanonicalDocument document, DocumentWorkflowProfile profile)
    {
        var result = new DocumentValidator().Validate(document, profile);
        var reasons = new List<string>();
        if (result.Issues.Any(i => i.Severity == IssueSeverity.BlockingError)) reasons.Add("BLOCKING_ERRORS");
        if (profile.ValidationPolicy.RequireEvaluatedChecks &&
            result.Checks.Any(c => c.Required && c.Outcome == CheckOutcome.NotEvaluated)) reasons.Add("REQUIRED_CHECK_NOT_EVALUATED");
        if (!Enum.IsDefined(document.ReviewState)) reasons.Add("INVALID_REVIEW_STATE");
        if (document.ReviewState == DocumentReviewState.Rejected) reasons.Add("DOCUMENT_REJECTED");
        var schemaValid = result.Checks.Any(c => c.Id == "schema" && c.Outcome == CheckOutcome.Passed);
        if (document.ReviewState is not (DocumentReviewState.ReadyForApproval or DocumentReviewState.Approved))
            reasons.Add("HUMAN_REVIEW_REQUIRED");
        if (document.ReviewEvidence is null) reasons.Add("REVIEW_EVIDENCE_MISSING");
        else if (!schemaValid || !document.HasCurrentReview(profile)) reasons.Add("REVIEW_STALE");
        if (document.Header.Values.Concat(document.LineItems.IsDefault ?
            Enumerable.Empty<TypedFieldValue>() : document.LineItems.SelectMany(l => l.Values))
            .Any(v => v.Review == FieldReviewStatus.Rejected)) reasons.Add("FIELD_REJECTED");
        if (profile.ValidationPolicy.RequireWarningAcknowledgement && result.Issues.Any(i =>
            i.Severity == IssueSeverity.Warning && !document.AcknowledgedWarnings.Contains(i.CheckId)))
            reasons.Add("WARNING_NOT_ACKNOWLEDGED");
        var approval = document.Approval;
        if ((profile.ValidationPolicy.RequireHumanApproval || document.ReviewState == DocumentReviewState.Approved) &&
            (approval is null || document.ReviewState != DocumentReviewState.Approved)) reasons.Add("APPROVAL_MISSING");
        if (approval is not null && (approval.DataRevision != document.DataRevision ||
            approval.ProfileVersion != document.ProfileVersion || approval.ProfileVersion != profile.Version ||
            (schemaValid &&
             approval.ProfileFingerprint != WorkflowProfileIdentity.Fingerprint(profile))))
            reasons.Add("APPROVAL_STALE");
        return new(reasons.Count == 0, reasons.ToImmutableArray());
    }
}
