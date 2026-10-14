using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class ReviewHardeningTests
{
    private static DocumentWorkflowProfile OptionalApproval()
    { var p = Fixture.Profile(); return p with { ValidationPolicy = p.ValidationPolicy with { RequireHumanApproval = false } }; }
    [Fact] public void OptionalApprovalStillRequiresReviewEvidence()
    {
        var p = OptionalApproval(); var d = Fixture.Document();
        Assert.False(new ExportEligibilityPolicy().Evaluate(d, p).CanExport);
    }
    [Theory] [InlineData(DocumentReviewState.ReadyForApproval)] [InlineData(DocumentReviewState.Approved)]
    public void DirectExportReadyStateAssignmentMustBeRejected(DocumentReviewState state)
        => Assert.Throws<InvalidOperationException>(() => Fixture.Document().Review(state));
    [Theory] [InlineData(DocumentReviewState.ReadyForApproval)] [InlineData(DocumentReviewState.Approved)]
    public void DirectStateAssignmentCannotEnableOptionalApprovalExport(DocumentReviewState state)
    {
        var d = Fixture.Document(); var p = OptionalApproval();
        try { d = d.Review(state); }
        catch (InvalidOperationException) { }
        Assert.False(new ExportEligibilityPolicy().Evaluate(d, p).CanExport);
    }
    [Fact] public void RejectedDocumentCannotBeReopenedByStateAssignment()
    {
        var d = Fixture.Document().Review(DocumentReviewState.Rejected);
        Assert.Throws<InvalidOperationException>(() => d.Review(DocumentReviewState.ReadyForApproval));
        Assert.Throws<InvalidOperationException>(() => d.Review(DocumentReviewState.NeedsReview));
    }
    [Fact] public void DirectApprovedStateWithoutRecordIsAlreadyBlockedByDefaultPolicy()
    {
        var d = Fixture.Document();
        try { d = d.Review(DocumentReviewState.Approved); }
        catch (InvalidOperationException) { }
        Assert.False(new ExportEligibilityPolicy().Evaluate(d, Fixture.Profile()).CanExport);
    }
}

public class CompletedReviewTests
{
    private static DocumentWorkflowProfile OptionalApproval()
    { var p = Fixture.Profile(); return p with { ValidationPolicy = p.ValidationPolicy with { RequireHumanApproval = false } }; }
    [Fact] public void ProperReviewWithOptionalFinalApprovalExports()
    {
        var p = OptionalApproval(); var d = Fixture.Reviewed(p: p);
        Assert.Null(d.Approval); Assert.NotNull(d.ReviewEvidence);
        Assert.True(new ExportEligibilityPolicy().Evaluate(d, p).CanExport);
    }
    [Fact] public void DefaultWorkflowStillRequiresFinalApproval()
    {
        var d = Fixture.Reviewed();
        Assert.Contains("APPROVAL_MISSING", new ExportEligibilityPolicy().Evaluate(d, Fixture.Profile()).DenialReasons);
        Assert.True(new ExportEligibilityPolicy().Evaluate(d.Approve("approver", Fixture.Profile()), Fixture.Profile()).CanExport);
    }
    [Fact] public void EditInvalidatesReviewAndApproval()
    {
        var p = OptionalApproval(); var d = Fixture.Reviewed(p: p);
        var edited = d.Edit(d.Header.SetItem("total", TypedFieldValue.FromDecimal(3)), d.LineItems);
        Assert.Null(edited.ReviewEvidence); Assert.False(edited.HasCurrentReview(p));
        Assert.False(new ExportEligibilityPolicy().Evaluate(edited, p).CanExport);
        Assert.True(d.HasCurrentReview(p));
    }
    [Fact] public void ProfileVersionEditClearsReview()
    {
        var p = OptionalApproval(); var d = Fixture.Reviewed(p: p).WithProfileVersion(2);
        Assert.Null(d.ReviewEvidence);
        Assert.False(new ExportEligibilityPolicy().Evaluate(d, p with { Version = 2 }).CanExport);
    }
    [Fact] public void SameVersionProfileChangeMakesReviewStale()
    {
        var p = OptionalApproval(); var d = Fixture.Reviewed(p: p);
        var changed = p with { ValidationPolicy = p.ValidationPolicy with { RequireWarningAcknowledgement = false } };
        Assert.Contains("REVIEW_STALE", new ExportEligibilityPolicy().Evaluate(d, changed).DenialReasons);
        Assert.Throws<InvalidOperationException>(() => d.Approve("approver", changed));
    }
    [Fact] public void ReviewBoundToTenantProfileAndVersion()
    {
        var p = OptionalApproval(); var d = Fixture.Document().Review(DocumentReviewState.NeedsReview);
        Assert.Throws<InvalidOperationException>(() => d.CompleteReview("reviewer", p with { TenantId = "tenant-b" }));
        Assert.Throws<InvalidOperationException>(() => d.CompleteReview("reviewer", p with { ProfileId = "other" }));
        Assert.Throws<InvalidOperationException>(() => d.CompleteReview("reviewer", p with { Version = 2 }));
        var reviewed = d.CompleteReview("reviewer", p);
        Assert.Equal(d.DataRevision, reviewed.ReviewEvidence!.DataRevision);
        Assert.Equal(WorkflowProfileIdentity.Fingerprint(p), reviewed.ReviewEvidence.ProfileFingerprint);
    }
    [Fact] public void CompletingReviewRequiresNamedReviewerAndReviewInProgress()
    {
        Assert.Throws<InvalidOperationException>(() => Fixture.Document().CompleteReview("reviewer", Fixture.Profile()));
        Assert.Throws<InvalidOperationException>(() => Fixture.Document().Review(DocumentReviewState.NeedsReview)
            .CompleteReview(" ", Fixture.Profile()));
        Assert.Throws<InvalidOperationException>(() => Fixture.Document().Review(DocumentReviewState.Rejected)
            .CompleteReview("reviewer", Fixture.Profile()));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void StartingAnotherReviewClearsAllEvidenceAndAcknowledgements(bool approved)
    {
        var d = (approved ? Fixture.Approved() : Fixture.Reviewed()).Acknowledge("amount-check:0").Review(DocumentReviewState.NeedsReview);
        Assert.Null(d.ReviewEvidence); Assert.Null(d.Approval); Assert.Empty(d.AcknowledgedWarnings);
        Assert.False(new ExportEligibilityPolicy().Evaluate(d, Fixture.Profile()).CanExport);
    }
    [Fact] public void OptionalApprovalCannotBypassBlockingErrors()
    {
        var p = OptionalApproval(); var d = Fixture.Reviewed(Fixture.Document(amount: 7), p).Acknowledge("amount-check:0");
        Assert.Contains("BLOCKING_ERRORS", new ExportEligibilityPolicy().Evaluate(d, p).DenialReasons);
    }
    [Fact] public void OptionalApprovalWarningAcknowledgementStillRequired()
    {
        var p = OptionalApproval(); p = p with { ValidationPolicy = p.ValidationPolicy with {
            Arithmetic = System.Collections.Immutable.ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Severity = IssueSeverity.Warning }) } };
        var d = Fixture.Reviewed(Fixture.Document(amount: 7), p); var policy = new ExportEligibilityPolicy();
        Assert.Contains("WARNING_NOT_ACKNOWLEDGED", policy.Evaluate(d,p).DenialReasons);
        Assert.True(policy.Evaluate(d.Acknowledge("amount-check:0"),p).CanExport);
    }
    [Fact] public void UnknownStateNeverAccepted() => Assert.Throws<InvalidOperationException>(() =>
        Fixture.Document().Review((DocumentReviewState)99));
}
