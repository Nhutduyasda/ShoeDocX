using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public static class StudioDemoCatalog
{
    public static readonly ImmutableArray<string> Workflows = ImmutableArray.Create("invoice", "receipt", "packing");
    private static FieldReference H(string key) => new(FieldScope.Header, key);
    private static FieldReference L(string key) => new(FieldScope.LineItem, key);
    public static DocumentWorkflowProfile Profile(string workflow, string tenant)
    {
        if (!Workflows.Contains(workflow)) throw new StudioFault(400, "UNSUPPORTED_WORKFLOW");
        return new("studio-" + workflow, tenant, 1, new(1, ImmutableArray.Create(
            new FieldDefinition("number", FieldScope.Header, FieldType.Text, true),
            new FieldDefinition("total", FieldScope.Header, FieldType.Decimal, true),
            new FieldDefinition("description", FieldScope.LineItem, FieldType.Text, true),
            new FieldDefinition("quantity", FieldScope.LineItem, FieldType.Decimal, true, 0),
            new FieldDefinition("price", FieldScope.LineItem, FieldType.Decimal, true, 0),
            new FieldDefinition("amount", FieldScope.LineItem, FieldType.Decimal, true, 0))),
            ImmutableArray<EnrichmentRule>.Empty, ImmutableArray<ComputationRule>.Empty,
            new(ImmutableArray.Create(new ArithmeticCheck("amount-check", L("quantity"), L("price"), ArithmeticOperator.Multiply, L("amount"), new())),
                ImmutableArray.Create(new CrossFootCheck("total-check", L("amount"), H("total"), new()))),
            new(ImmutableArray<ExcelMapping>.Empty));
    }
    public static ApprovedLabelSynonyms Synonyms(DocumentWorkflowProfile profile) => new("studio-server-v1",
        profile.ExtractionSchema.Fields.Select(f => new FieldAliases(new(f.Scope, f.Key),
            f.Key == "total" ? ImmutableArray.Create("total", "Document value") : ImmutableArray.Create(f.Key))).ToImmutableArray());
    public static byte[] Template(string workflow)
    {
        if (!Workflows.Contains(workflow)) throw new StudioFault(400, "UNSUPPORTED_WORKFLOW");
        using var wb = new XLWorkbook(); var sheet = wb.AddWorksheet("Document");
        sheet.Cell("A1").Value = "Number"; sheet.Cell("B1").Value = "sample";
        sheet.Cell("A3").Value = "Document value"; sheet.Cell("B3").Value = 0;
        string[] headings = { "Description", "Quantity", "Price", "Amount" };
        for (int col = 1; col <= 4; col++)
        {
            sheet.Cell(5, col).Value = headings[col - 1]; sheet.Cell(5, col).Style.Font.Bold = true;
            sheet.Cell(6, col).Value = "sample"; sheet.Cell(6, col).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
        sheet.Cell("B6").Style.NumberFormat.Format = "0.00";
        sheet.Cell("E6").FormulaA1 = "B6*C6"; sheet.Cell("A7").Value = "Total"; sheet.Cell("E7").FormulaA1 = "SUM(E6:E6)";
        sheet.Range("A8:C8").Merge().Value = "Signature";
        sheet.Columns().AdjustToContents(); wb.AddWorksheet("Reference").Cell("A1").Value = "Synthetic " + workflow + " only";
        using var data = new MemoryStream(); wb.SaveAs(data); return data.ToArray();
    }
    public static FieldNormalizationRule Rule(FieldReference field) => new(field, ImmutableArray.Create(DecimalConvention.DotDecimal),
        ImmutableArray.Create("yyyy-MM-dd"), ImmutableArray.Create("true"), ImmutableArray.Create("false"));
    public static ExtractionContext ExtractionContext(DocumentWorkflowProfile profile)
    {
        var seed = JsonSerializer.SerializeToUtf8Bytes(new[] { "DEMO-0001", "Vải tổng hợp", "2", "6", "12", "Packing insert", "1", "7.50", "7.50", "19.50" });
        return new(profile, ImmutableArray.Create(new VerifiedExtractionSource("synthetic-json-fixture", Convert.ToHexString(SHA256.HashData(seed)), seed.Length, 1)),
            new(profile.ExtractionSchema.Fields.Select(f => Rule(new(f.Scope, f.Key))).ToImmutableArray()));
    }
    public static ExtractionBatch Observations(ExtractionContext context)
    {
        var source = context.Sources[0]; var observations = new List<ExtractionObservation>();
        void Add(string id, FieldReference field, string raw, string? row = null, int? position = null) => observations.Add(new(id,
            source.SourceId, source.Sha256, "synthetic-document", row is null ? null : "items", row, position, field, raw,
            1, new(.1m, .1m + (position ?? 0) * .1m, .2m, .05m), null));
        Add("number", H("number"), "DEMO-0001"); Add("total", H("total"), "19.50");
        string[][] rows = { new[] { "Vải tổng hợp", "2", "6", "12" }, new[] { "Packing insert", "1", "7.50", "7.50" } };
        string[] keys = { "description", "quantity", "price", "amount" };
        for (int i = 0; i < rows.Length; i++) for (int col = 0; col < keys.Length; col++) Add($"r{i}-{keys[col]}", L(keys[col]), rows[i][col], "row-" + i, i);
        return new(context.Profile.TenantId, context.Profile.ProfileId, context.Profile.Version, context.Profile.ExtractionSchema.Version,
            WorkflowProfileIdentity.Fingerprint(context.Profile), "server-synthetic-provider", observations.ToImmutableArray());
    }
}
