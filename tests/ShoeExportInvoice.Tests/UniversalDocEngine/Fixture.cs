using System.Collections.Immutable;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
internal static class Fixture
{
    internal static FieldReference H(string key) => new(FieldScope.Header, key);
    internal static FieldReference L(string key) => new(FieldScope.LineItem, key);
    internal static DocumentWorkflowProfile Profile() => new("synthetic", "tenant-a", 1,
        new(1, ImmutableArray.Create(new FieldDefinition("total", FieldScope.Header, FieldType.Decimal, true),
            new FieldDefinition("note", FieldScope.Header, FieldType.Text),
            new FieldDefinition("quantity", FieldScope.LineItem, FieldType.Decimal, true, 0, null, true),
            new FieldDefinition("price", FieldScope.LineItem, FieldType.Decimal, true, 0),
            new FieldDefinition("amount", FieldScope.LineItem, FieldType.Decimal, true))),
        ImmutableArray<EnrichmentRule>.Empty, ImmutableArray<ComputationRule>.Empty,
        new(ImmutableArray.Create(new ArithmeticCheck("amount-check", L("quantity"), L("price"),
            ArithmeticOperator.Multiply, L("amount"), new())),
            ImmutableArray.Create(new CrossFootCheck("total-check", L("quantity"), H("total"), new()))),
        new(ImmutableArray.Create(new ExcelMapping(L("amount"), "Items.Amount"))));
    internal static ImmutableDictionary<string, TypedFieldValue> Row(decimal? q = 2, decimal? price = 3,
        decimal? amount = 6) => ImmutableDictionary<string, TypedFieldValue>.Empty
        .Add("quantity", TypedFieldValue.FromDecimal(q)).Add("price", TypedFieldValue.FromDecimal(price))
        .Add("amount", TypedFieldValue.FromDecimal(amount));
    internal static CanonicalDocument Document(decimal? q = 2, decimal? price = 3, decimal? amount = 6,
        decimal? total = 2, string tenant = "tenant-a") => new("demo-1", tenant, "synthetic", 1,
            ImmutableDictionary<string, TypedFieldValue>.Empty.Add("total", TypedFieldValue.FromDecimal(total)),
            ImmutableArray.Create(Row(q, price, amount)));
    internal static ValidationResult Validate(CanonicalDocument? d = null, DocumentWorkflowProfile? p = null)
        => new DocumentValidator().Validate(d ?? Document(), p ?? Profile());
    internal static CanonicalDocument Reviewed(CanonicalDocument? d = null, DocumentWorkflowProfile? p = null)
    {
        var document = d ?? Document();
        if (document.ReviewState == DocumentReviewState.Draft) document = document.Review(DocumentReviewState.NeedsReview);
        return document.CompleteReview("synthetic-reviewer", p ?? Profile());
    }
    internal static CanonicalDocument Approved(CanonicalDocument? d = null, DocumentWorkflowProfile? p = null) =>
        Reviewed(d, p).Approve("synthetic-reviewer", p ?? Profile());
}
