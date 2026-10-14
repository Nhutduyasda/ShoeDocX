using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class ReviewStateTests
{
    [Fact] public void ApprovalRequiresReview() => Assert.Throws<InvalidOperationException>(() => Fixture.Document().Approve("reviewer", Fixture.Profile()));
    [Fact] public void ApprovalRequiresNamedReviewer() => Assert.Throws<InvalidOperationException>(() =>
        Fixture.Reviewed().Approve(" ", Fixture.Profile()));
    [Fact] public void EditingClearsApprovalAndWarnings()
    { var d = Fixture.Approved().Acknowledge("warning"); var changed = d.Edit(d.Header, d.LineItems);
      Assert.Null(changed.Approval); Assert.Empty(changed.AcknowledgedWarnings);
      Assert.Equal(d.DataRevision + 1, changed.DataRevision); Assert.NotNull(d.Approval); }
    [Fact] public void ProfileChangeClearsApproval() => Assert.Null(Fixture.Approved().WithProfileVersion(2).Approval);
    [Fact] public void ApprovalCapturesRevisionAndVersion()
    { var d = Fixture.Approved(); Assert.Equal(d.DataRevision, d.Approval!.DataRevision);
      Assert.Equal(d.ProfileVersion, d.Approval.ProfileVersion); }
}
