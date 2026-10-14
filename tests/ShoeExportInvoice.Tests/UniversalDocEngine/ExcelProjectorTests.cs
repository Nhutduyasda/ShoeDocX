using System.Collections.Immutable;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Configuration;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Excel;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class ExcelProjectorTests
{
    private sealed record Scenario(byte[] Bytes, ExcelTemplateLayout Layout, DocumentWorkflowProfile Profile,
        CanonicalDocument Document);
    private static FieldReference H(string key) => new(FieldScope.Header, key);
    private static FieldReference L(string key) => new(FieldScope.LineItem, key);
    private static Scenario Create(string name = "Accounting", int count = 3, bool finalApproval = true)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(name);
        ws.Cell("A1").Value = "Synthetic " + name;
        ws.Range("A2:B2").Merge().Value = "Mẫu thử";
        ws.Cell("B3").Value = 2;
        ws.Cell("C3").Value = 3;
        ws.Cell("A4").Value = "INVOICE";
        ws.Cell("B4").Value = "DATE";
        ws.Cell("B4").Style.DateFormat.Format = "yyyy-mm-dd";
        ws.Cell("A11").Value = "sample";
        ws.Cell("B11").Value = "sample unit";
        ws.Cell("C11").Value = "sample note";
        ws.Cell("D11").Value = 99;
        ws.Cell("E11").Value = 99;
        ws.Cell("F11").FormulaA1 = "D11*E11";
        ws.Cell("G11").FormulaA1 = "$B$3*D11";
        ws.Cell("H11").FormulaA1 = "$B11*C$3";
        ws.Cell("I11").Value = "old sample data";
        ws.Row(11).Height = 27;
        ws.Range("A11:I11").Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Cell("E11").Style.NumberFormat.Format = "#,##0.00";
        ws.Cell("A12").Value = "Footer " + name;
        ws.Cell("F12").FormulaA1 = "SUM(F11:F11)";
        if (name == "Packing") wb.AddWorksheet("Reference").Cell("A1").Value = "Untouched";
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var bytes = stream.ToArray();
        var layout = new ExcelTemplateLayout(1, ExcelTemplateLayout.TemplateIdentity(bytes),
            ImmutableArray.Create(new ExcelSheetLayout(name,
                ImmutableArray.Create(new ExcelCellMapping(H("number"), "A4"),
                    new ExcelCellMapping(H("date"), "B4"), new ExcelCellMapping(H("flag"), "C4")),
                new ExcelItemRegion(11, 11, 100, ImmutableArray.Create(
                    new ExcelColumnMapping(L("description"), 1), new ExcelColumnMapping(L("note"), 3),
                    new ExcelColumnMapping(L("quantity"), 4), new ExcelColumnMapping(L("price"), 5))))));
        var profile = new DocumentWorkflowProfile("excel", "synthetic", 1,
            new(1, ImmutableArray.Create(new FieldDefinition("number", FieldScope.Header, FieldType.Text, true),
                new FieldDefinition("date", FieldScope.Header, FieldType.Date, true),
                new FieldDefinition("flag", FieldScope.Header, FieldType.Boolean),
                new FieldDefinition("description", FieldScope.LineItem, FieldType.Text, true),
                new FieldDefinition("note", FieldScope.LineItem, FieldType.Text),
                new FieldDefinition("quantity", FieldScope.LineItem, FieldType.Decimal, true, 0),
                new FieldDefinition("price", FieldScope.LineItem, FieldType.Decimal, true))),
            ImmutableArray<EnrichmentRule>.Empty, ImmutableArray<ComputationRule>.Empty,
            new(ImmutableArray<ArithmeticCheck>.Empty, ImmutableArray<CrossFootCheck>.Empty,
                RequireHumanApproval: finalApproval),
            new(ImmutableArray<ExcelMapping>.Empty, layout.Fingerprint()));
        var header = ImmutableDictionary<string, TypedFieldValue>.Empty
            .Add("number", TypedFieldValue.FromText("VN-001"))
            .Add("date", TypedFieldValue.FromDate(new DateOnly(2026, 10, 14)))
            .Add("flag", TypedFieldValue.FromBoolean(true));
        var row = ImmutableDictionary<string, TypedFieldValue>.Empty
            .Add("description", TypedFieldValue.FromText("Nguyên liệu Việt Nam"))
            .Add("quantity", TypedFieldValue.FromDecimal(2.5m))
            .Add("price", TypedFieldValue.FromDecimal(12.75m));
        var document = new CanonicalDocument("demo", "synthetic", "excel", 1, header,
            Enumerable.Repeat(row, count).ToImmutableArray());
        return new(bytes, layout, profile, Review(document, profile));
    }
    private static CanonicalDocument Review(CanonicalDocument d, DocumentWorkflowProfile p)
    {
        if (d.ReviewState != DocumentReviewState.NeedsReview) d = d.Review(DocumentReviewState.NeedsReview);
        d = d.CompleteReview("reviewer", p);
        return p.ValidationPolicy.RequireHumanApproval ? d.Approve("approver", p) : d;
    }
    private static ExcelProjectionResult Run(Scenario s, ExcelTemplateLayout? layout = null,
        DocumentWorkflowProfile? profile = null, CanonicalDocument? document = null, string filename = "template.xlsx")
    {
        using var input = new MemoryStream(s.Bytes);
        return new ExcelTemplateProjector().Project(input, filename, document ?? s.Document,
            profile ?? s.Profile, layout ?? s.Layout);
    }
    private static XLWorkbook Output(ExcelProjectionResult result)
    {
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Code + " " + e.Location)));
        return new XLWorkbook(new MemoryStream(result.Workbook!));
    }
    private static Scenario Rebind(Scenario s, ExcelTemplateLayout layout)
    {
        var p = s.Profile with { ExcelProjectionMap = s.Profile.ExcelProjectionMap with {
            ApprovedLayoutFingerprint = layout.Fingerprint() } };
        return s with { Layout = layout, Profile = p, Document = Review(
            s.Document.Edit(s.Document.Header, s.Document.LineItems), p) };
    }
    private static Scenario ModifyWorkbook(Scenario s, Action<XLWorkbook> change)
    {
        using var wb = new XLWorkbook(new MemoryStream(s.Bytes));
        change(wb);
        using var output = new MemoryStream(); wb.SaveAs(output);
        var bytes = output.ToArray();
        return Rebind(s with { Bytes = bytes }, s.Layout with { TemplateSha256 = ExcelTemplateLayout.TemplateIdentity(bytes) });
    }
    [Theory]
    [InlineData("Accounting", 1)] [InlineData("Accounting", 2)] [InlineData("Warehouse", 5)] [InlineData("Packing", 3)]
    public void SyntheticWorkbooksPreserveTypesFormulasStylesAndFooter(string name, int count)
    {
        var s = Create(name, count);
        using var wb = Output(Run(s)); var ws = wb.Worksheet(name);
        Assert.Equal("VN-001", ws.Cell("A4").GetString());
        Assert.Equal(new DateTime(2026, 10, 14), ws.Cell("B4").GetDateTime());
        Assert.True(ws.Cell("C4").GetBoolean());
        Assert.Equal("Synthetic " + name, ws.Cell("A1").GetString());
        Assert.True(ws.Range("A2:B2").IsMerged());
        for (var i = 0; i < count; i++)
        {
            var r = 11 + i;
            Assert.Equal("Nguyên liệu Việt Nam", ws.Cell(r, 1).GetString());
            Assert.Equal(2.5, ws.Cell(r, 4).GetDouble());
            Assert.Equal(12.75, ws.Cell(r, 5).GetDouble());
            Assert.Equal($"D{r}*E{r}", ws.Cell(r, 6).FormulaA1);
            Assert.Equal($"$B$3*D{r}", ws.Cell(r, 7).FormulaA1);
            Assert.Equal($"$B{r}*C$3", ws.Cell(r, 8).FormulaA1);
            Assert.True(ws.Cell(r, 3).IsEmpty()); Assert.True(ws.Cell(r, 9).IsEmpty());
            Assert.Equal("#,##0.00", ws.Cell(r, 5).Style.NumberFormat.Format);
            Assert.Equal(XLBorderStyleValues.Thin, ws.Cell(r, 5).Style.Border.BottomBorder);
            Assert.Equal(27, ws.Row(r).Height);
        }
        Assert.Equal("Footer " + name, ws.Cell(count + 11, 1).GetString());
        Assert.Equal($"SUM(F11:F{10 + count})", ws.Cell(count + 11, 6).FormulaA1);
        if (name == "Packing") Assert.Equal("Untouched", wb.Worksheet("Reference").Cell("A1").GetString());
    }
    [Theory] [InlineData("=1+1")] [InlineData("+SUM(A1)")] [InlineData("-1+2")] [InlineData("@SUM(A1)")]
    public void UntrustedTextStaysLiteral(string text)
    {
        var s = Create(); var d = s.Document.Edit(s.Document.Header.SetItem("number", TypedFieldValue.FromText(text)), s.Document.LineItems);
        using var wb = Output(Run(s, document: Review(d, s.Profile)));
        Assert.Equal(text, wb.Worksheet(1).Cell("A4").GetString());
        Assert.False(wb.Worksheet(1).Cell("A4").HasFormula);
        Assert.Equal(XLDataType.Text, wb.Worksheet(1).Cell("A4").DataType);
    }
    [Fact] public void InputBytesAndCursorRemainUnchanged()
    {
        var s = Create(); var original = s.Bytes.ToArray(); using var stream = new MemoryStream(s.Bytes); stream.Position = 7;
        Assert.True(new ExcelTemplateProjector().Project(stream, "a.xlsx", s.Document, s.Profile, s.Layout).Success);
        Assert.Equal(7, stream.Position); Assert.Equal(original, stream.ToArray());
    }
    [Fact] public void SemanticallyDeterministic()
    {
        var s = Create(); using var a = Output(Run(s)); using var b = Output(Run(s));
        Assert.Equal(a.Worksheet(1).CellsUsed().Select(c => (c.Address.ToString(), c.Value.ToString(), c.FormulaA1)),
            b.Worksheet(1).CellsUsed().Select(c => (c.Address.ToString(), c.Value.ToString(), c.FormulaA1)));
    }
    [Theory] [InlineData("missing")] [InlineData("edit")] [InlineData("profile")] [InlineData("approval")] [InlineData("blocking")]
    public void Gate1DenialsCannotBeBypassed(string kind)
    {
        var s = Create(); var d = s.Document; var p = s.Profile;
        if (kind == "missing") d = new CanonicalDocument("demo", "synthetic", "excel", 1, d.Header, d.LineItems);
        if (kind == "edit") d = d.Edit(d.Header, d.LineItems);
        if (kind == "profile") p = p with { Version = 2 };
        if (kind == "approval") d = d.Review(DocumentReviewState.NeedsReview).CompleteReview("r", p);
        if (kind == "blocking") d = Review(d.Edit(d.Header.Remove("number"), d.LineItems), p);
        Assert.False(Run(s, profile: p, document: d).Success);
    }
    [Fact] public void OptionalApprovalStillRequiresCompletedReview() => Assert.True(Run(Create(finalApproval: false)).Success);
    [Fact] public void LayoutAndTemplateSubstitutionDenied()
    {
        var s = Create(); Assert.False(Run(s, layout: s.Layout with { TemplateSha256 = new string('0', 64) }).Success);
        var changed = s.Layout with { Version = 2 };
        Assert.False(Run(s, layout: changed).Success);
        var p = s.Profile with { ExcelProjectionMap = s.Profile.ExcelProjectionMap with { ApprovedLayoutFingerprint = changed.Fingerprint() } };
        Assert.False(Run(s, layout: changed, profile: p).Success); // old review is stale too
    }
    [Theory] [InlineData("A0")] [InlineData("XFE1")] [InlineData("A1048577")] [InlineData("Items.Amount")] [InlineData("A1:B1")]
    public void InvalidCellGrammarDenied(string address)
    {
        var s = Create(); var sheet = s.Layout.Sheets[0];
        s = Rebind(s, s.Layout with { Sheets = ImmutableArray.Create(sheet with {
            Headers = ImmutableArray.Create(new ExcelCellMapping(H("number"), address)) }) });
        Assert.Contains(Run(s).Errors, e => e.Code == "INVALID_CELL");
    }
    [Theory] [InlineData("sheet")] [InlineData("overlap")] [InlineData("formula")] [InlineData("scope")]
    [InlineData("region")] [InlineData("max")] [InlineData("column")]
    public void InvalidMappingsDenied(string kind)
    {
        var s = Create(); var sheet = s.Layout.Sheets[0];
        sheet = kind switch {
            "sheet" => sheet with { Worksheet = "Absent" },
            "overlap" => sheet with { Headers = sheet.Headers.Add(sheet.Headers[0]) },
            "formula" => sheet with { Items = sheet.Items with { Columns = sheet.Items.Columns.Add(new(L("price"), 6)) } },
            "scope" => sheet with { Headers = ImmutableArray.Create(new ExcelCellMapping(L("price"), "A4")) },
            "region" => sheet with { Items = sheet.Items with { PrototypeRow = 12 } },
            "max" => sheet with { Items = sheet.Items with { MaximumRows = 2 } },
            _ => sheet with { Items = sheet.Items with { Columns = ImmutableArray.Create(new ExcelColumnMapping(L("price"), 16385)) } }
        };
        s = Rebind(s, s.Layout with { Sheets = ImmutableArray.Create(sheet) });
        Assert.False(Run(s).Success);
    }
    [Theory] [InlineData("macro.xlsm")] [InlineData("old.xls")]
    public void UnsupportedExtensionDenied(string name) => Assert.Contains(Run(Create(), filename: name).Errors, e => e.Code == "UNSUPPORTED_FILE_TYPE");
    [Fact] public void EmptyRowsDenied() => Assert.False(Run(Create(count: 0)).Success);
    [Fact] public void CorruptedWorkbookDenied()
    {
        var s = Create(); var bytes = new byte[] { 1, 2, 3 };
        s = Rebind(s with { Bytes = bytes }, s.Layout with { TemplateSha256 = ExcelTemplateLayout.TemplateIdentity(bytes) });
        Assert.False(Run(s).Success);
    }
    [Fact] public void InputLimitEnforced()
    {
        var s = Create(); using var stream = new MemoryStream(new byte[ExcelTemplateProjector.MaximumInputBytes + 1]);
        var result = new ExcelTemplateProjector().Project(stream, "a.xlsx", s.Document, s.Profile, s.Layout);
        Assert.Contains(result.Errors, e => e.Code == "INPUT_LIMIT");
    }
    [Theory] [InlineData("merge")] [InlineData("formula")] [InlineData("table")]
    public void UnsupportedStructuresDenied(string kind)
    {
        var s = ModifyWorkbook(Create(), wb => {
            var ws = wb.Worksheet(1);
            if (kind == "merge") ws.Range("A11:B11").Merge();
            if (kind == "formula") ws.Cell("J1").FormulaA1 = "HYPERLINK(\"https://example.com\")";
            if (kind == "table") ws.Range("A10:B11").CreateTable();
        });
        Assert.False(Run(s).Success);
    }
    [Fact] public void IndependentWarehouseLayoutAndMultipleMappedSheets()
    {
        var s = Create(count: 2);
        using var wb = new XLWorkbook();
        var warehouse = wb.AddWorksheet("Receipt");
        warehouse.Cell("D2").Value = "Receipt number";
        warehouse.Cell("A7").Value = "Code";
        warehouse.Cell("B7").Value = "Description";
        warehouse.Cell("C7").Value = "Unit";
        warehouse.Cell("D7").Value = "Quantity";
        warehouse.Cell("E7").Value = "Notes";
        warehouse.Cell("B8").Value = "sample material";
        warehouse.Cell("C8").Value = "sample unit";
        warehouse.Cell("A9").Value = "Warehouse keeper signature";
        var packing = wb.AddWorksheet("Commercial");
        packing.Cell("F3").Value = "Packing number";
        packing.Cell("B15").Value = "sample goods";
        packing.Cell("C15").Value = 1;
        packing.Cell("D15").Value = 1;
        packing.Cell("E15").FormulaA1 = "C15*D15";
        packing.Cell("E16").FormulaA1 = "SUM(E15:E15)";
        packing.Cell("B16").Value = "Total packing";
        packing.Cell("D15").Style.NumberFormat.Format = "0.000";
        wb.AddWorksheet("Instructions").Cell("B2").Value = "Synthetic reference";
        using var stream = new MemoryStream(); wb.SaveAs(stream);
        var bytes = stream.ToArray();
        var layout = new ExcelTemplateLayout(1, ExcelTemplateLayout.TemplateIdentity(bytes), ImmutableArray.Create(
            new ExcelSheetLayout("Receipt", ImmutableArray.Create(new ExcelCellMapping(H("number"), "D2")),
                new ExcelItemRegion(8, 8, 10, ImmutableArray.Create(new ExcelColumnMapping(L("code"), 1),
                    new ExcelColumnMapping(L("unit"), 3), new ExcelColumnMapping(L("description"), 2),
                    new ExcelColumnMapping(L("quantity"), 4), new ExcelColumnMapping(L("note"), 5)))),
            new ExcelSheetLayout("Commercial", ImmutableArray.Create(new ExcelCellMapping(H("number"), "F3")),
                new ExcelItemRegion(15, 15, 10, ImmutableArray.Create(new ExcelColumnMapping(L("description"), 2),
                    new ExcelColumnMapping(L("quantity"), 3), new ExcelColumnMapping(L("price"), 4))))));
        var profile = s.Profile with { ExtractionSchema = s.Profile.ExtractionSchema with {
            Fields = s.Profile.ExtractionSchema.Fields.Add(new("code", FieldScope.LineItem, FieldType.Text, true))
                .Add(new("unit", FieldScope.LineItem, FieldType.Text, true)) } };
        var lines = s.Document.LineItems.Select(row => row.Add("code", TypedFieldValue.FromText("MAT-001"))
            .Add("unit", TypedFieldValue.FromText("kg"))).ToImmutableArray();
        s = Rebind(s with { Bytes = bytes, Profile = profile,
            Document = s.Document.Edit(s.Document.Header, lines) }, layout);
        using var result = Output(Run(s));
        Assert.Equal("VN-001", result.Worksheet("Receipt").Cell("D2").GetString());
        Assert.Equal("Warehouse keeper signature", result.Worksheet("Receipt").Cell("A10").GetString());
        Assert.Equal("kg", result.Worksheet("Receipt").Cell("C9").GetString());
        Assert.Equal("MAT-001", result.Worksheet("Receipt").Cell("A8").GetString());
        Assert.Equal("Nguyên liệu Việt Nam", result.Worksheet("Receipt").Cell("B9").GetString());
        Assert.Equal("C16*D16", result.Worksheet("Commercial").Cell("E16").FormulaA1);
        Assert.Equal("SUM(E15:E16)", result.Worksheet("Commercial").Cell("E17").FormulaA1);
        Assert.Equal("0.000", result.Worksheet("Commercial").Cell("D16").Style.NumberFormat.Format);
        Assert.Equal("Synthetic reference", result.Worksheet("Instructions").Cell("B2").GetString());
    }
    [Fact] public void PrototypeReferencesBelowRegionFailExplicitly()
    {
        var s = ModifyWorkbook(Create(), wb => wb.Worksheet(1).Cell("F11").FormulaA1 = "D12*E12");
        Assert.Contains(Run(s).Errors, e => e.Code == "UNSUPPORTED_PROTOTYPE_REFERENCE");
    }

    [Theory] [InlineData("xl/vbaProject.bin")] [InlineData("xl/connections.xml")]
    [InlineData("xl/externalLinks/externalLink1.xml")] [InlineData("xl/drawings/drawing1.xml")]
    public void DangerousPackagePartsDenied(string part)
    {
        var s = Create(); using var stream = new MemoryStream(); stream.Write(s.Bytes);
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Update, true))
        { using var writer = new StreamWriter(zip.CreateEntry(part).Open()); writer.Write("synthetic unsupported part"); }
        var bytes = stream.ToArray();
        s = Rebind(s with { Bytes = bytes }, s.Layout with { TemplateSha256 = ExcelTemplateLayout.TemplateIdentity(bytes) });
        Assert.Contains(Run(s).Errors, e => e.Code == "UNSUPPORTED_PACKAGE_PART");
    }
    [Fact] public void ExternalRelationshipDenied()
    {
        var s = Create(); using var stream = new MemoryStream(); stream.Write(s.Bytes);
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Update, true))
        { using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/_rels/fake.xml.rels").Open());
          writer.Write("<Relationships><Relationship TargetMode=\"External\" Target=\"https://example.com\" /></Relationships>"); }
        var bytes = stream.ToArray();
        s = Rebind(s with { Bytes = bytes }, s.Layout with { TemplateSha256 = ExcelTemplateLayout.TemplateIdentity(bytes) });
        Assert.Contains(Run(s).Errors, e => e.Code == "UNSUPPORTED_EXTERNAL_OR_MACRO");
    }
    [Fact] public void OptionalHeaderPreservePolicyIsExplicit()
    {
        var s = Create(); var d = s.Document.Edit(s.Document.Header.Remove("flag"), s.Document.LineItems);
        s = s with { Document = Review(d, s.Profile) };
        s = ModifyWorkbook(s, wb => wb.Worksheet(1).Cell("C4").Value = "keep me");
        using (var blank = Output(Run(s))) Assert.True(blank.Worksheet(1).Cell("C4").IsEmpty());
        var sheet = s.Layout.Sheets[0];
        s = Rebind(s, s.Layout with { Sheets = ImmutableArray.Create(sheet with {
            Headers = sheet.Headers.SetItem(2, sheet.Headers[2] with { Missing = MissingValuePolicy.Preserve }) }) });
        using var kept = Output(Run(s)); Assert.Equal("keep me", kept.Worksheet(1).Cell("C4").GetString());
    }
    [Fact] public void ExcessiveExcelNumericPrecisionDenied()
    {
        var s = Create(); var d = s.Document.Edit(s.Document.Header, s.Document.LineItems.SetItem(0,
            s.Document.LineItems[0].SetItem("price", TypedFieldValue.FromDecimal(1234567890123456m))));
        Assert.Contains(Run(s, document: Review(d, s.Profile)).Errors, e => e.Code == "EXCEL_NUMERIC_PRECISION");
    }
    [Fact] public void GlobalRowLimitCannotBeRaisedByConfiguration()
    {
        var s = Create(count: 1001);
        Assert.Contains(Run(s).Errors, e => e.Code == "ITEM_LIMIT");
    }

    [Fact] public void FooterMergeAndPrintSetupSurviveExpansion()
    {
        var s = ModifyWorkbook(Create(), wb => {
            var ws = wb.Worksheet(1); ws.Range("A13:D13").Merge().Value = "Signature";
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        });
        using var result = Output(Run(s)); var ws = result.Worksheet(1);
        Assert.Equal("Signature", ws.Cell("A15").GetString());
        Assert.True(ws.Range("A15:D15").IsMerged());
        Assert.Equal(XLPageOrientation.Landscape, ws.PageSetup.PageOrientation);
    }

    [Theory] [InlineData("SUMSUM(A1:A2)")] [InlineData("A1A2")] [InlineData("XFE1+1")]
    [InlineData("Other!A1")] [InlineData("SUM(A1:A2")] [InlineData("IF(A1,1,0)")]
    public void UnsupportedOrMalformedFormulaDenied(string formula)
    {
        var s = ModifyWorkbook(Create(), wb => wb.Worksheet(1).Cell("J1").FormulaA1 = formula);
        Assert.Contains(Run(s).Errors, e => e.Code == "UNSUPPORTED_FORMULA");
    }

}
