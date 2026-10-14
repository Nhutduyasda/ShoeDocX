using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class CrossFootValidationTests
{
    [Fact] public void MatchingTotalsPass() => Assert.Contains(Fixture.Validate().Checks,
        c => c.Id == "total-check" && c.Outcome == CheckOutcome.Passed);
    [Fact] public void DifferentTotalsFail() => Assert.Contains(Fixture.Validate(Fixture.Document(total: 3)).Issues,
        i => i.Code == "SOURCE_TOTAL_MISMATCH");
    [Fact] public void MissingPrintedTotalIsNotEvaluated() => Assert.Contains(Fixture.Validate(Fixture.Document(total: null)).Checks,
        c => c.Id == "total-check" && c.Outcome == CheckOutcome.NotEvaluated);
    [Fact] public void EmptyLinesDoNotVerifyZero()
    { var d = Fixture.Document(total: 0); Assert.Contains(Fixture.Validate(d.Edit(d.Header,
        ImmutableArray<ImmutableDictionary<string, TypedFieldValue>>.Empty)).Checks,
        c => c.Id == "total-check" && c.Outcome == CheckOutcome.NotEvaluated); }
    [Fact] public void MissingLineQuantityDoesNotBecomeZero()
    { var d = Fixture.Document(); d = d.Edit(d.Header, d.LineItems.Add(Fixture.Row(q: null)));
      Assert.Contains(Fixture.Validate(d).Checks, c => c.Id == "total-check" && c.Outcome == CheckOutcome.NotEvaluated); }
    [Fact] public void DecimalSumsExact()
    { var d = Fixture.Document(total: .3m); d = d.Edit(d.Header, ImmutableArray.Create(
        Fixture.Row(.1m, 1, .1m), Fixture.Row(.2m, 1, .2m))); Assert.Empty(Fixture.Validate(d).Issues); }
}
