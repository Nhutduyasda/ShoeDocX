using static ShoeExportInvoice.Tests.UniversalDocEngine.Gate5BFixtures;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
using Xunit.Abstractions;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class Gate5BTests(ITestOutputHelper output)
{
    private sealed class Actor : IUniversalDocCurrentActor
    {
        public UniversalDocActor Value = new("owner", "tenant", "Admin", DateTimeOffset.UtcNow.AddHours(1));
        public UniversalDocActor Resolve() => Value;
    }
    private static ImmutableArray<SchemaFieldCorrection> Corrections(StudioSessionView s) => s.SchemaDraft!.Fields.Select(f =>
        new SchemaFieldCorrection(f.Key, f.Label, f.Scope, f.Label == "Item ID" ? "Text" : "Decimal", false,
            f.Candidates[0].Worksheet != "Data1", 0)).ToImmutableArray();
    private static StudioSessionView Ready(UniversalDocWorkflowOrchestrator w, int sheets = 2, int exampleRows = 0)
    {
        var s = w.Create("example-template"); s = w.Upload(s.Id, s.Version, Workbook(sheets), "template.xlsx");
        if (exampleRows > 0) s = w.UploadLearning(s.Id, s.Version, Workbook(sheets, exampleRows, true), "example.xlsx", true);
        return w.CorrectSchema(s.Id, s.Version, Corrections(s));
    }
    private static ImmutableArray<StudioSheetMapping> Mappings(StudioSessionView s, int count = 2) => Enumerable.Range(1, count)
        .Select(n => new StudioSheetMapping("Data" + n, 4, s.Mapping!.Headers, s.Mapping.Columns)).ToImmutableArray();
    private static string Key(StudioSessionView s, string label) => s.SchemaDraft!.Fields.Single(f => !f.Omit && f.Label == label).Key;
    private static ImmutableArray<StudioValidationRule> Rules(StudioSessionView s) => [
        new("arithmetic", "Arithmetic", Key(s, "Quantity"), "Multiply", Key(s, "Price"), Key(s, "Amount"), 0, true, "BlockingError"),
        new("total", "CrossFoot", Key(s, "Amount"), null, null, Key(s, "Printed total"), 0, true, "BlockingError")];
    private static ManualSample Sample(StudioSessionView s, int rows = 2, string amount = "7.5", string total = "15", string quantity = "2.5", string price = "3") => new(
        ImmutableDictionary<string, string?>.Empty.Add(Key(s, "Printed total"), total),
        Enumerable.Range(1, rows).Select(n => ImmutableDictionary<string, string?>.Empty.Add(Key(s, "Item ID"), "000" + n)
            .Add(Key(s, "Quantity"), quantity).Add(Key(s, "Price"), price).Add(Key(s, "Amount"), amount)).ToImmutableArray());
    [Theory] [InlineData(2)] [InlineData(3)]
    public void CompleteMultisheetWorkflowExportsRealWorkbookAndInvalidatesApproval(int count)
    {
        var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System);
        var w = new UniversalDocWorkflowOrchestrator(actor, store); var s = Ready(w, count, 2);
        var bytes = store.Read(actor.Value, s.Id).Template; var example = store.Read(actor.Value, s.Id).Example;
        var mapping = Mappings(s, count); s = w.ChangeMultisheetMapping(s.Id, s.Version, mapping);
        s = w.ChangeValidationRules(s.Id, s.Version, Rules(s)); s = w.ConfirmMapping(s.Id, s.Version);
        s = w.SetManualSample(s.Id, s.Version, Sample(s)); Assert.All(s.Checks.Where(check => check.Required), check => Assert.Equal("Passed", check.Outcome));
        s = w.Review(s.Id, s.Version); s = w.Approve(s.Id, s.Version);
        var timer = Stopwatch.StartNew(); var allocation = GC.GetAllocatedBytesForCurrentThread(); var exported = w.Export(s.Id, s.Version);
        output.WriteLine($"sheets={count}; rows=2; wall_ms={timer.Elapsed.TotalMilliseconds}; allocation_bytes={GC.GetAllocatedBytesForCurrentThread()-allocation}; output_bytes={exported.Workbook.Length}; generated_cells={count*5*2}");
        using var workbook = new XLWorkbook(new MemoryStream(exported.Workbook));
        Assert.Equal(Enumerable.Range(1, count).Select(n => "Data" + n).Append("Reference"), workbook.Worksheets.Select(ws => ws.Name));
        for (var n = 1; n <= count; n++)
        {
            var ws = workbook.Worksheet("Data" + n); Assert.Equal("0001", ws.Cell("A4").GetString()); Assert.Equal("0002", ws.Cell("A5").GetString());
            Assert.Equal(2.5m, ws.Cell("B5").GetValue<decimal>()); Assert.Equal(15m, ws.Cell("B1").GetValue<decimal>());
            Assert.Equal("B5*C5", ws.Cell("E5").FormulaA1); Assert.Equal("SUM(E4:E5)", ws.Cell("E6").FormulaA1);
            Assert.Equal("0.00", ws.Cell("B5").Style.NumberFormat.Format); Assert.Equal(XLBorderStyleValues.Thin, ws.Cell("B5").Style.Border.BottomBorder);
            Assert.Contains(ws.MergedRanges, range => range.RangeAddress.ToString() == "A7:C7");
        }
        Assert.Equal("Dữ liệu tham chiếu 00123", workbook.Worksheet("Reference").Cell("A1").GetString());
        s = w.Read(s.Id); s = w.ChangeValidationRules(s.Id, s.Version, Rules(s).Reverse().ToImmutableArray());
        Assert.Equal("NeedsReview", s.ReviewState); Assert.Null(store.Read(actor.Value, s.Id).Document!.Approval);
        Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version));
        Assert.Equal(bytes, store.Read(actor.Value, s.Id).Template); Assert.Equal(example, store.Read(actor.Value, s.Id).Example);
    }
    [Fact] public void DuplicateLabelsAcrossSheetsAreSeparateBusinessFields()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store);
        var s = w.Create("example-template"); s = w.Upload(s.Id, s.Version, Workbook(), "template.xlsx");
        var quantities = s.SchemaDraft!.Fields.Where(f => f.Label == "Quantity").ToArray();
        Assert.Equal(2, quantities.Length); Assert.NotEqual(quantities[0].Key, quantities[1].Key); Assert.All(quantities, f => Assert.Single(f.Candidates));
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(5)]
    public void ExampleCorrespondenceAcceptsSupportedContiguousRowsAndBindsHashes(int rows)
    {
        var original = Workbook(); var example = Workbook(2, rows, true); var originalCopy = original.ToArray(); var exampleCopy = example.ToArray();
        using var a = new MemoryStream(original); using var b = new MemoryStream(example);
        var analysis = new TemplateStructureAnalyzer();
        var result = new ExampleRowCorrespondence().Compare(original, example, analysis.Analyze(a, "a.xlsx").Structure!, analysis.Analyze(b, "b.xlsx").Structure!);
        Assert.True(result.Supported, string.Join(';', result.Problems)); Assert.All(result.Sheets, sheet => Assert.Equal(rows, sheet.ExampleRowCount));
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)), result.TemplateSha256);
        Assert.Equal(originalCopy, original); Assert.Equal(exampleCopy, example);
    }
    [Theory] [InlineData("header")] [InlineData("formula")] [InlineData("footer")] [InlineData("reference")] [InlineData("extra")] [InlineData("count")] [InlineData("six")]
    public void UnsupportedExamplesAreRejectedAtomically(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        using var workbook = new XLWorkbook(new MemoryStream(Workbook(2, kind == "six" ? 6 : 2, true))); var ws = workbook.Worksheet("Data1");
        if (kind == "header") ws.Cell("B3").Value = "Changed";
        if (kind == "formula") ws.Cell("E5").FormulaA1 = "B5*C5+1";
        if (kind == "footer") ws.Cell("E6").FormulaA1 = "SUM(E4:E4)";
        if (kind == "reference") workbook.Worksheet("Reference").Cell("A1").Value = "Changed";
        if (kind == "extra") ws.Cell("A5").Value = "Unrelated"; // bold row becomes a distinct table rather than repeated data
        if (kind == "extra") ws.Range("A5:B5").Style.Font.Bold = true;
        if (kind == "count") ws.Row(5).Delete();
        using var data = new MemoryStream(); workbook.SaveAs(data);
        Assert.Throws<StudioFault>(() => w.UploadLearning(s.Id, s.Version, data.ToArray(), "example.xlsx", true));
        Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
    [Theory] [InlineData("missing")] [InlineData("duplicate")] [InlineData("prototype")] [InlineData("formula")] [InlineData("merged")] [InlineData("header-only")] [InlineData("four")] [InlineData("far-header")]
    public void InvalidMultisheetMappingsCannotBeConfirmed(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w, 3);
        var maps = Mappings(s);
        maps = kind switch {
            "missing" => maps.SetItem(1, maps[1] with { Sheet = "Missing" }),
            "duplicate" => maps.SetItem(1, maps[1] with { Sheet = "Data1" }),
            "prototype" => maps.SetItem(1, maps[1] with { PrototypeRow = 7 }),
            "formula" => maps.SetItem(1, maps[1] with { Columns = maps[1].Columns.SetItem(Key(s, "Amount"), 5) }),
            "merged" => maps.SetItem(1, maps[1] with { Headers = maps[1].Headers.SetItem(Key(s, "Printed total"), "A6") }),
            "far-header" => maps.SetItem(1, maps[1] with { Headers = maps[1].Headers.SetItem(Key(s, "Printed total"), "XFD1") }),
            "header-only" => maps.SetItem(1, maps[1] with { Columns = ImmutableDictionary<string, int>.Empty }),
            _ => [maps[0], maps[1], maps[0] with { Sheet = "Data3" }, maps[0] with { Sheet = "Reference" }] };
        Assert.Throws<StudioFault>(() => w.ChangeMultisheetMapping(s.Id, s.Version, maps)); Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
    [Theory] [InlineData("missing-field")] [InlineData("wrong-type")] [InlineData("scope")] [InlineData("id")] [InlineData("duplicate")] [InlineData("tolerance")] [InlineData("operator")] [InlineData("nine")] [InlineData("self")]
    public void InvalidRulesAreRejectedWithoutChangingConfiguration(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w); var rules = Rules(s);
        rules = kind switch {
            "missing-field" => rules.SetItem(0, rules[0] with { FieldA = "missing" }),
            "wrong-type" => rules.SetItem(0, rules[0] with { FieldA = Key(s, "Item ID") }),
            "scope" => rules.SetItem(0, rules[0] with { Target = Key(s, "Printed total") }),
            "id" => rules.SetItem(0, rules[0] with { Id = rules[1].Id }),
            "duplicate" => rules.Add(rules[0] with { Id = "copy" }),
            "tolerance" => rules.SetItem(0, rules[0] with { Tolerance = 1.01m }),
            "operator" => rules.SetItem(0, rules[0] with { Operator = "eval()" }),
            "self" => rules.SetItem(0, rules[0] with { Target = rules[0].FieldA }),
            _ => Enumerable.Range(1, 9).Select(n => rules[0] with { Id = "r" + n }).ToImmutableArray() };
        Assert.Throws<StudioFault>(() => w.ChangeValidationRules(s.Id, s.Version, rules)); Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
    [Theory] [InlineData("amount")] [InlineData("total")] [InlineData("missing")] [InlineData("zero")] [InlineData("overflow")]
    public void RequiredChecksNeverPassInvalidOrUnevaluatedInputs(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w); var rules = Rules(s);
        if (kind == "zero") rules = rules.SetItem(0, rules[0] with { Operator = "Divide" });
        s = w.ChangeValidationRules(s.Id, s.Version, rules); s = w.ConfirmMapping(s.Id, s.Version);
        var sample = Sample(s, amount: kind == "amount" ? "8" : "7.5", total: kind == "total" ? "16" : "15",
            quantity: kind == "overflow" ? decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture) : "2.5", price: kind == "zero" ? "0" : "3");
        if (kind == "missing") sample = sample with { Rows = sample.Rows.SetItem(0, sample.Rows[0].Remove(Key(s, "Quantity"))) };
        s = w.SetManualSample(s.Id, s.Version, sample);
        Assert.Contains(s.Checks, c => c.Required && c.Outcome != "Passed");
        if (kind == "missing") Assert.Contains(s.Checks, c => c.Outcome == "NotEvaluated");
        Assert.Throws<StudioFault>(() => w.Review(s.Id, s.Version)); Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version));
    }
    [Theory] [InlineData("rules")] [InlineData("mapping")]
    public async Task ConcurrentConfigurationUpdatesHaveExactlyOneWinner(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => { try {
            if (kind == "rules") w.ChangeValidationRules(s.Id, s.Version, Rules(s)); else w.ChangeMultisheetMapping(s.Id, s.Version, Mappings(s));
            return 200; } catch (StudioFault f) { return f.Status; } })));
        Assert.Equal(new[] { 200, 409 }, results.Order());
    }
    [Theory] [InlineData("owner")] [InlineData("tenant")]
    public void NewConfigurationMutationsRespectPrivateSessionOwnership(string kind)
    {
        var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(actor, store); var s = Ready(w);
        var rules = Rules(s); var maps = Mappings(s); actor.Value = kind == "owner" ? actor.Value with { UserId = "other" } : actor.Value with { TenantId = "other" };
        Assert.Equal(404, Assert.Throws<StudioFault>(() => w.ChangeValidationRules(s.Id, s.Version, rules)).Status);
        Assert.Equal(404, Assert.Throws<StudioFault>(() => w.ChangeMultisheetMapping(s.Id, s.Version, maps)).Status);
    }
    [Theory] [InlineData("validation-rules")] [InlineData("multisheet-mapping")]
    public async Task RealHttpRejectsMissingCsrfAndForgedNestedAuthority(string endpoint)
    {
        await using var host = await StudioHttpHost.Start(); using var client = host.Client();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin-a", password = "Studio@Test2026!" }); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var denied = await client.PatchAsJsonAsync($"/api/universal-doc/sessions/any/{endpoint}", new { expectedVersion = 1 }); Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        var bootstrap = await client.GetFromJsonAsync<JsonElement>("/api/universal-doc/bootstrap"); client.DefaultRequestHeaders.Add("X-Studio-CSRF", bootstrap.GetProperty("csrfToken").GetString());
        using var created = await client.PostAsJsonAsync("/api/universal-doc/sessions", new { workflow = "example-template" });
        var session = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = session.GetProperty("id").GetString();
        object body = endpoint == "validation-rules" ? new { expectedVersion = 1, rules = new[] { new { id = "test", kind = "Arithmetic", fieldA = "a", fieldB = "b", target = "c", @operator = "Multiply", tolerance = 0, required = true, severity = "BlockingError", approval = true } } } :
            new { expectedVersion = 1, sheets = new[] { new { sheet = "Data1", prototypeRow = 4, headers = new { }, columns = new { a = 1 }, confirmed = true } } };
        using var forged = await client.PatchAsJsonAsync($"/api/universal-doc/sessions/{id}/{endpoint}", body); Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
    }
    [Theory] [InlineData("mapping")] [InlineData("rule-remove")] [InlineData("example")]
    public void MaterialConfigurationChangesInvalidateCurrentApproval(string kind)
    {
        var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(actor, store);
        var s = Ready(w); s = w.ChangeValidationRules(s.Id, s.Version, Rules(s)); s = w.ConfirmMapping(s.Id, s.Version);
        s = w.SetManualSample(s.Id, s.Version, Sample(s)); s = w.Review(s.Id, s.Version); s = w.Approve(s.Id, s.Version);
        s = kind switch {
            "mapping" => w.ChangeMultisheetMapping(s.Id, s.Version, Mappings(s)),
            "rule-remove" => w.ChangeValidationRules(s.Id, s.Version, []),
            _ => w.UploadLearning(s.Id, s.Version, Workbook(2, 2, true), "example.xlsx", true) };
        Assert.Null(store.Read(actor.Value, s.Id).Document?.Approval); Assert.Null(store.Read(actor.Value, s.Id).Document?.ReviewEvidence);
        Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version));
    }
    [Theory] [InlineData("profile")] [InlineData("example")]
    public void ConfigurationCannotConfirmTamperedBindings(string kind)
    {
        var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(actor, store);
        var s = Ready(w, 2, 2);
        var altered = store.Update(actor.Value, s.Id, s.Version, current => kind == "profile" ? current with { MappingProfile = current.MappingProfile with { Version = 999 } } : current with { Example = current.Example.SetItem(0, 0) });
        Assert.Throws<StudioFault>(() => w.ChangeValidationRules(s.Id, altered.Version, Rules(s)));
        Assert.Throws<StudioFault>(() => w.ChangeMultisheetMapping(s.Id, altered.Version, Mappings(s)));
    }
    [Theory] [InlineData("row-order")] [InlineData("no-shared-id")] [InlineData("shared-value")]
    public void EqualRowCountsNeverProveMatchingOrderedCollection(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store);
        var s = w.Create("example-template"); s = w.Upload(s.Id, s.Version, Workbook(), "template.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(Workbook(2, 2, true)));
        if (kind == "row-order") { workbook.Worksheet("Data2").Cell("A4").Value = "0002"; workbook.Worksheet("Data2").Cell("A5").Value = "0001"; }
        if (kind == "shared-value") workbook.Worksheet("Data2").Cell("B4").Value = 99;
        using var bytes = new MemoryStream(); workbook.SaveAs(bytes); s = w.UploadLearning(s.Id, s.Version, bytes.ToArray(), "example.xlsx", true);
        var corrections = Corrections(s);
        if (kind == "no-shared-id") corrections = corrections.Select(f => f with { Omit = false }).ToImmutableArray();
        s = w.CorrectSchema(s.Id, s.Version, corrections);
        if (kind != "no-shared-id") s = w.ChangeMultisheetMapping(s.Id, s.Version, Mappings(s));
        Assert.Equal(kind == "no-shared-id" ? "EXAMPLE_SHARED_ORDERED_IDENTIFIER_REQUIRED" : "EXAMPLE_SHARED_FIELD_VALUES_DIFFER", Assert.Throws<StudioFault>(() => w.ConfirmMapping(s.Id, s.Version)).Code);
    }
    [Theory] [InlineData("inside")] [InlineData("outside")]
    public void DecimalToleranceUsesExactBoundedPolicy(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        s = w.ChangeValidationRules(s.Id, s.Version, [Rules(s)[0] with { Tolerance = 0.01m }]); s = w.ConfirmMapping(s.Id, s.Version);
        s = w.SetManualSample(s.Id, s.Version, Sample(s, amount: kind == "inside" ? "7.51" : "7.511"));
        Assert.All(s.Checks.Where(c => c.Id.StartsWith("arithmetic:")), c => Assert.Equal(kind == "inside" ? "Passed" : "Failed", c.Outcome));
    }
    [Fact] public void SchemaCannotSilentlyRemapExistingRuleReferences()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        s = w.ChangeValidationRules(s.Id, s.Version, Rules(s)); var fields = Corrections(s);
        var index = Array.FindIndex(fields.ToArray(), f => f.Key == Key(s, "Quantity"));
        Assert.Throws<StudioFault>(() => w.CorrectSchema(s.Id, s.Version, fields.SetItem(index, fields[index] with { Key = "renamed" })));
        Assert.Equal(s.Version, w.Read(s.Id).Version);
    }

    [Theory] [InlineData(2)] [InlineData(3)]
    public async Task MaximumRowsRemainBoundedAcrossConcurrentPrivateSessions(int count)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System);
        async Task Verify(int index)
        {
            await Task.Yield(); var actor = new Actor(); actor.Value = actor.Value with { UserId = "owner" + index };
            var w = new UniversalDocWorkflowOrchestrator(actor, store); var s = Ready(w, count);
            s = w.ChangeMultisheetMapping(s.Id, s.Version, Mappings(s, count)); s = w.ChangeValidationRules(s.Id, s.Version, Rules(s));
            s = w.ConfirmMapping(s.Id, s.Version); s = w.SetManualSample(s.Id, s.Version, Sample(s, 20, total: "150"));
            s = w.Review(s.Id, s.Version); s = w.Approve(s.Id, s.Version);
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); var result = w.Export(s.Id, s.Version);
            output.WriteLine($"concurrent_session={index}; sheets={count}; rows=20; wall_ms={timer.Elapsed.TotalMilliseconds}; allocation_bytes={GC.GetAllocatedBytesForCurrentThread()-allocated}; output_bytes={result.Workbook.Length}; generated_cells={count*5*20}");
            using var workbook = new XLWorkbook(new MemoryStream(result.Workbook));
            foreach (var name in Enumerable.Range(1, count).Select(n => "Data" + n))
            { Assert.Equal("00020", workbook.Worksheet(name).Cell("A23").GetString()); Assert.Equal("SUM(E4:E23)", workbook.Worksheet(name).Cell("E24").FormulaA1); }
            s = w.Read(s.Id); Assert.Throws<StudioFault>(() => w.SetManualSample(s.Id, s.Version, Sample(s, 21)));
        }
        await Task.WhenAll(Enumerable.Range(1, 3).Select(n => Task.Run(() => Verify(n))));
    }
    private sealed class SnapshotGate(IUniversalDocSessionStore inner, ManualResetEventSlim entered, ManualResetEventSlim release) : IUniversalDocSessionStore
    {
        public UniversalDocSession Create(UniversalDocActor actor, string workflow) => inner.Create(actor, workflow);
        public UniversalDocSession Read(UniversalDocActor actor, string id) {
            var snapshot = inner.Read(actor, id); entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(20))); return snapshot;
        }
        public UniversalDocSession Update(UniversalDocActor actor, string id, long version, Func<UniversalDocSession, UniversalDocSession> transition) => inner.Update(actor, id, version, transition);
        public void Remove(UniversalDocActor actor, string id, long version) => inner.Remove(actor, id, version);
        public void Cleanup() => inner.Cleanup();
    }
    [Theory] [InlineData("rules")] [InlineData("mapping")]
    public async Task ExportCannotReturnWorkbookAfterConcurrentConfigurationChange(string kind)
    {
        var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(actor, store);
        var s = Ready(w); s = w.ChangeValidationRules(s.Id, s.Version, Rules(s)); s = w.ConfirmMapping(s.Id, s.Version);
        s = w.SetManualSample(s.Id, s.Version, Sample(s)); s = w.Review(s.Id, s.Version); s = w.Approve(s.Id, s.Version);
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var exporting = new UniversalDocWorkflowOrchestrator(actor, new SnapshotGate(store, entered, release));
        var task = Task.Run(() => Assert.Throws<StudioFault>(() => exporting.Export(s.Id, s.Version)));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(20)));
        if (kind == "rules") w.ChangeValidationRules(s.Id, s.Version, []); else w.ChangeMultisheetMapping(s.Id, s.Version, Mappings(s));
        release.Set(); Assert.Equal("SESSION_VERSION_CONFLICT", (await task).Code);
        Assert.Null(store.Read(actor.Value, s.Id).Document!.Approval);
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    [Fact] public void ExpiredSessionsCannotChangeRulesOrMapping()
    {
        var clock = new Clock(); using var store = new InMemoryUniversalDocSessionStore(clock); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        clock.Now += TimeSpan.FromMinutes(21);
        Assert.Equal(410, Assert.Throws<StudioFault>(() => w.ChangeValidationRules(s.Id, s.Version, Rules(s))).Status);
        Assert.Throws<StudioFault>(() => w.ChangeMultisheetMapping(s.Id, s.Version, Mappings(s)));
    }

    [Fact] public void RuleMessagesResolveFieldIdentityByScopeAndKey()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        var corrections = Corrections(s).Select(f => f.Label == "Printed total" && !f.Omit ? f with { Key = Key(s, "Quantity") } : f).ToImmutableArray();
        s = w.CorrectSchema(s.Id, s.Version, corrections); s = w.ChangeValidationRules(s.Id, s.Version, Rules(s)); s = w.ConfirmMapping(s.Id, s.Version);
        s = w.SetManualSample(s.Id, s.Version, Sample(s, total: "16"));
        Assert.Contains(s.Issues, i => i.Message == "Printed total differs from the sum of Amount.");
    }

}
