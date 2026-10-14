using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class ExportEligibilityTests
{
    private static ExportEligibilityResult Export(CanonicalDocument d) => new ExportEligibilityPolicy().Evaluate(d, Fixture.Profile());
    [Fact] public void BlockingErrorsPreventApprovedExport() => Assert.Contains("BLOCKING_ERRORS", Export(Fixture.Approved(Fixture.Document(amount:7))).DenialReasons);
    [Fact] public void AcknowledgementCannotClearBlockingError() => Assert.False(Export(Fixture.Approved(Fixture.Document(amount:7))
        .Acknowledge("amount-check:0")).CanExport);
    [Fact] public void ValidUnapprovedDataCannotExport() => Assert.False(Export(Fixture.Document().Review(DocumentReviewState.ReadyForApproval)).CanExport);
    [Fact] public void ApprovedAndValidatedCanExport() => Assert.True(Export(Fixture.Approved()).CanExport);
    [Fact] public void EditingInvalidatesExport()
    { var d = Fixture.Approved(); Assert.False(Export(d.Edit(d.Header, d.LineItems)).CanExport); }
    [Fact] public void NewProfileVersionInvalidatesExport()
    { Assert.False(new ExportEligibilityPolicy().Evaluate(Fixture.Approved(), Fixture.Profile() with { Version = 2 }).CanExport); }
    [Fact] public void RejectedCannotExport() => Assert.Contains("DOCUMENT_REJECTED",
        Export(Fixture.Approved().Review(DocumentReviewState.Rejected)).DenialReasons);
    [Theory] [InlineData(DocumentReviewState.Draft)] [InlineData(DocumentReviewState.NeedsReview)]
    [InlineData(DocumentReviewState.ReadyForApproval)] [InlineData(DocumentReviewState.Approved)]
    [InlineData(DocumentReviewState.Rejected)] [InlineData((DocumentReviewState)99)]
    public void UncheckedStatesCannotExport(DocumentReviewState state) => Assert.False(Export(Fixture.Document().Review(state)).CanExport);
    [Fact] public void TenantMismatchRejected() => Assert.Contains(Fixture.Validate(Fixture.Document(tenant:"tenant-b")).Issues,
        i => i.Code == "DOCUMENT_PROFILE_MISMATCH");
    [Fact] public void DifferentTenantsNotInterchangeable()
    { Assert.False(new ExportEligibilityPolicy().Evaluate(Fixture.Approved(), Fixture.Profile() with { TenantId = "tenant-b" }).CanExport); }
    [Fact] public void RequiredUnevaluatedRuleBlocksEvenWhenOnlyWarning()
    { var p = Fixture.Profile(); p = p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Severity = IssueSeverity.Warning }) },
        ExtractionSchema = p.ExtractionSchema with { Fields = p.ExtractionSchema.Fields.SetItem(3,
            p.ExtractionSchema.Fields[3] with { Required = false }) } };
      var result = new ExportEligibilityPolicy().Evaluate(Fixture.Approved(Fixture.Document(price:null), p).Acknowledge("amount-check:0"),p);
      Assert.Contains("REQUIRED_CHECK_NOT_EVALUATED", result.DenialReasons); }
    [Fact] public void WarningNeedsAcknowledgement()
    { var p = Fixture.Profile(); p = p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Severity = IssueSeverity.Warning }) } };
      var d = Fixture.Approved(Fixture.Document(amount:7), p); var policy = new ExportEligibilityPolicy();
      Assert.Contains("WARNING_NOT_ACKNOWLEDGED", policy.Evaluate(d,p).DenialReasons);
      Assert.True(policy.Evaluate(d.Acknowledge("amount-check:0"),p).CanExport); }
    [Fact] public void SameVersionConfigurationChangeInvalidatesApproval()
    { var p = Fixture.Profile(); p = p with { ValidationPolicy = p.ValidationPolicy with { RequireWarningAcknowledgement = false } };
      Assert.Contains("APPROVAL_STALE", new ExportEligibilityPolicy().Evaluate(Fixture.Approved(), p).DenialReasons); }
    [Fact] public void FieldRejectionBlocksExport()
    { var d = Fixture.Document(); d = d.Edit(d.Header.SetItem("total", d.Header["total"] with {
        Review = FieldReviewStatus.Rejected }), d.LineItems);
      Assert.Contains("FIELD_REJECTED", Export(Fixture.Approved(d)).DenialReasons); }
}
