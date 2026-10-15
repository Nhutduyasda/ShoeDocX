using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Validation;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class ExtractionTests
{
    private const string Sha = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static FieldReference H(string key) => new(FieldScope.Header, key);
    private static FieldReference L(string key) => new(FieldScope.LineItem, key);
    private static DocumentWorkflowProfile Profile() => new("generic-extraction", "synthetic-tenant", 1,
        new(1, ImmutableArray.Create(new FieldDefinition("number", FieldScope.Header, FieldType.Text, true),
            new FieldDefinition("date", FieldScope.Header, FieldType.Date), new FieldDefinition("supplier", FieldScope.Header, FieldType.Text),
            new FieldDefinition("total", FieldScope.Header, FieldType.Decimal, true),
            new FieldDefinition("description", FieldScope.LineItem, FieldType.Text, true),
            new FieldDefinition("quantity", FieldScope.LineItem, FieldType.Decimal, true),
            new FieldDefinition("price", FieldScope.LineItem, FieldType.Decimal, true),
            new FieldDefinition("amount", FieldScope.LineItem, FieldType.Decimal, true))),
        ImmutableArray<EnrichmentRule>.Empty, ImmutableArray<ComputationRule>.Empty,
        new(ImmutableArray.Create(new ArithmeticCheck("line-amount", L("quantity"), L("price"), ArithmeticOperator.Multiply, L("amount"), new())),
            ImmutableArray.Create(new CrossFootCheck("printed-total", L("amount"), H("total"), new()))),
        new(ImmutableArray<ExcelMapping>.Empty));
    private static ExtractionContext Context(DocumentWorkflowProfile? profile = null) => new(profile ?? Profile(),
        ImmutableArray.Create(new VerifiedExtractionSource("source-a", Sha, 100, 4)), NormalizationPolicy.Conservative);
    private static ExtractionObservation O(string id, FieldReference field, string? text, string? row = null, int? position = null,
        string document = "invoice", string? table = "items", int page = 1) => new(id, "source-a", Sha, document,
        field.Scope == FieldScope.Header ? null : table, row, position, field, text, page, new(.1m, .2m, .1m, .05m), .99m, 0);
    private static ExtractionBatch Batch(ExtractionContext context, params ExtractionObservation[] observations) =>
        new(context.Profile.TenantId, context.Profile.ProfileId, context.Profile.Version, context.Profile.ExtractionSchema.Version,
            WorkflowProfileIdentity.Fingerprint(context.Profile), "synthetic-provider", observations.ToImmutableArray());
    private static ExtractionObservation[] Invoice() => new[] {
        O("number", H("number"), "00017"), O("date", H("date"), "2026-10-15"), O("supplier", H("supplier"), "Công ty Việt"),
        O("total", H("total"), "19.50"), O("d2", L("description"), "Second", "r2", 1), O("q2", L("quantity"), "1", "r2", 1),
        O("p2", L("price"), "7.50", "r2", 1), O("a2", L("amount"), "7.50", "r2", 1),
        O("d1", L("description"), "=literal", "r1", 0), O("q1", L("quantity"), "2", "r1", 0),
        O("p1", L("price"), "6", "r1", 0), O("a1", L("amount"), "12", "r1", 0) };
    private static ExtractionResult Run(ExtractionContext c, params ExtractionObservation[] observations) => new ExtractionPipeline().Extract(c, Batch(c, observations));
    private static FieldNormalizationRule Rule(FieldReference field, DecimalConvention convention = DecimalConvention.DotDecimal,
        string format = "yyyy-MM-dd") => new(field, ImmutableArray.Create(convention), ImmutableArray.Create(format),
            ImmutableArray.Create("true", "có"), ImmutableArray.Create("false", "không"));

    [Fact] public void AccountingPreservesExactTypedValuesOrderingAndEvidence()
    {
        var c = Context(); var result = Run(c, Invoice()); var d = Assert.Single(result.Documents);
        Assert.Empty(result.Issues); Assert.Empty(d.Issues); Assert.Empty(d.Validation.Issues);
        Assert.Equal("00017", d.Document.Header["number"].Text);
        Assert.Equal(new DateOnly(2026, 10, 15), d.Document.Header["date"].Date);
        Assert.Equal("Công ty Việt", d.Document.Header["supplier"].Text);
        Assert.Equal(19.50m, d.Document.Header["total"].Number);
        Assert.Equal("=literal", d.Document.LineItems[0]["description"].Text);
        Assert.Equal(2m, d.Document.LineItems[0]["quantity"].Number);
        Assert.Equal(7.50m, d.Document.LineItems[1]["price"].Number);
        Assert.Equal(12m, d.Document.LineItems[0]["amount"].Number);
        Assert.Equal(DocumentReviewState.Draft, d.Document.ReviewState); Assert.Null(d.Document.ReviewEvidence); Assert.Null(d.Document.Approval);
        Assert.Empty(d.Document.AcknowledgedWarnings);
        foreach (var v in d.Document.Header.Values.Concat(d.Document.LineItems.SelectMany(r => r.Values)))
        {
            Assert.Equal(FieldSource.Extracted, v.Source); Assert.Equal(FieldReviewStatus.Unreviewed, v.Review);
            Assert.Equal("source-a", v.Evidence!.SourceFileId); Assert.Equal(1, v.Evidence.PageNumber);
            Assert.Equal(.1m, v.Evidence.Region!.X); Assert.Equal("synthetic-provider", v.Evidence.Provider);
            Assert.Equal(.99m, v.Evidence.ProviderConfidence);
        }
        Assert.Contains(d.Observations, o => o.SourceSha256 == Sha && o.RawText == "00017");
        Assert.False(new ExportEligibilityPolicy().Evaluate(d.Document, c.Profile).CanExport);
    }

    [Theory]
    [InlineData(FieldType.Text, "00012", NormalizedValueState.Value)]
    [InlineData(FieldType.Text, "  Tiếng Việt  ", NormalizedValueState.Value)]
    [InlineData(FieldType.Text, "=SUM(A1:A2)", NormalizedValueState.Value)]
    [InlineData(FieldType.Decimal, "0", NormalizedValueState.Value)]
    [InlineData(FieldType.Decimal, "-12.50", NormalizedValueState.Value)]
    [InlineData(FieldType.Decimal, "1,234", NormalizedValueState.Ambiguous)]
    [InlineData(FieldType.Decimal, "1.234", NormalizedValueState.Ambiguous)]
    [InlineData(FieldType.Decimal, "1,234.50", NormalizedValueState.Value)]
    [InlineData(FieldType.Decimal, "1.234,50", NormalizedValueState.Value)]
    [InlineData(FieldType.Decimal, "1,23,4", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Decimal, "not-a-number", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Decimal, "1e3", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Decimal, "0.00000000000000000000000000001", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Decimal, "79228162514264337593543950336", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Decimal, "1.12345678901234567890123456789", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Date, "01/02/2026", NormalizedValueState.Ambiguous)]
    [InlineData(FieldType.Date, "2026-02-30", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Date, "15/10", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Boolean, "false", NormalizedValueState.Value)]
    [InlineData(FieldType.Boolean, "yes", NormalizedValueState.Invalid)]
    [InlineData(FieldType.Text, "", NormalizedValueState.ObservedEmpty)]
    [InlineData(FieldType.Decimal, " ", NormalizedValueState.ObservedEmpty)]
    [InlineData(FieldType.Date, null, NormalizedValueState.Invalid)]
    public void NormalizationDoesNotGuess(FieldType type, string? text, NormalizedValueState expected)
    {
        var result = new TypedValueNormalizer().Normalize(type, text); Assert.Equal(expected, result.State);
        if (expected == NormalizedValueState.Value)
        {
            Assert.Equal(type, result.Value!.Type); Assert.Equal(FieldSource.Extracted, result.Value.Source);
            Assert.Equal(FieldReviewStatus.Unreviewed, result.Value.Review);
            if (type == FieldType.Text) Assert.Equal(text, result.Value.Text);
            if (type == FieldType.Boolean) Assert.False(result.Value.Boolean);
            if (type == FieldType.Decimal) Assert.NotNull(result.Value.Number);
        }
        else { Assert.NotNull(result.IssueCode); if (expected != NormalizedValueState.ObservedEmpty) Assert.Null(result.Value); }
    }

    [Theory] [InlineData("1,234.50", DecimalConvention.DotDecimal, "1234.5")]
    [InlineData("1.234,50", DecimalConvention.CommaDecimal, "1234.5")]
    [InlineData("1,234", DecimalConvention.DotDecimal, "1234")]
    [InlineData("1,234", DecimalConvention.CommaDecimal, "1.234")]
    public void ApprovedDecimalConventionResolvesCulture(string text, DecimalConvention convention, string number)
    {
        var result = new TypedValueNormalizer().Normalize(FieldType.Decimal, text, Rule(H("n"), convention));
        Assert.Equal(NormalizedValueState.Value, result.State);
        Assert.Equal(decimal.Parse(number, CultureInfo.InvariantCulture), result.Value!.Number);
    }
    [Fact] public void TrustedDateAndBooleanTokensResolveOnlyDeclaredValues()
    {
        var n = new TypedValueNormalizer(); var r = Rule(H("x"), format: "dd/MM/yyyy");
        Assert.Equal(new DateOnly(2026, 2, 1), n.Normalize(FieldType.Date, "01/02/2026", r).Value!.Date);
        Assert.False(n.Normalize(FieldType.Boolean, "KHÔNG", r).Value!.Boolean);
        Assert.True(n.Normalize(FieldType.Boolean, "có", r).Value!.Boolean);
        Assert.Equal(NormalizedValueState.Invalid, n.Normalize(FieldType.Boolean, "yes", r).State);
    }

    [Fact] public void WarehousePreservesVietnameseAndAmbiguousQuantityWithoutZeroDefault()
    {
        var p = Profile() with { ExtractionSchema = new(1, ImmutableArray.Create(
            new FieldDefinition("receipt", FieldScope.Header, FieldType.Text, true),
            new FieldDefinition("material", FieldScope.LineItem, FieldType.Text, true),
            new FieldDefinition("description", FieldScope.LineItem, FieldType.Text),
            new FieldDefinition("unit", FieldScope.LineItem, FieldType.Text),
            new FieldDefinition("quantity", FieldScope.LineItem, FieldType.Decimal, true),
            new FieldDefinition("notes", FieldScope.LineItem, FieldType.Text))),
            ValidationPolicy = new(ImmutableArray<ArithmeticCheck>.Empty, ImmutableArray<CrossFootCheck>.Empty) };
        var c = Context(p); var observations = new[] { O("h", H("receipt"), "PN-0001"),
            O("m1", L("material"), "0008", "r1", 0), O("d1", L("description"), "Vải lót giày", "r1", 0),
            O("u1", L("unit"), "mét", "r1", 0), O("q1", L("quantity"), "1,234", "r1", 0), O("n1", L("notes"), "Đã kiểm", "r1", 0),
            O("m2", L("material"), "0009", "r2", 1), O("d2", L("description"), "Da tổng hợp", "r2", 1),
            O("u2", L("unit"), "tấm", "r2", 1), O("q2", L("quantity"), "0", "r2", 1), O("n2", L("notes"), "", "r2", 1) };
        var d = Assert.Single(Run(c, observations).Documents);
        Assert.Equal("0008", d.Document.LineItems[0]["material"].Text); Assert.Equal("Vải lót giày", d.Document.LineItems[0]["description"].Text);
        Assert.False(d.Document.LineItems[0].ContainsKey("quantity")); Assert.Equal(0m, d.Document.LineItems[1]["quantity"].Number);
        Assert.Contains(d.Issues, i => i.Code == "AMBIGUOUS_DECIMAL" && i.CandidateIds.SequenceEqual(new[] { "q1" }));
        Assert.Contains(d.Issues, i => i.Code == "OBSERVED_EMPTY");
        Assert.Contains(d.Validation.Issues, i => i.Code == "REQUIRED_FIELD_MISSING");
        Assert.True(d.RequiresManualCorrection);
        var approved = c with { Normalization = new(ImmutableArray.Create(Rule(L("quantity"), DecimalConvention.CommaDecimal))) };
        Assert.Equal(1.234m, Assert.Single(Run(approved, observations).Documents).Document.LineItems[0]["quantity"].Number);
    }

    [Fact] public void CommercialKeepsDocumentsTablesPagesAndConflictsSeparate()
    {
        var c = Context(); var rows = new List<ExtractionObservation>();
        foreach (var (table, page, x) in new[] { ("left", 1, .1m), ("right", 1, .6m), ("continued", 3, .1m) })
        {
            rows.AddRange(new[] { O(table + "d", L("description"), table, "r1", 0, table: table, page: page) with { Region = new(x, .2m, .1m, .1m) },
                O(table + "q", L("quantity"), "1", "r1", 0, table: table, page: page),
                O(table + "p", L("price"), "2", "r1", 0, table: table, page: page),
                O(table + "a", L("amount"), "2", "r1", 0, table: table, page: page) });
        }
        rows.Add(O("h", H("number"), "PACK-001"));
        rows.Add(O("conflict", L("quantity"), "9", "r1", 0, table: "right") with { CandidateOrder = 1 });
        rows.Add(O("other", H("number"), "PACK-002", document: "second"));
        rows.Add(O("page4", L("description"), "later", "r2", 1, table: "continued", page: 4));
        var result = Run(c, rows.ToArray()); Assert.Equal(4, result.Documents.Length);
        var left = Assert.Single(result.Documents.Where(d => d.TableGroupId == "left"));
        var right = Assert.Single(result.Documents.Where(d => d.TableGroupId == "right"));
        Assert.Single(left.Document.LineItems); Assert.Equal("left", left.Document.LineItems[0]["description"].Text);
        Assert.Equal(2m, left.Document.LineItems[0]["amount"].Number); Assert.Equal(.1m, left.Document.LineItems[0]["description"].Evidence!.Region!.X);
        Assert.False(right.Document.LineItems[0].ContainsKey("quantity"));
        Assert.Contains(right.Issues, i => i.Code == "CONFLICTING_OBSERVATIONS" && i.CandidateIds.Length == 2);
        Assert.Contains(left.Validation.Issues, i => i.Code == "REQUIRED_FIELD_MISSING"); Assert.False(left.Document.Header.ContainsKey("total"));
        var continued = Assert.Single(result.Documents.Where(d => d.TableGroupId == "continued"));
        Assert.Equal(new int?[] { 3, 4 }, continued.Document.LineItems.Select(r => r["description"].Evidence!.PageNumber));
        Assert.Equal("PACK-002", Assert.Single(result.Documents.Where(d => d.DocumentGroupId == "second")).Document.Header["number"].Text);
    }

    [Theory]
    [InlineData("unknown", "FIELD_NOT_EXTRACTABLE")]
    [InlineData("scope", "FIELD_NOT_EXTRACTABLE")]
    [InlineData("page-zero", "INVALID_PAGE")]
    [InlineData("page-high", "INVALID_PAGE")]
    [InlineData("region-negative", "INVALID_REGION")]
    [InlineData("region-overflow", "INVALID_REGION")]
    [InlineData("region-zero", "INVALID_REGION")]
    [InlineData("region-no-page", "INVALID_REGION")]
    [InlineData("source-missing", "UNAUTHORIZED_SOURCE")]
    [InlineData("source-wrong", "UNAUTHORIZED_SOURCE")]
    [InlineData("fingerprint", "SOURCE_FINGERPRINT_MISMATCH")]
    [InlineData("document-missing", "DOCUMENT_GROUP_REQUIRED")]
    [InlineData("table-missing", "ROW_GROUP_REQUIRED")]
    [InlineData("row-missing", "ROW_GROUP_REQUIRED")]
    [InlineData("position-negative", "ROW_GROUP_REQUIRED")]
    [InlineData("confidence", "INVALID_CONFIDENCE")]
    [InlineData("candidate", "INVALID_CANDIDATE_ORDER")]
    [InlineData("id", "INVALID_OBSERVATION_ID")]
    [InlineData("text", "TEXT_LIMIT")]
    [InlineData("header-row", "CONTRADICTORY_HEADER_GROUP")]
    [InlineData("null-field", "FIELD_NOT_EXTRACTABLE")]
    public void UntrustedObservationAdmissionFailsClosed(string kind, string code)
    {
        var o = O("obs", L("quantity"), "2", "r1", 0);
        o = kind switch {
            "unknown" => o with { Field = L("unexpected-provider-field") }, "scope" => o with { Field = H("quantity") },
            "page-zero" => o with { PageNumber = 0 }, "page-high" => o with { PageNumber = 5 },
            "region-negative" => o with { Region = new(-.1m, 0, .1m, .1m) },
            "region-overflow" => o with { Region = new(.9m, 0, .2m, .1m) },
            "region-zero" => o with { Region = new(0, 0, 0, .1m) }, "region-no-page" => o with { PageNumber = null },
            "source-missing" => o with { SourceId = "" }, "source-wrong" => o with { SourceId = "another-tenant-source" },
            "fingerprint" => o with { SourceSha256 = new string('B', 64) }, "document-missing" => o with { DocumentGroupId = "" },
            "table-missing" => o with { TableGroupId = null }, "row-missing" => o with { RowId = null },
            "position-negative" => o with { RowPosition = -1 }, "confidence" => o with { ProviderConfidence = 1.1m },
            "candidate" => o with { CandidateOrder = -1 }, "id" => o with { ObservationId = "" },
            "text" => o with { RawText = new string('x', ExtractionLimits.TextLength + 1) },
            "header-row" => o with { Field = H("number") }, "null-field" => o with { Field = null! }, _ => throw new Exception() };
        var result = Run(Context(), o); Assert.Empty(result.Documents); Assert.Contains(result.Issues, i => i.Code == code);
        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("another-tenant-source"));
    }

    [Theory] [InlineData("tenant")] [InlineData("profile")] [InlineData("version")] [InlineData("schema")]
    [InlineData("profile-fingerprint")] [InlineData("provider")]
    public void ProviderCannotChangeTrustedWorkflow(string kind)
    {
        var c = Context(); var b = Batch(c, Invoice());
        b = kind switch { "tenant" => b with { TenantId = "wrong" }, "profile" => b with { ProfileId = "wrong" },
            "version" => b with { ProfileVersion = 2 }, "schema" => b with { SchemaVersion = 2 },
            "profile-fingerprint" => b with { ProfileFingerprint = "wrong" }, _ => b with { ProviderId = "" } };
        var result = new ExtractionPipeline().Extract(c, b); Assert.Empty(result.Documents);
        Assert.Contains(result.Issues, i => i.Code == (kind == "provider" ? "INVALID_PROVIDER" : "WORKFLOW_IDENTITY_MISMATCH"));
    }

    [Theory] [InlineData("different-row-position")] [InlineData("duplicate-position")] [InlineData("different-row-page")]
    public void ContradictoryGroupingCannotCombineRows(string kind)
    {
        var a = O("a", L("quantity"), "1", "r1", 0); var b = O("b", L("price"), "2", "r1", 0);
        b = kind switch { "different-row-position" => b with { RowPosition = 1 },
            "duplicate-position" => b with { RowId = "r2" }, _ => b with { PageNumber = 2 } };
        var result = Run(Context(), a, b); Assert.Empty(result.Documents);
        Assert.Contains(result.Issues, i => i.Code.StartsWith("CONTRADICTORY_ROW", StringComparison.Ordinal));
    }

    [Fact] public void ConflictingCandidatesRemainUnresolvedRegardlessOfConfidence()
    {
        var observations = Invoice().Append(O("alternative", H("total"), "999") with { ProviderConfidence = 1m, CandidateOrder = 0 }).ToArray();
        var d = Assert.Single(Run(Context(), observations).Documents);
        Assert.False(d.Document.Header.ContainsKey("total"));
        Assert.Contains(d.Issues, i => i.Code == "CONFLICTING_OBSERVATIONS" && i.CandidateIds.Length == 2);
        Assert.Contains(d.Observations, o => o.RawText == "999"); Assert.True(d.RequiresManualCorrection);
    }
    [Fact] public void AbsentEmptyInvalidZeroAndFalseRemainDistinct()
    {
        var c = Context(); var observations = Invoice().Where(o => o.ObservationId != "supplier").ToArray();
        observations[0] = observations[0] with { RawText = "" }; observations[2] = observations[2] with { RawText = "bad" };
        var d = Assert.Single(Run(c, observations).Documents);
        Assert.Contains(d.Issues, i => i.Code == "NOT_OBSERVED" && i.Field == H("supplier"));
        Assert.Contains(d.Issues, i => i.Code == "OBSERVED_EMPTY" && i.Field == H("number"));
        Assert.Contains(d.Issues, i => i.Code == "INVALID_DECIMAL" && i.Field == H("total"));
        Assert.True(d.Document.Header["number"].IsMissing); Assert.False(d.Document.Header.ContainsKey("total"));
        Assert.Equal(0m, new TypedValueNormalizer().Normalize(FieldType.Decimal, "0").Value!.Number);
        Assert.False(new TypedValueNormalizer().Normalize(FieldType.Boolean, "false").Value!.Boolean);
    }
    [Fact] public void ConfidenceWithoutSpatialEvidenceDoesNotInventEvidenceOrApproval()
    {
        var o = O("h", H("number"), "0001") with { PageNumber = null, Region = null, ProviderConfidence = 1 };
        var d = Assert.Single(Run(Context(), o).Documents); var value = d.Document.Header["number"];
        Assert.Null(value.Evidence!.PageNumber); Assert.Null(value.Evidence.Region); Assert.Equal(1m, value.Evidence.ProviderConfidence);
        Assert.Contains(d.Issues, i => i.Code == "PAGE_EVIDENCE_ABSENT"); Assert.Contains(d.Issues, i => i.Code == "REGION_EVIDENCE_ABSENT");
        Assert.False(new ExportEligibilityPolicy().Evaluate(d.Document, Profile()).CanExport);
    }
    [Fact] public void UnknownPageBoundsAreReported()
    {
        var c = Context() with { Sources = ImmutableArray.Create(new VerifiedExtractionSource("source-a", Sha, 100, null)) };
        var d = Assert.Single(Run(c, O("h", H("number"), "n")).Documents);
        Assert.Contains(d.Issues, i => i.Code == "PAGE_BOUNDS_UNKNOWN");
    }
    [Fact] public void IndependentAuthorizedSourcesNeverShareHeadersOrRows()
    {
        var c = Context(); c = c with { Sources = c.Sources.Add(new("source-b", new string('B', 64), 100, 4)) };
        var observations = Invoice().Concat(Invoice().Select(o => o with { ObservationId = "b-" + o.ObservationId,
            SourceId = "source-b", SourceSha256 = new string('B', 64), RawText = o.ObservationId == "number" ? "different" : o.RawText })).ToArray();
        var docs = Run(c, observations).Documents; Assert.Equal(2, docs.Length);
        Assert.Equal("00017", docs[0].Document.Header["number"].Text); Assert.Equal("different", docs[1].Document.Header["number"].Text);
        Assert.All(docs[1].Observations, o => Assert.Equal("source-b", o.SourceId));
        Assert.All(docs[1].Document.LineItems.SelectMany(r => r.Values), v => Assert.Equal("source-b", v.Evidence!.SourceFileId));
        Assert.NotEqual(docs[0].Document.DocumentId, docs[1].Document.DocumentId);
    }
    [Fact] public void FalseAndZeroAreAssembledAsPresentExtractedValues()
    {
        var p = Profile() with { ExtractionSchema = new(1, ImmutableArray.Create(new FieldDefinition("flag", FieldScope.Header, FieldType.Boolean, true),
            new FieldDefinition("zero", FieldScope.Header, FieldType.Decimal, true))),
            ValidationPolicy = new(ImmutableArray<ArithmeticCheck>.Empty, ImmutableArray<CrossFootCheck>.Empty) };
        var d = Assert.Single(Run(Context(p), O("f", H("flag"), "false"), O("z", H("zero"), "0")).Documents);
        Assert.Empty(d.Validation.Issues); Assert.False(d.Document.Header["flag"].Boolean); Assert.False(d.Document.Header["flag"].IsMissing);
        Assert.Equal(0m, d.Document.Header["zero"].Number); Assert.False(d.Document.Header["zero"].IsMissing);
        Assert.Equal(FieldType.Boolean, d.Document.Header["flag"].Type); Assert.Equal(FieldType.Decimal, d.Document.Header["zero"].Type);
    }
    [Theory] [InlineData("total", "SOURCE_TOTAL_MISMATCH")] [InlineData("a1", "ARITHMETIC_MISMATCH")]
    public void PrintedValuesAreNeverReplacedToForceArithmeticConsistency(string id, string code)
    {
        var d = Assert.Single(Run(Context(), Invoice().Select(o => o.ObservationId == id ? o with { RawText = "999" } : o).ToArray()).Documents);
        Assert.Contains(d.Validation.Issues, i => i.Code == code);
        Assert.Equal(999m, id == "total" ? d.Document.Header["total"].Number : d.Document.LineItems[0]["amount"].Number);
        Assert.False(new ExportEligibilityPolicy().Evaluate(d.Document, Profile()).CanExport);
    }
    [Fact] public void NullProviderBatchAndNullRawTextAreReportedWithoutFabrication()
    {
        var c = Context(); var malformed = new ExtractionPipeline().Extract(c, null!);
        Assert.Empty(malformed.Documents); Assert.Contains(malformed.Issues, i => i.Code == "MALFORMED_PROVIDER_DATA");
        var d = Assert.Single(Run(c, O("h", H("number"), null)).Documents);
        Assert.False(d.Document.Header.ContainsKey("number")); Assert.Contains(d.Issues, i => i.Code == "RAW_TEXT_MISSING");
        Assert.Null(d.Observations[0].RawText);
    }
    [Fact] public void ExactDecimalPrecisionAndLimitsAvoidRounding()
    {
        var n = new TypedValueNormalizer(); var rule = Rule(H("x"));
        Assert.Equal(decimal.MaxValue, n.Normalize(FieldType.Decimal, decimal.MaxValue.ToString(CultureInfo.InvariantCulture), rule).Value!.Number);
        Assert.Equal(0.0000000000000000000000000001m, n.Normalize(FieldType.Decimal, "0.0000000000000000000000000001", rule).Value!.Number);
        Assert.Equal(NormalizedValueState.Invalid, n.Normalize(FieldType.Decimal, "7.9228162514264337593543950336", rule).State);
        Assert.Equal(NormalizedValueState.Invalid, n.Normalize((FieldType)99, "1").State);
    }
    [Fact] public void EnrichmentAndComputationTargetsCannotBeExtracted()
    {
        var p = Profile() with { EnrichmentRules = ImmutableArray.Create(new EnrichmentRule("lookup", new("enriched", FieldScope.Header, FieldType.Text), "master", H("number"))),
            ComputationRules = ImmutableArray.Create(new ComputationRule("computed", new("computed", FieldScope.LineItem, FieldType.Decimal), L("quantity"), L("price"), ArithmeticOperator.Multiply)) };
        foreach (var field in new[] { H("enriched"), L("computed") })
        {
            var result = Run(Context(p), O("obs", field, "4", field.Scope == FieldScope.LineItem ? "r" : null, field.Scope == FieldScope.LineItem ? 0 : null));
            Assert.Empty(result.Documents); Assert.Contains(result.Issues, i => i.Code == "FIELD_NOT_EXTRACTABLE");
        }
    }
    [Theory] [InlineData("empty", "EMPTY_EXTRACTION")] [InlineData("default", "MALFORMED_PROVIDER_DATA")]
    [InlineData("null", "MALFORMED_OBSERVATION")] [InlineData("duplicate-id", "INVALID_OBSERVATION_ID")]
    [InlineData("count", "OBSERVATION_LIMIT")] [InlineData("payload", "PAYLOAD_LIMIT")]
    [InlineData("documents", "DOCUMENT_LIMIT")]
    public void PayloadShapesAndLimitsAreEnforced(string kind, string code)
    {
        var c = Context(); var a = O("a", H("number"), "n");
        var obs = kind switch { "empty" => ImmutableArray<ExtractionObservation>.Empty, "default" => default,
            "null" => new ExtractionObservation[] { null! }.ToImmutableArray(), "duplicate-id" => ImmutableArray.Create(a, a),
            "count" => Enumerable.Repeat(a, ExtractionLimits.Observations + 1).ToImmutableArray(),
            "documents" => Enumerable.Range(0, 101).Select(i => a with { ObservationId = "o" + i, DocumentGroupId = "d" + i }).ToImmutableArray(),
            _ => Enumerable.Range(0, 101).Select(i => a with { ObservationId = "o" + i, RawText = new string('x', 10000) }).ToImmutableArray() };
        var result = new ExtractionPipeline().Extract(c, Batch(c) with { Observations = obs });
        Assert.Empty(result.Documents); Assert.Contains(result.Issues, i => i.Code == code);
    }
    [Theory] [InlineData("profile-null")] [InlineData("schema-default")] [InlineData("duplicate-schema")]
    [InlineData("invalid-source")] [InlineData("duplicate-source")] [InlineData("source-size")]
    [InlineData("page-count")] [InlineData("bad-rule")] [InlineData("rule-overlap")] [InlineData("null-policy")]
    public void MalformedTrustedConfigurationIsReported(string kind)
    {
        var c = Context();
        c = kind switch {
            "profile-null" => c with { Profile = null! },
            "schema-default" => c with { Profile = c.Profile with { ExtractionSchema = new(1, default) } },
            "duplicate-schema" => c with { Profile = c.Profile with { ExtractionSchema = new(1, c.Profile.ExtractionSchema.Fields.Add(c.Profile.ExtractionSchema.Fields[0])) } },
            "invalid-source" => c with { Sources = ImmutableArray.Create(new VerifiedExtractionSource("", "bad", 1, 1)) },
            "duplicate-source" => c with { Sources = c.Sources.Add(c.Sources[0]) },
            "source-size" => c with { Sources = ImmutableArray.Create(c.Sources[0] with { ByteLength = ExtractionLimits.SourceBytes + 1 }) },
            "page-count" => c with { Sources = ImmutableArray.Create(c.Sources[0] with { PageCount = 1001 }) },
            "bad-rule" => c with { Normalization = new(ImmutableArray.Create(Rule(H("date"), format: "dd/MM"))) },
            "rule-overlap" => c with { Normalization = new(ImmutableArray.Create(Rule(H("number")) with { FalseTokens = ImmutableArray.Create("true") })) },
            _ => c with { Normalization = null! } };
        Assert.NotEmpty(new ExtractionObservationValidator().ValidateContext(c));
    }
    [Fact] public void DeterminismAndInputImmutabilityHoldAcrossCultureAndCandidateOrder()
    {
        var c = Context(); var b = Batch(c, Invoice()); var before = JsonSerializer.Serialize(b);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN"); var first = new ExtractionPipeline().Extract(c, b);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); var second = new ExtractionPipeline().Extract(c, b with { Observations = b.Observations.Reverse().ToImmutableArray() });
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second)); Assert.Equal(before, JsonSerializer.Serialize(b));
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }
    private sealed class SyntheticProvider(ExtractionBatch batch) : IExtractionProvider
    {
        public int Calls { get; private set; }
        public Task<ExtractionBatch> ObserveAsync(ExtractionContext context, CancellationToken cancellationToken)
        { Calls++; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(batch); }
    }
    [Fact] public async Task ProviderContractUsesSyntheticObservationsOnly()
    {
        var c = Context(); var provider = new SyntheticProvider(Batch(c, Invoice()));
        var d = Assert.Single((await new ExtractionPipeline().ExtractAsync(c, provider)).Documents);
        Assert.Equal(1, provider.Calls); Assert.Equal("00017", d.Document.Header["number"].Text); Assert.Equal(DocumentReviewState.Draft, d.Document.ReviewState);
    }
    [Fact] public async Task CancellationBeforeProviderDoesNotProducePartialDraft()
    {
        var c = Context(); var provider = new SyntheticProvider(Batch(c, Invoice())); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExtractionPipeline().ExtractAsync(c, provider, cancel.Token)); Assert.Equal(0, provider.Calls);
    }
    private sealed class CancellingProvider(CancellationTokenSource cancel, ExtractionBatch batch) : IExtractionProvider
    {
        public Task<ExtractionBatch> ObserveAsync(ExtractionContext context, CancellationToken cancellationToken)
        { cancel.Cancel(); return Task.FromResult(batch); }
    }
    [Fact] public async Task CancellationAfterProviderDoesNotProducePartialDraft()
    {
        var c = Context(); using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExtractionPipeline().ExtractAsync(c, new CancellingProvider(cancel, Batch(c, Invoice())), cancel.Token));
    }
    [Fact] public async Task InvalidContextNeverCallsProvider()
    {
        var c = Context(); var provider = new SyntheticProvider(Batch(c, Invoice()));
        var result = await new ExtractionPipeline().ExtractAsync(c with { Sources = default }, provider);
        Assert.Equal(0, provider.Calls); Assert.Empty(result.Documents); Assert.NotEmpty(result.Issues);
    }

    [Fact] public void ExtractionReviewConfirmedMappingAndExistingExporterEndToEnd()
    {
        using var wb = new XLWorkbook(); var ws = wb.AddWorksheet("Invoice");
        ws.Cell("A1").Value = "Invoice number"; ws.Cell("B1").Value = "prototype";
        string[] labels = { "Description", "Quantity", "Price", "Amount" };
        for (int col = 1; col <= 4; col++) { ws.Cell(4, col).Value = labels[col - 1]; ws.Cell(4, col).Style.Font.Bold = true; ws.Cell(5, col).Value = "sample"; }
        ws.Cell("E5").FormulaA1 = "B5*C5"; ws.Cell("B5").Style.NumberFormat.Format = "0.00";
        ws.Cell("B5").Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Cell("A6").Value = "Printed total"; ws.Cell("B6").Value = 0;
        ws.Cell("E6").FormulaA1 = "SUM(E5:E5)"; ws.Range("A7:C7").Merge().Value = "Signature";
        wb.AddWorksheet("Reference").Cell("A1").Value = "Reference preserved";
        using var bytes = new MemoryStream(); wb.SaveAs(bytes); var template = bytes.ToArray();
        // Use existing Gate 3A analysis and an explicit human-selected mapping. No second exporter.
        var p = Profile() with { ExtractionSchema = new(1, Profile().ExtractionSchema.Fields.Where(f => f.Key is not ("date" or "supplier")).ToImmutableArray()) };
        // Gate 2 requires headers before its owned item row; printed total has a separate header slot.
        using (var update = new XLWorkbook(new MemoryStream(template)))
        {
            update.Worksheet("Invoice").Cell("A2").Value = "Printed total"; update.Worksheet("Invoice").Cell("B2").Value = 0;
            using var stream = new MemoryStream(); update.SaveAs(stream); template = stream.ToArray();
        }
        using var freshAnalysis = new MemoryStream(template);
        var structure = new TemplateStructureAnalyzer().Analyze(freshAnalysis, "synthetic.xlsx").Structure!;
        var layout = new ExcelTemplateLayout(1, structure.TemplateSha256, ImmutableArray.Create(new ExcelSheetLayout("Invoice",
            ImmutableArray.Create(new ExcelCellMapping(H("number"), "B1"), new ExcelCellMapping(H("total"), "B2")),
            new(5, 5, 10, ImmutableArray.Create(new ExcelColumnMapping(L("description"), 1), new ExcelColumnMapping(L("quantity"), 2),
                new ExcelColumnMapping(L("price"), 3), new ExcelColumnMapping(L("amount"), 4))))));
        var proposal = MappingProposal.Draft(structure, p, layout); var service = new HumanMappingConfirmationService();
        using var confirmStream = new MemoryStream(template); var confirmation = service.Confirm(confirmStream, "synthetic.xlsx", proposal, p, "mapping-reviewer");
        Assert.Empty(confirmation.Errors); Assert.NotNull(confirmation.Confirmation);
        using var convertStream = new MemoryStream(template); var converted = service.Convert(convertStream, "synthetic.xlsx", proposal, p, confirmation.Confirmation);
        Assert.Empty(converted.Errors); layout = converted.Layout!;
        var bound = p with { ExcelProjectionMap = p.ExcelProjectionMap with { ApprovedLayoutFingerprint = layout.Fingerprint() } };
        var observations = Invoice().Where(o => o.Field.Key is not ("date" or "supplier") && o.ObservationId != "total").ToArray();
        var result = Run(Context(bound), observations); var draft = Assert.Single(result.Documents).Document;
        Assert.False(new ExportEligibilityPolicy().Evaluate(draft, bound).CanExport);
        using var deniedStream = new MemoryStream(template);
        Assert.False(new ExcelTemplateProjector().Project(deniedStream, "synthetic.xlsx", draft, bound, layout).Success);
        var corrected = draft.Edit(draft.Header.Add("total", TypedFieldValue.FromDecimal(19.50m) with { Source = FieldSource.Manual }), draft.LineItems);
        Assert.Empty(new DocumentValidator().Validate(corrected, bound).Issues);
        // Separate explicit human operations, never actions of the extraction pipeline.
        var approved = corrected.CompleteReview("document-reviewer", bound).Approve("document-approver", bound);
        Assert.True(new ExportEligibilityPolicy().Evaluate(approved, bound).CanExport);
        using var exportStream = new MemoryStream(template); var export = new ExcelTemplateProjector().Project(exportStream, "synthetic.xlsx", approved, bound, layout);
        Assert.True(export.Success, string.Join(",", export.Errors.Select(e => e.Code)));
        using var reopened = new XLWorkbook(new MemoryStream(export.Workbook!)); var sheet = reopened.Worksheet("Invoice");
        Assert.Equal("00017", sheet.Cell("B1").GetString()); Assert.Equal(19.50m, sheet.Cell("B2").GetValue<decimal>());
        Assert.Equal("=literal", sheet.Cell("A5").GetString()); Assert.False(sheet.Cell("A5").HasFormula);
        Assert.Equal(2m, sheet.Cell("B5").GetValue<decimal>()); Assert.Equal("B6*C6", sheet.Cell("E6").FormulaA1);
        Assert.Equal("SUM(E5:E6)", sheet.Cell("E7").FormulaA1); Assert.Equal("0.00", sheet.Cell("B6").Style.NumberFormat.Format);
        Assert.Equal(XLBorderStyleValues.Thin, sheet.Cell("B6").Style.Border.BottomBorder);
        Assert.Contains(sheet.MergedRanges, r => r.RangeAddress.ToString() == "A8:C8"); Assert.Equal("Reference preserved", reopened.Worksheet("Reference").Cell("A1").GetString());
        var edited = approved.Edit(approved.Header.SetItem("number", TypedFieldValue.FromText("changed") with { Source = FieldSource.Manual }), approved.LineItems);
        Assert.Null(edited.Approval); Assert.Null(edited.ReviewEvidence); Assert.False(new ExportEligibilityPolicy().Evaluate(edited, bound).CanExport);
    }
}
