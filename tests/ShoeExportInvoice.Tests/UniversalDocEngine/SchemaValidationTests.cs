using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class SchemaValidationTests
{
    private static void Invalid(DocumentWorkflowProfile p, string code) =>
        Assert.Contains(new DocumentValidator().ValidateConfiguration(p).Issues, i => i.Code == code);
    [Fact] public void ValidSchemaAccepted() => Assert.Empty(new DocumentValidator().ValidateConfiguration(Fixture.Profile()).Issues);
    [Fact] public void DuplicateFieldsRejected()
    { var p = Fixture.Profile(); Invalid(p with { ExtractionSchema = p.ExtractionSchema with {
        Fields = p.ExtractionSchema.Fields.Add(p.ExtractionSchema.Fields[0]) } }, "DUPLICATE_FIELD"); }
    [Fact] public void UnknownReferenceRejected()
    { var p = Fixture.Profile(); Invalid(p with { ExcelProjectionMap = new(ImmutableArray.Create(
        new ExcelMapping(Fixture.H("unknown"), "A1"))) }, "UNKNOWN_FIELD_REFERENCE"); }
    [Fact] public void UnsupportedOperatorRejected()
    { var p = Fixture.Profile(); Invalid(p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Operator = (ArithmeticOperator)99 }) } }, "UNSUPPORTED_OPERATOR"); }
    [Theory] [InlineData(-1, 2)] [InlineData(0, 29)] [InlineData(0, -1)]
    public void InvalidNumericPolicies(decimal tolerance, int places)
    { var p = Fixture.Profile(); Invalid(p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Numeric = new(tolerance, places) }) } }, "INVALID_NUMERIC_POLICY"); }
    [Fact] public void InvalidVersionRejected() => Invalid(Fixture.Profile() with { Version = 0 }, "INVALID_SCHEMA_VERSION");
    [Fact] public void UnknownFieldTypeRejected()
    { var p = Fixture.Profile(); Invalid(p with { ExtractionSchema = p.ExtractionSchema with {
        Fields = p.ExtractionSchema.Fields.SetItem(0, p.ExtractionSchema.Fields[0] with { Type = (FieldType)99 }) } }, "INVALID_FIELD_DEFINITION"); }
    [Fact] public void IncompatibleTypeRejected()
    { var p = Fixture.Profile(); Invalid(p with { ValidationPolicy = p.ValidationPolicy with {
        Arithmetic = ImmutableArray.Create(p.ValidationPolicy.Arithmetic[0] with { Left = Fixture.H("note") }) } }, "INCOMPATIBLE_RULE_TYPE"); }
    [Fact] public void EmptyCollectionsMustBeExplicit()
    { var p = Fixture.Profile(); Invalid(p with { EnrichmentRules = default }, "INVALID_COLLECTION"); }
    [Fact] public void ComputationCyclesRejected()
    { var p = Fixture.Profile(); Invalid(p with { ComputationRules = ImmutableArray.Create(new ComputationRule(
        "cycle", new("computed", FieldScope.Header, FieldType.Decimal), Fixture.H("computed"),
        Fixture.H("total"), ArithmeticOperator.Add)) }, "COMPUTATION_CYCLE"); }
    [Fact] public void ExtractionEnrichmentComputationAndProjectionRemainSeparate()
    {
        var p = Fixture.Profile() with { EnrichmentRules = ImmutableArray.Create(new EnrichmentRule("lookup",
            new("reference", FieldScope.Header, FieldType.Text), "approved-catalog", Fixture.H("note"))),
            ComputationRules = ImmutableArray.Create(new ComputationRule("calculate",
                new("derived", FieldScope.LineItem, FieldType.Decimal), Fixture.L("quantity"), Fixture.L("price"), ArithmeticOperator.Multiply)),
            ExcelProjectionMap = new(ImmutableArray.Create(new ExcelMapping(Fixture.H("reference"), "B1"),
                new ExcelMapping(Fixture.L("derived"), "Items.Derived"))) };
        Assert.Empty(new DocumentValidator().ValidateConfiguration(p).Issues);
        Assert.DoesNotContain(p.ExtractionSchema.Fields, f => f.Key == "derived" || f.Key == "reference");
    }
    [Fact] public void ContractsHaveNoLegacyOrInfrastructureDependencies()
    {
        var assembly = typeof(CanonicalDocument).Assembly;
        var types = assembly.GetTypes().Where(t => t.Namespace?.StartsWith(
            "ShoeExportInvoice.Api.Features.UniversalDocEngine", StringComparison.Ordinal) == true);
        foreach (var type in types)
        foreach (var memberType in type.GetProperties().Select(p => p.PropertyType).Concat(
            type.GetFields().Select(f => f.FieldType)).Concat(type.GetMethods().SelectMany(m =>
                m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))))
            Inspect(memberType);
        static void Inspect(Type t)
        {
            Assert.False(t.Namespace?.StartsWith("ShoeExportInvoice.Api.Models") == true);
            Assert.False(t.Namespace?.StartsWith("ShoeExportInvoice.Api.Services") == true);
            Assert.False(t.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true);
            if (t.IsGenericType) foreach (var arg in t.GetGenericArguments()) Inspect(arg);
        }
    }
}
