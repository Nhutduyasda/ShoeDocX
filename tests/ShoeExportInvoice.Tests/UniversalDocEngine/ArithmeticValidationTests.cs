using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class ArithmeticValidationTests
{
    [Fact] public void CorrectMultiplicationPasses() => Assert.Contains(Fixture.Validate().Checks,
        c => c.Id == "amount-check:0" && c.Outcome == CheckOutcome.Passed);
    [Fact] public void IncorrectPrintedAmountFails() => Assert.Contains(Fixture.Validate(Fixture.Document(amount: 7)).Issues,
        i => i.Code == "ARITHMETIC_MISMATCH");
    [Fact] public void RoundingAndToleranceRespected()
    { var p = Fixture.Profile(); p = p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Numeric = new(.01m, 2) }) } };
      Assert.DoesNotContain(Fixture.Validate(Fixture.Document(1, 1.235m, 1.24m, 1), p).Issues, i => i.Code == "ARITHMETIC_MISMATCH");
      Assert.Contains(Fixture.Validate(Fixture.Document(1, 1.235m, 1.26m, 1), p).Issues, i => i.Code == "ARITHMETIC_MISMATCH"); }
    [Fact] public void MissingOperandIsNotEvaluated() => Assert.Contains(Fixture.Validate(Fixture.Document(price: null)).Checks,
        c => c.Id == "amount-check:0" && c.Outcome == CheckOutcome.NotEvaluated);
    [Fact] public void DivisionByZeroSafe()
    { var p = Fixture.Profile(); p = p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Operator = ArithmeticOperator.Divide }) } };
      Assert.Contains(Fixture.Validate(Fixture.Document(price: 0), p).Issues, i => i.Code == "DIVISION_BY_ZERO"); }
    [Fact] public void OverflowSafe() => Assert.Contains(Fixture.Validate(Fixture.Document(decimal.MaxValue, 2, 1, decimal.MaxValue)).Issues,
        i => i.Code == "NUMERIC_OVERFLOW");
    [Theory] [InlineData(ArithmeticOperator.Add, 5)] [InlineData(ArithmeticOperator.Subtract, -1)]
    [InlineData(ArithmeticOperator.Divide, 0.5)]
    public void AllowlistedOperatorsWork(ArithmeticOperator op, double printed)
    { var p = Fixture.Profile(); p = p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Operator = op }) } };
      var d = op == ArithmeticOperator.Divide ? Fixture.Document(2, 4, (decimal)printed) : Fixture.Document(amount: (decimal)printed);
      Assert.DoesNotContain(Fixture.Validate(d, p).Issues, i => i.Code == "ARITHMETIC_MISMATCH"); }
    [Fact] public void ComparisonWorks()
    { var p = Fixture.Profile(); p = p with { ExtractionSchema = p.ExtractionSchema with {
        Fields = p.ExtractionSchema.Fields.Add(new("comparison", FieldScope.LineItem, FieldType.Boolean)) },
        ValidationPolicy = p.ValidationPolicy with { Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with {
            Operator = ArithmeticOperator.Compare, PrintedValue = Fixture.L("comparison"), Comparison = ComparisonOperator.LessThan }) } };
      var d = Fixture.Document(); d = d.Edit(d.Header, d.LineItems.SetItem(0,
        d.LineItems[0].Add("comparison", TypedFieldValue.FromBoolean(true)))); Assert.Empty(Fixture.Validate(d,p).Issues); }
    [Fact] public void ValidationDeterministicAndDoesNotMutate()
    { var d = Fixture.Document(amount: 7); var p = Fixture.Profile(); var a = Fixture.Validate(d,p); var b = Fixture.Validate(d,p);
      Assert.Equal(a.Checks.ToArray(), b.Checks.ToArray()); Assert.Equal(a.Issues.ToArray(), b.Issues.ToArray());
      Assert.Equal(7m, d.LineItems[0]["amount"].Number); Assert.Equal(1L, d.DataRevision);
      Assert.Equal(ArithmeticOperator.Multiply, p.ValidationPolicy.Arithmetic[0].Operator); }
}
