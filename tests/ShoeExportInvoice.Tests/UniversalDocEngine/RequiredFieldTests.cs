using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class RequiredFieldTests
{
    [Fact] public void MissingRequiredFieldFails()
    { var d = Fixture.Document(); Assert.Contains(Fixture.Validate(d.Edit(d.Header.Remove("total"), d.LineItems)).Issues,
        i => i.Code == "REQUIRED_FIELD_MISSING"); }
    [Fact] public void NullIsNotZero()
    { Assert.Contains(Fixture.Validate(Fixture.Document(total: null)).Issues, i => i.Code == "REQUIRED_FIELD_MISSING");
      Assert.DoesNotContain(Fixture.Validate(Fixture.Document(total: 0)).Issues, i => i.Code == "REQUIRED_FIELD_MISSING"); }
    [Fact] public void WrongTypeFails()
    { var d = Fixture.Document(); Assert.Contains(Fixture.Validate(d.Edit(d.Header.SetItem("total",
        TypedFieldValue.FromText("two")), d.LineItems)).Issues, i => i.Code == "INVALID_FIELD_TYPE"); }
    [Fact] public void OptionalFieldCanBeAbsent() => Assert.Empty(Fixture.Validate().Issues);
    [Theory] [InlineData(-1)] [InlineData(0)]
    public void QuantityRangeIsConfigured(decimal q) => Assert.Contains(Fixture.Validate(Fixture.Document(q: q)).Issues,
        i => i.Code == "VALUE_OUT_OF_RANGE");
    [Fact] public void AdjustmentProfileAllowsNegativeQuantity()
    { var p = Fixture.Profile(); p = p with { ExtractionSchema = p.ExtractionSchema with {
        Fields = p.ExtractionSchema.Fields.SetItem(2, p.ExtractionSchema.Fields[2] with { Minimum = null, ExclusiveMinimum = false }) } };
      Assert.Empty(Fixture.Validate(Fixture.Document(-2, 3, -6, -2), p).Issues); }
    [Fact] public void EmptyTextIsMissingWhenRequired()
    { var p = Fixture.Profile(); p = p with { ExtractionSchema = p.ExtractionSchema with {
        Fields = p.ExtractionSchema.Fields.SetItem(1, p.ExtractionSchema.Fields[1] with { Required = true }) } };
      var d = Fixture.Document(); Assert.Contains(Fixture.Validate(d.Edit(d.Header.Add("note", TypedFieldValue.FromText("  ")),
        d.LineItems), p).Issues, i => i.Code == "REQUIRED_FIELD_MISSING"); }
    [Fact] public void DateAndBooleanAreTyped()
    { var p = Fixture.Profile(); p = p with { ExtractionSchema = p.ExtractionSchema with { Fields = p.ExtractionSchema.Fields
        .Add(new("date", FieldScope.Header, FieldType.Date, true)).Add(new("flag", FieldScope.Header, FieldType.Boolean, true)) } };
      var d = Fixture.Document(); Assert.Empty(Fixture.Validate(d.Edit(d.Header.Add("date", TypedFieldValue.FromDate(new DateOnly(2026,1,1)))
        .Add("flag", TypedFieldValue.FromBoolean(false)), d.LineItems), p).Issues); }
}

public class ProvenanceTests
{
    [Fact] public void ComputedPrintedTotalCannotVerifyItself()
    { var d = Fixture.Document(); d = d.Edit(d.Header.SetItem("total", d.Header["total"] with { Source = FieldSource.Computed }), d.LineItems);
      Assert.Contains(Fixture.Validate(d).Issues, i => i.Code == "INVALID_FIELD_PROVENANCE"); }
    [Fact] public void UnknownFieldsCannotSilentlyExport()
    { var d = Fixture.Document(); d = d.Edit(d.Header.Add("unknown", TypedFieldValue.FromDecimal(1)), d.LineItems);
      Assert.Contains(Fixture.Validate(d).Issues, i => i.Code == "UNKNOWN_DOCUMENT_FIELD"); }
}
