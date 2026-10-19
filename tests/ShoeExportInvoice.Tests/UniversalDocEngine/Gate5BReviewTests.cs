using System.Collections.Immutable;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
using static ShoeExportInvoice.Tests.UniversalDocEngine.Gate5BFixtures;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class Gate5BReviewTests
{
    private sealed class Actor : IUniversalDocCurrentActor
    {
        public UniversalDocActor Resolve() => new("owner", "tenant", "Admin", DateTimeOffset.UtcNow.AddHours(1));
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private static ImmutableArray<SchemaFieldCorrection> Corrections(StudioSessionView s) => s.SchemaDraft!.Fields.Select(f =>
        new SchemaFieldCorrection(f.Key, f.Label, f.Scope, f.Label == "Item ID" ? "Text" : "Decimal", false, f.Candidates[0].Worksheet != "Data1", 0)).ToImmutableArray();
    private static string Key(StudioSessionView s, string label) => s.SchemaDraft!.Fields.Single(f => !f.Omit && f.Label == label).Key;
    private static ImmutableArray<StudioValidationRule> Rules(StudioSessionView s) => [
        new("arithmetic", "Arithmetic", Key(s, "Quantity"), "Multiply", Key(s, "Price"), Key(s, "Amount"), 0, true, "BlockingError"),
        new("total", "CrossFoot", Key(s, "Amount"), null, null, Key(s, "Printed total"), 0, true, "BlockingError")];
    private static StudioSessionView Ready(UniversalDocWorkflowOrchestrator w, byte[]? bytes = null)
    {
        var s = w.Create("example-template"); s = w.Upload(s.Id, s.Version, bytes ?? Workbook(), "template.xlsx");
        return w.CorrectSchema(s.Id, s.Version, Corrections(s));
    }
    private static StudioSessionView Approved(UniversalDocWorkflowOrchestrator w, StudioSessionView s)
    {
        s = w.ChangeValidationRules(s.Id, s.Version, Rules(s)); s = w.ConfirmMapping(s.Id, s.Version);
        var row = ImmutableDictionary<string, string?>.Empty.Add(Key(s, "Item ID"), "0001").Add(Key(s, "Quantity"), "2.5").Add(Key(s, "Price"), "3").Add(Key(s, "Amount"), "7.5");
        s = w.SetManualSample(s.Id, s.Version, new(ImmutableDictionary<string, string?>.Empty.Add(Key(s, "Printed total"), "7.5"), [row, row.SetItem(Key(s, "Item ID"), "0002")]));
        // The printed total intentionally needs correction before review.
        Assert.Throws<StudioFault>(() => w.Review(s.Id, s.Version));
        s = w.EditField(s.Id, s.Version, "Header", Key(s, "Printed total"), null, "15");
        s = w.Review(s.Id, s.Version); return w.Approve(s.Id, s.Version);
    }
    [Theory] [InlineData("tolerance")] [InlineData("required")] [InlineData("severity")] [InlineData("operator")] [InlineData("remove")] [InlineData("reorder")]
    public void EveryMaterialRuleChangeInvalidatesExactReviewAndApproval(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var actor = new Actor(); var w = new UniversalDocWorkflowOrchestrator(actor, store);
        var s = Approved(w, Ready(w)); var before = store.Read(actor.Resolve(), s.Id); var rules = Rules(s);
        rules = kind switch {
            "tolerance" => rules.SetItem(0, rules[0] with { Tolerance = 0.1m }),
            "required" => rules.SetItem(0, rules[0] with { Required = false }),
            "severity" => rules.SetItem(0, rules[0] with { Severity = "Warning" }),
            "operator" => rules.SetItem(0, rules[0] with { Operator = "Add" }),
            "remove" => [], _ => rules.Reverse().ToImmutableArray() };
        s = w.ChangeValidationRules(s.Id, s.Version, rules); var after = store.Read(actor.Resolve(), s.Id);
        Assert.Equal(before.Profile.Version + 1, after.Profile.Version);
        Assert.NotEqual(WorkflowProfileIdentity.Fingerprint(before.Profile), WorkflowProfileIdentity.Fingerprint(after.Profile));
        Assert.Null(after.Confirmation); Assert.Null(after.Document!.Approval); Assert.Null(after.Document.ReviewEvidence);
        Assert.Equal("NeedsReview", s.ReviewState); Assert.NotNull(after.Validation);
        Assert.Equal("MAPPING_CONFIRMATION_REQUIRED", Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version)).Code);
    }
    [Fact] public void UnchangedRulesPreserveContentIdentityButAdvanceAndEnforceSessionVersion()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store);
        var s = Approved(w, Ready(w)); var original = s;
        s = w.ChangeValidationRules(s.Id, s.Version, Rules(s));
        Assert.Equal(original.Version + 1, s.Version); Assert.Equal(original.ProfileVersion, s.ProfileVersion); Assert.Equal("Approved", s.ReviewState);
        Assert.Equal("SESSION_VERSION_CONFLICT", Assert.Throws<StudioFault>(() => w.ChangeValidationRules(s.Id, original.Version, Rules(s))).Code);
        Assert.NotEmpty(w.Export(s.Id, s.Version).Workbook);
    }
    [Theory] [InlineData("rename")] [InlineData("omit")] [InlineData("type")]
    public void ReferencedSchemaChangesAreRejectedAtomically(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store);
        var s = Approved(w, Ready(w)); var fields = Corrections(s); var index = Array.FindIndex(fields.ToArray(), f => f.Key == Key(s, "Quantity"));
        fields = fields.SetItem(index, kind switch { "rename" => fields[index] with { Key = "renamed" }, "omit" => fields[index] with { Omit = true }, _ => fields[index] with { Type = "Text" } });
        Assert.Throws<StudioFault>(() => w.CorrectSchema(s.Id, s.Version, fields));
        Assert.Equal(s.Version, w.Read(s.Id).Version); Assert.Equal("Approved", w.Read(s.Id).ReviewState);
    }
    private sealed class SnapshotGate(IUniversalDocSessionStore inner, ManualResetEventSlim entered, ManualResetEventSlim release) : IUniversalDocSessionStore
    {
        public UniversalDocSession Create(UniversalDocActor actor, string workflow) => inner.Create(actor, workflow);
        public UniversalDocSession Read(UniversalDocActor actor, string id) { var s = inner.Read(actor, id); entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(20))); return s; }
        public UniversalDocSession Update(UniversalDocActor actor, string id, long version, Func<UniversalDocSession, UniversalDocSession> transition) => inner.Update(actor, id, version, transition);
        public void Remove(UniversalDocActor actor, string id, long version) => inner.Remove(actor, id, version);
        public void Cleanup() => inner.Cleanup();
    }
    [Theory] [InlineData("schema")] [InlineData("example")] [InlineData("expiry")] [InlineData("review")] [InlineData("reapproval")]
    public async Task ExportNeverReturnsBytesAfterSnapshotLosesAuthority(string kind)
    {
        var clock = new Clock(); using var store = new InMemoryUniversalDocSessionStore(clock); var actor = new Actor(); var w = new UniversalDocWorkflowOrchestrator(actor, store);
        var s = Approved(w, Ready(w)); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var export = new UniversalDocWorkflowOrchestrator(actor, new SnapshotGate(store, entered, release));
        var pending = Task.Run(() => Assert.Throws<StudioFault>(() => export.Export(s.Id, s.Version)));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(20)));
        try {
            switch (kind) {
                case "schema": w.CorrectSchema(s.Id, s.Version, Corrections(s)); break;
                case "example": w.UploadLearning(s.Id, s.Version, Workbook(2, 2, true), "example.xlsx", true); break;
                case "expiry": clock.Now += TimeSpan.FromMinutes(21); break;
                default: var reviewed = w.Review(s.Id, s.Version); if (kind == "reapproval") w.Approve(s.Id, reviewed.Version); break;
            }
        } finally { release.Set(); }
        Assert.Equal(kind == "expiry" ? "SESSION_EXPIRED" : "SESSION_VERSION_CONFLICT", (await pending).Code);
    }
    [Theory] [InlineData("AF1")] [InlineData("XFD1048576")] [InlineData("A1001")] [InlineData("B4")]
    public void ManualHeaderAddressesCannotEscapeAnalyzedCellsOrHeaderRegion(string address)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        var map = new StudioSheetMapping("Data1", 4, s.Mapping!.Headers.SetItem(Key(s, "Printed total"), address), s.Mapping.Columns);
        Assert.Throws<StudioFault>(() => w.ChangeMultisheetMapping(s.Id, s.Version, [map])); Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
    [Theory] [InlineData("relative")] [InlineData("absolute")] [InlineData("mixed")]
    public void SupportedReferenceGrammarSurvivesCorrespondenceAndRealExport(string kind)
    {
        string Formula(int row) => kind switch { "relative" => $"B{row}*C{row}", "absolute" => "$B$1+$C$1", _ => $"$B{row}*C$1+$B$1" };
        byte[] Make(int rows, bool filled) {
            using var wb = new XLWorkbook(new MemoryStream(Workbook(2, rows, filled)));
            foreach (var name in new[] { "Data1", "Data2" }) {
                var ws = wb.Worksheet(name); for (var row = 4; row < 4 + rows; row++) ws.Cell(row, 5).FormulaA1 = Formula(row);
                ws.Cell(4 + rows, 5).FormulaA1 = $"SUM($E$4:$E{3 + rows})";
            }
            using var data = new MemoryStream(); wb.SaveAs(data); return data.ToArray();
        }
        var original = Make(1, false); var example = Make(2, true);
        var analyzer = new TemplateStructureAnalyzer();
        var correspondence = new ExampleRowCorrespondence().Compare(original, example,
            analyzer.Analyze(new MemoryStream(original), "template.xlsx").Structure!, analyzer.Analyze(new MemoryStream(example), "example.xlsx").Structure!);
        Assert.True(correspondence.Supported, string.Join(", ", correspondence.Problems));
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store);
        var s = w.Create("example-template"); s = w.Upload(s.Id, s.Version, original, "template.xlsx"); s = w.UploadLearning(s.Id, s.Version, example, "example.xlsx", true);
        s = w.CorrectSchema(s.Id, s.Version, Corrections(s));
        s = w.ChangeMultisheetMapping(s.Id, s.Version, [new("Data1", 4, s.Mapping!.Headers, s.Mapping.Columns), new("Data2", 4, s.Mapping.Headers, s.Mapping.Columns)]);
        s = Approved(w, s); using var result = new XLWorkbook(new MemoryStream(w.Export(s.Id, s.Version).Workbook));
        foreach (var name in new[] { "Data1", "Data2" }) { Assert.Equal(Formula(5), result.Worksheet(name).Cell("E5").FormulaA1); Assert.Equal("SUM($E$4:$E5)", result.Worksheet(name).Cell("E6").FormulaA1); }
        Assert.Equal("Dữ liệu tham chiếu 00123", result.Worksheet("Reference").Cell("A1").GetString());
    }
    [Theory] [InlineData("absolute-end")] [InlineData("footer-arithmetic")]
    public void UnverifiedFooterTransformationsRemainUnsupportedAndAtomic(string kind)
    {
        byte[] Make(int rows, bool filled) {
            using var wb = new XLWorkbook(new MemoryStream(Workbook(2, rows, filled)));
            foreach (var name in new[] { "Data1", "Data2" }) wb.Worksheet(name).Cell(4 + rows, 5).FormulaA1 = kind == "absolute-end" ? $"SUM($E$4:$E${3 + rows})" : $"SUM(E4:E{3 + rows})+1";
            using var output = new MemoryStream(); wb.SaveAs(output); return output.ToArray();
        }
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w, Make(1, false));
        Assert.Equal("EXAMPLE_STRUCTURE_MISMATCH", Assert.Throws<StudioFault>(() => w.UploadLearning(s.Id, s.Version, Make(2, true), "example.xlsx", true)).Code);
        Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
}
