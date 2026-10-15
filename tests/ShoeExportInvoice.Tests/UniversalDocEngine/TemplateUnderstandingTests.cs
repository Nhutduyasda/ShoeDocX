using System.Collections.Immutable;
using System.Text.Json;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class TemplateUnderstandingTests
{
    private sealed record Example(byte[] Bytes, DocumentWorkflowProfile Profile, ApprovedLabelSynonyms Synonyms,
        int Start, string Name, ImmutableArray<string> Keys);
    private static FieldReference H(string key) => new(FieldScope.Header, key);
    private static FieldReference L(string key) => new(FieldScope.LineItem, key);
    private static Example Create(string kind = "Accounting")
    {
        using var wb = new XLWorkbook(); var ws = wb.AddWorksheet(kind);
        ws.Range("A1:H1").Merge().Value = kind == "Warehouse" ? "Phiếu nhập kho" : "Synthetic " + kind;
        ws.Cell("A3").Value = kind == "Warehouse" ? "Số phiếu" : "Invoice number";
        ws.Cell("A3").Style.Font.Bold = true;
        ws.Cell("B3").Value = "sample number";
        ws.Cell("A5").Value = kind == "Warehouse" ? "Ngày" : "Date";
        ws.Cell("A5").Style.Font.Bold = true;
        ws.Cell("B5").Value = new DateTime(2026, 10, 15);
        ws.Cell("B5").Style.DateFormat.Format = "yyyy-mm-dd";
        string[] keys, labels;
        var start = kind == "Warehouse" ? 9 : kind == "Commercial" ? 12 : 11;
        if (kind == "Warehouse")
        { keys = new[] { "ItemCode", "Description", "Unit", "Quantity", "Notes" };
          labels = new[] { "Mã vật tư", "Tên vật tư", "Đơn vị tính", "Số lượng", "Ghi chú" }; }
        else
        { keys = new[] { "ItemCode", "Description", "Unit", "Quantity", "UnitPrice" };
          labels = new[] { "Item code", "Description", "Unit", "Quantity", "Unit price" }; }
        for (var i = 0; i < keys.Length; i++)
        {
            ws.Cell(start - 1, i + 1).Value = labels[i];
            ws.Cell(start - 1, i + 1).Style.Font.Bold = true;
            if (keys[i] is "Quantity" or "UnitPrice") ws.Cell(start, i + 1).Value = 99;
            else ws.Cell(start, i + 1).Value = "old sample";
            ws.Cell(start, i + 1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
        ws.Cell(start, 4).Style.NumberFormat.Format = "0.00";
        ws.Cell(start, 6).FormulaA1 = $"D{start}*E{start}";
        if (kind == "Warehouse") ws.Cell(start, 6).FormulaA1 = $"D{start}*2";
        ws.Cell(start + 1, 1).Value = kind == "Warehouse" ? "Tổng cộng" : "Total";
        ws.Cell(start + 1, 6).FormulaA1 = $"SUM(F{start}:F{start})";
        ws.Range(start + 2, 1, start + 2, 3).Merge().Value = "Signature";
        if (kind == "Commercial")
        {
            ws.Cell("H5").Value = 2;
            ws.Cell(start, 7).FormulaA1 = $"$H$5*D{start}";
            ws.Cell(start, 8).FormulaA1 = $"$D{start}*H$5";
            wb.AddWorksheet("Reference").Cell("B2").Value = "Synthetic reference only";
        }
        var fields = ImmutableArray.Create(new FieldDefinition("Number", FieldScope.Header, FieldType.Text, true),
            new FieldDefinition("Date", FieldScope.Header, FieldType.Date, true));
        foreach (var key in keys) fields = fields.Add(new(key, FieldScope.LineItem,
            key is "Quantity" or "UnitPrice" ? FieldType.Decimal : FieldType.Text, key != "Notes"));
        var profile = new DocumentWorkflowProfile("generic-" + kind, "synthetic-tenant", 1, new(1, fields),
            ImmutableArray<EnrichmentRule>.Empty, ImmutableArray<ComputationRule>.Empty,
            new(ImmutableArray<ArithmeticCheck>.Empty, ImmutableArray<CrossFootCheck>.Empty),
            new(ImmutableArray<ExcelMapping>.Empty));
        var aliases = ImmutableArray.Create(new FieldAliases(H("Number"), ImmutableArray.Create("Invoice number", "Số phiếu")),
            new FieldAliases(H("Date"), ImmutableArray.Create("Date", "Ngày")));
        for (var i = 0; i < keys.Length; i++) aliases = aliases.Add(new(L(keys[i]), ImmutableArray.Create(labels[i])));
        using var data = new MemoryStream(); wb.SaveAs(data);
        return new(data.ToArray(), profile, new("approved-test-vocabulary-v1", aliases), start, kind, keys.ToImmutableArray());
    }
    private static TemplateStructure Analyze(Example e)
    {
        using var data = new MemoryStream(e.Bytes);
        var result = new TemplateStructureAnalyzer().Analyze(data, "synthetic.xlsx");
        Assert.True(result.Success, string.Join(",", result.Errors.Select(x => x.Code)));
        return result.Structure!;
    }
    private static MappingProposal Draft(Example e)
    {
        var structure = Analyze(e); var service = new TemplateMappingSuggester();
        var suggestions = service.Suggest(structure, e.Profile, e.Synonyms);
        Assert.Empty(suggestions.Errors);
        Assert.All(suggestions.Suggestions, s => Assert.Equal(SuggestionStatus.UniqueCandidate, s.Status));
        return Assert.IsType<MappingProposal>(service.CreateDraft(suggestions));
    }
    private static ProposalValidationResult Validate(Example e, MappingProposal p, DocumentWorkflowProfile? profile = null)
    { using var stream = new MemoryStream(e.Bytes); return new MappingProposalValidator().Validate(stream, "template.xlsx", p, profile ?? e.Profile); }
    private static HumanMappingConfirmation Confirm(Example e, MappingProposal p)
    {
        using var stream = new MemoryStream(e.Bytes);
        var result = new HumanMappingConfirmationService().Confirm(stream, "template.xlsx", p, e.Profile, "named-mapping-reviewer");
        Assert.Empty(result.Errors); return Assert.IsType<HumanMappingConfirmation>(result.Confirmation);
    }
    private static ConfirmedLayoutResult Convert(Example e, MappingProposal p, HumanMappingConfirmation? c,
        DocumentWorkflowProfile? profile = null)
    { using var stream = new MemoryStream(e.Bytes); return new HumanMappingConfirmationService().Convert(stream, "template.xlsx", p, profile ?? e.Profile, c); }
    private static Example Modify(Example e, Action<IXLWorksheet> change)
    {
        using var wb = new XLWorkbook(new MemoryStream(e.Bytes)); change(wb.Worksheet(e.Name));
        using var stream = new MemoryStream(); wb.SaveAs(stream); return e with { Bytes = stream.ToArray() };
    }

    [Theory] [InlineData("Accounting")] [InlineData("Warehouse")] [InlineData("Commercial")]
    public void AnalysisSuggestionsConfirmationAndGate2ExportEndToEnd(string kind)
    {
        var e = Create(kind); var analysis = Analyze(e); var p = Draft(e);
        Assert.Contains(analysis.Sheets[0].Tables, t => t.HeaderRow == e.Start - 1 && t.PrototypeRows.SequenceEqual(new[] { e.Start }));
        Assert.Contains(e.Start + 1, analysis.Sheets[0].FooterRows);
        Assert.Contains(analysis.Sheets[0].Cells, c => c.Formula == $"D{e.Start}*E{e.Start}" || kind == "Warehouse" && c.Formula == $"D{e.Start}*2");
        Assert.True(Validate(e, p).IsValid);
        var confirmation = Confirm(e, p); var conversion = Convert(e, p, confirmation);
        Assert.Empty(conversion.Errors); var layout = Assert.IsType<ExcelTemplateLayout>(conversion.Layout);
        Assert.Equal("named-mapping-reviewer", confirmation.ReviewerId);
        Assert.Null(e.Profile.ExcelProjectionMap.ApprovedLayoutFingerprint);
        var bound = e.Profile with { ExcelProjectionMap = e.Profile.ExcelProjectionMap with { ApprovedLayoutFingerprint = layout.Fingerprint() } };
        using (var wb = new XLWorkbook(new MemoryStream(e.Bytes)))
            Assert.Empty(new ExcelLayoutValidator().Validate(wb, layout, bound, 3));
        var header = ImmutableDictionary<string, TypedFieldValue>.Empty.Add("Number", TypedFieldValue.FromText("=literal invoice"))
            .Add("Date", TypedFieldValue.FromDate(new DateOnly(2026, 10, 15)));
        var row = ImmutableDictionary<string, TypedFieldValue>.Empty;
        foreach (var field in bound.ExtractionSchema.Fields.Where(f => f.Scope == FieldScope.LineItem))
            row = row.Add(field.Key, field.Type == FieldType.Decimal ? TypedFieldValue.FromDecimal(2.5m) :
                TypedFieldValue.FromText(field.Key == "Description" ? "Vật tư Việt Nam" : field.Key == "Notes" ? null : "MAT-001"));
        var document = new CanonicalDocument("synthetic-document", bound.TenantId, bound.ProfileId, bound.Version,
            header, Enumerable.Repeat(row, 3).ToImmutableArray());
        using (var input = new MemoryStream(e.Bytes))
            Assert.False(new ExcelTemplateProjector().Project(input, "test.xlsx", document, bound, layout).Success);
        document = document.Review(DocumentReviewState.NeedsReview).CompleteReview("business-reviewer", bound).Approve("business-approver", bound);
        using var template = new MemoryStream(e.Bytes);
        var output = new ExcelTemplateProjector().Project(template, "test.xlsx", document, bound, layout);
        Assert.True(output.Success, string.Join(",", output.Errors.Select(x => x.Code)));
        using var reopened = new XLWorkbook(new MemoryStream(output.Workbook!)); var ws = reopened.Worksheet(e.Name);
        Assert.Equal("=literal invoice", ws.Cell("B3").GetString()); Assert.False(ws.Cell("B3").HasFormula);
        Assert.Equal(new DateTime(2026, 10, 15), ws.Cell("B5").GetDateTime());
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal("Vật tư Việt Nam", ws.Cell(e.Start + i, 2).GetString());
            Assert.Equal(2.5, ws.Cell(e.Start + i, 4).GetDouble());
            Assert.Equal("0.00", ws.Cell(e.Start + i, 4).Style.NumberFormat.Format);
            Assert.Equal(XLBorderStyleValues.Thin, ws.Cell(e.Start + i, 4).Style.Border.BottomBorder);
            Assert.Equal(kind == "Warehouse" ? $"D{e.Start + i}*2" : $"D{e.Start + i}*E{e.Start + i}", ws.Cell(e.Start + i, 6).FormulaA1);
            if (kind == "Commercial")
            { Assert.Equal($"$H$5*D{e.Start + i}", ws.Cell(e.Start + i, 7).FormulaA1); Assert.Equal($"$D{e.Start + i}*H$5", ws.Cell(e.Start + i, 8).FormulaA1); }
            if (kind == "Warehouse") Assert.True(ws.Cell(e.Start + i, 5).IsEmpty());
        }
        Assert.Equal($"SUM(F{e.Start}:F{e.Start + 2})", ws.Cell(e.Start + 3, 6).FormulaA1);
        Assert.Equal("Signature", ws.Cell(e.Start + 4, 1).GetString()); Assert.True(ws.Cell(e.Start + 4, 1).IsMerged());
        if (kind == "Commercial") Assert.Equal("Synthetic reference only", reopened.Worksheet("Reference").Cell("B2").GetString());
    }
    [Fact] public void AnalysisIsDeterministicAndPreservesSourceAndPosition()
    {
        var e = Create(); using var input = new MemoryStream(e.Bytes); input.Position = 17;
        var service = new TemplateStructureAnalyzer(); var a = service.Analyze(input, "a.xlsx"); var b = service.Analyze(input, "a.xlsx");
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b)); Assert.Equal(17, input.Position); Assert.Equal(e.Bytes, input.ToArray());
    }
    [Theory] [InlineData("Warehouse")] [InlineData("Accounting")]
    public void VietnameseAndEnglishSynonymsHaveEvidenceAndRequireReview(string kind)
    {
        var e = Create(kind); var result = new TemplateMappingSuggester().Suggest(Analyze(e), e.Profile, e.Synonyms);
        Assert.All(result.Suggestions, s => { Assert.True(s.RequiresHumanReview); Assert.Equal(SuggestionStatus.UniqueCandidate, s.Status);
            Assert.All(s.Candidates, c => { Assert.NotEmpty(c.Evidence); Assert.NotEmpty(c.Explanation); Assert.NotEmpty(c.MatchedLabel); }); });
    }
    [Theory] [InlineData("header")] [InlineData("column")]
    public void AmbiguityIsRetainedAndNotAutoSelected(string kind)
    {
        var e = Modify(Create(), ws => {
            if (kind == "header") { ws.Cell("D3").Value = "Invoice number"; ws.Cell("D3").Style.Font.Bold = true; ws.Cell("E3").Value = "other sample"; }
            else { ws.Cell(10, 7).Value = "Quantity"; ws.Cell(10, 7).Style.Font.Bold = true; ws.Cell(11, 7).Value = 99; }
        });
        var service = new TemplateMappingSuggester(); var result = service.Suggest(Analyze(e), e.Profile, e.Synonyms);
        Assert.Contains(result.Suggestions, s => s.Status == SuggestionStatus.Ambiguous && s.Candidates.Length > 1); Assert.Null(service.CreateDraft(result));
    }
    [Fact] public void MissingLabelsRemainUnresolved()
    {
        var e = Modify(Create(), ws => ws.Cell("A3").Value = "Unrelated label"); var service = new TemplateMappingSuggester();
        var result = service.Suggest(Analyze(e), e.Profile, e.Synonyms); Assert.Contains(result.Suggestions, s => s.Source == H("Number") && s.Status == SuggestionStatus.Unresolved);
        Assert.Null(service.CreateDraft(result));
    }
    [Fact] public void ExplicitHumanCorrectionCanResolveAmbiguity()
    {
        var original = Create(); var chosen = Draft(original);
        var e = Modify(original, ws => { ws.Cell("D3").Value = "Invoice number"; ws.Cell("D3").Style.Font.Bold = true; ws.Cell("E3").Value = "another sample"; });
        var layout = chosen.Layout with { TemplateSha256 = ExcelTemplateLayout.TemplateIdentity(e.Bytes) };
        var corrected = MappingProposal.Draft(Analyze(e), e.Profile, layout);
        Assert.True(Validate(e, corrected).IsValid); Assert.NotNull(Convert(e, corrected, Confirm(e, corrected)).Layout);
    }
    [Theory] [InlineData("unknown")] [InlineData("scope")] [InlineData("duplicate")]
    [InlineData("coordinate")] [InlineData("sheet")] [InlineData("collision")]
    [InlineData("formula")] [InlineData("prototype")] [InlineData("max")]
    public void InvalidFinalMappingsAreRejected(string kind)
    {
        var e = Create(); var p = Draft(e); var sheet = p.Layout.Sheets[0];
        sheet = kind switch {
            "unknown" => sheet with { Headers = sheet.Headers.SetItem(0, new(H("Unknown"), "B3")) },
            "scope" => sheet with { Headers = sheet.Headers.SetItem(0, new(L("Quantity"), "B3")) },
            "duplicate" => sheet with { Headers = sheet.Headers.Add(sheet.Headers[0]) },
            "coordinate" => sheet with { Headers = sheet.Headers.SetItem(0, new(H("Number"), "XFE0")) },
            "sheet" => sheet with { Worksheet = "NotPresent" },
            "collision" => sheet with { Headers = sheet.Headers.SetItem(0, new(H("Number"), "A11")) },
            "formula" => sheet with { Items = sheet.Items with { Columns = sheet.Items.Columns.Add(new(L("Quantity"), 6)) } },
            "prototype" => sheet with { Items = sheet.Items with { StartRow = 12, PrototypeRow = 12 } },
            _ => sheet with { Items = sheet.Items with { MaximumRows = 1001 } }
        };
        p = p with { Layout = p.Layout with { Sheets = ImmutableArray.Create(sheet) } };
        Assert.False(Validate(e, p).IsValid);
        using var input = new MemoryStream(e.Bytes);
        Assert.Null(new HumanMappingConfirmationService().Confirm(input, "a.xlsx", p, e.Profile, "reviewer").Confirmation);
    }
    [Fact] public void MultipleSampleRowsAreUnsupportedRatherThanOverwritten()
    {
        var original = Create(); var p = Draft(original);
        var e = Modify(original, ws => { ws.Row(12).InsertRowsAbove(1); ws.Row(11).CopyTo(ws.Row(12)); });
        p = MappingProposal.Draft(Analyze(e), e.Profile, p.Layout with { TemplateSha256 = ExcelTemplateLayout.TemplateIdentity(e.Bytes) });
        Assert.Contains(Validate(e, p).Errors, x => x.Code == "AMBIGUOUS_OR_INVALID_PROTOTYPE");
    }
    [Theory] [InlineData("IF(A1,1,0)")] [InlineData("Other!A1")] [InlineData("A1A2")]
    public void UnsupportedFormulaStructuresFail(string formula)
    {
        var e = Modify(Create(), ws => ws.Cell("H2").FormulaA1 = formula);
        using var input = new MemoryStream(e.Bytes); Assert.Contains(new TemplateStructureAnalyzer().Analyze(input, "a.xlsx").Errors, x => x.Code == "UNSUPPORTED_FORMULA");
    }
    [Theory] [InlineData("revision")] [InlineData("mapping")] [InlineData("schema")] [InlineData("profile")]
    [InlineData("template")]
    public void TamperingInvalidatesConfirmation(string kind)
    {
        var e = Create(); var p = Draft(e); var confirmation = Confirm(e, p); var profile = e.Profile;
        if (kind == "revision") p = p with { Revision = 2 };
        if (kind == "mapping") p = p with { Layout = p.Layout with { Sheets = p.Layout.Sheets.SetItem(0,
            p.Layout.Sheets[0] with { Items = p.Layout.Sheets[0].Items with { MaximumRows = 99 } }) } };
        if (kind == "schema") profile = profile with { ExtractionSchema = profile.ExtractionSchema with { Version = 2 } };
        if (kind == "profile") profile = profile with { Version = 2 };
        if (kind == "template") e = Modify(e, ws => ws.Cell("H3").Value = "changed");
        Assert.Null(Convert(e, p, confirmation, profile).Layout);
    }
    [Fact] public void RevisedProposalRequiresANewConfirmation()
    {
        var e = Create(); var p = Draft(e); var old = Confirm(e, p); var revised = p.Revise(p.Layout, p.OmittedOptionalFields);
        Assert.Null(Convert(e, revised, old).Layout); Assert.NotNull(Convert(e, revised, Confirm(e, revised)).Layout);
    }
    [Fact] public void CandidateOrValidProposalIsNotAConfirmation()
    {
        var e = Create(); var p = Draft(e); Assert.True(Validate(e, p).IsValid);
        Assert.Null(Convert(e, p, null).Layout);
        using var input = new MemoryStream(e.Bytes);
        Assert.Null(new HumanMappingConfirmationService().Confirm(input, "a.xlsx", p, e.Profile, " ").Confirmation);
    }
    [Fact] public void OptionalUnmappedFieldsMustBeExplicitlyOmitted()
    {
        var e = Create("Warehouse"); var p = Draft(e); var sheet = p.Layout.Sheets[0];
        p = p with { Layout = p.Layout with { Sheets = ImmutableArray.Create(sheet with {
            Items = sheet.Items with { Columns = sheet.Items.Columns.Where(c => c.Source != L("Notes")).ToImmutableArray() } }) } };
        Assert.Contains(Validate(e, p).Errors, x => x.Code == "UNRESOLVED_FIELD");
        Assert.True(Validate(e, p with { OmittedOptionalFields = ImmutableArray.Create(L("Notes")) }).IsValid);
        Assert.False(Validate(e, p with { OmittedOptionalFields = ImmutableArray.Create(L("Quantity")) }).IsValid);
    }
    [Fact] public void UnapprovedOrUnknownSynonymConfigurationIsRejected()
    {
        var e = Create(); var service = new TemplateMappingSuggester();
        Assert.NotEmpty(service.Suggest(Analyze(e), e.Profile, e.Synonyms with { ApprovalReference = "" }).Errors);
        Assert.NotEmpty(service.Suggest(Analyze(e), e.Profile, e.Synonyms with { Fields = e.Synonyms.Fields.Add(new(H("Unknown"), ImmutableArray.Create("Invoice"))) }).Errors);
    }
    [Theory] [InlineData("xls")] [InlineData("xlsm")]
    public void UnsupportedFileTypesFail(string extension)
    { using var input = new MemoryStream(Create().Bytes); Assert.False(new TemplateStructureAnalyzer().Analyze(input, "a." + extension).Success); }
    [Fact] public void CorruptedInputFailsSafely()
    { using var input = new MemoryStream(new byte[] { 1, 2, 3 }); Assert.False(new TemplateStructureAnalyzer().Analyze(input, "a.xlsx").Success); }
    [Fact] public void InputSizeLimitEnforced()
    { using var input = new MemoryStream(new byte[ExcelTemplateProjector.MaximumInputBytes + 1]); Assert.Contains(new TemplateStructureAnalyzer().Analyze(input, "a.xlsx").Errors, e => e.Code == "INPUT_LIMIT"); }
    [Fact] public void SparseAndStyleOnlyHugeRangesFailBeforeScanning()
    { var e = Modify(Create(), ws => ws.Cell("XFD1048576").Style.Font.Bold = true); using var input = new MemoryStream(e.Bytes);
      Assert.Contains(new TemplateStructureAnalyzer().Analyze(input, "a.xlsx").Errors, x => x.Code == "ANALYSIS_RANGE_LIMIT"); }
    [Theory] [InlineData("merge")] [InlineData("table")]
    public void UnsupportedEditedMergesAndTablesFail(string kind)
    {
        var e = Create(); var p = Draft(e); e = Modify(e, ws => { if (kind == "merge") ws.Range("A11:B11").Merge(); else ws.Range("A10:E11").CreateTable(); });
        var hash = ExcelTemplateLayout.TemplateIdentity(e.Bytes); p = p with { TemplateSha256 = hash, Layout = p.Layout with { TemplateSha256 = hash } };
        Assert.False(Validate(e, p).IsValid);
    }
    [Theory] [InlineData("xl/vbaProject.bin")] [InlineData("xl/connections.xml")] [InlineData("xl/externalLinks/link.xml")]
    public void DangerousPackagePartsAreNotProcessed(string part)
    {
        var e = Create(); using var bytes = new MemoryStream(); bytes.Write(e.Bytes);
        using (var zip = new System.IO.Compression.ZipArchive(bytes, System.IO.Compression.ZipArchiveMode.Update, true))
        { using var writer = new StreamWriter(zip.CreateEntry(part).Open()); writer.Write("synthetic"); }
        bytes.Position = 0; Assert.Contains(new TemplateStructureAnalyzer().Analyze(bytes, "a.xlsx").Errors, x => x.Code == "UNSUPPORTED_PACKAGE_PART");
    }
    [Theory] [InlineData("SỐ LƯỢNG:")] [InlineData("so luong")]
    public void ApprovedVietnameseSynonymsNormalizeCaseAccentsAndPunctuation(string label)
    {
        var e = Modify(Create("Warehouse"), ws => ws.Cell(8, 4).Value = label);
        Assert.NotNull(new TemplateMappingSuggester().CreateDraft(new TemplateMappingSuggester().Suggest(Analyze(e), e.Profile, e.Synonyms)));
    }
    [Fact] public void DuplicateItemColumnsAreRejected()
    {
        var e = Create(); var p = Draft(e); var sheet = p.Layout.Sheets[0];
        p = p with { Layout = p.Layout with { Sheets = ImmutableArray.Create(sheet with {
            Items = sheet.Items with { Columns = sheet.Items.Columns.SetItem(1, new(L("Description"), 1)) } }) } };
        Assert.Contains(Validate(e, p).Errors, x => x.Code == "OVERLAPPING_MAPPING");
    }
    [Fact] public void SpoofedProposalTemplateHashIsRejected()
    {
        var e = Create(); var p = Draft(e) with { TemplateSha256 = new string('0', 64) };
        Assert.Contains(Validate(e, p).Errors, x => x.Code == "TEMPLATE_IDENTITY_MISMATCH");
    }

}
