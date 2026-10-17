using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.TemplateUnderstanding;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public class Gate5ATests
{
    private sealed class Actor : IUniversalDocCurrentActor { public UniversalDocActor Value = new("owner", "tenant", "Admin", DateTimeOffset.UtcNow.AddHours(1)); public UniversalDocActor Resolve() => Value; }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private static ImmutableArray<SchemaFieldCorrection> Corrections(StudioSessionView s) => s.SchemaDraft!.Fields.Select((f, i) => new SchemaFieldCorrection(f.Key, f.Label, f.Scope,
        f.Label is "Quantity" or "Số lượng" or "Printed value" ? "Decimal" : "Text", false, false, 0)).ToImmutableArray();
    private static ManualSample Sample(StudioSessionView s)
    {
        var header = ImmutableDictionary.CreateBuilder<string, string?>(); var first = ImmutableDictionary.CreateBuilder<string, string?>(); var second = ImmutableDictionary.CreateBuilder<string, string?>();
        foreach (var f in s.SchemaDraft!.Fields.Where(f => !f.Omit))
        {
            if (f.Scope == "Header") header[f.Key] = f.Type == "Decimal" ? "19.50" : "00123";
            else { first[f.Key] = f.Type == "Decimal" ? "2.5" : f.Label is "Product code" or "Mã hàng" ? "00042" : "Vải tổng hợp"; second[f.Key] = f.Type == "Decimal" ? "3" : "00043"; }
        }
        return new(header.ToImmutable(), ImmutableArray.Create(first.ToImmutable(), second.ToImmutable()));
    }
    private static StudioSessionView Ready(UniversalDocWorkflowOrchestrator w, string kind = "accounting")
    { var s = w.Create("example-template"); s = w.Upload(s.Id, s.Version, Gate5AFixtures.Workbook(kind), "template.xlsx"); return w.CorrectSchema(s.Id, s.Version, Corrections(s)); }
    [Theory] [InlineData("accounting")] [InlineData("warehouse")] [InlineData("packing")]
    public void LearningConfirmationReviewAndRealExportPreserveStructure(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var actor = new Actor(); var w = new UniversalDocWorkflowOrchestrator(actor, store);
        var s = Ready(w, kind); Assert.Equal("ReadyForConfirmation", s.SchemaDraft!.State); Assert.Equal(6, s.SchemaDraft.Fields.Length);
        Assert.Equal("MAPPING_CONFIRMATION_REQUIRED", Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version)).Code);
        s = w.ConfirmMapping(s.Id, s.Version); Assert.Equal("UserConfirmed", s.SchemaDraft!.State);
        s = w.SetManualSample(s.Id, s.Version, Sample(s)); Assert.All(s.Fields, f => Assert.Equal("Manual", f.Source));
        s = w.Review(s.Id, s.Version); s = w.Approve(s.Id, s.Version); var exported = w.Export(s.Id, s.Version); Gate5AFixtures.Verify(exported.Workbook);
        var snapshot = store.Read(actor.Value, s.Id); Assert.Equal("owner", snapshot.Document!.Approval!.ReviewerId);
        s = w.Read(s.Id); var revision = s.ProfileVersion; s = w.CorrectSchema(s.Id, s.Version, Corrections(s));
        Assert.Equal(revision + 1, s.ProfileVersion); Assert.Equal("NeedsReview", s.ReviewState); Assert.Null(store.Read(actor.Value, s.Id).Document!.Approval);
        Assert.Equal("MAPPING_CONFIRMATION_REQUIRED", Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version)).Code);
        s = w.ConfirmMapping(s.Id, s.Version); Assert.Equal("CURRENT_APPROVAL_REQUIRED", Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version)).Code);
    }
    [Fact] public void SuggestionsDeterministicAmbiguousUnicodeAndBytesUnchanged()
    {
        var bytes = Gate5AFixtures.Workbook("warehouse"); var original = bytes.ToArray(); using var stream = new MemoryStream(bytes);
        var structure = new TemplateStructureAnalyzer().Analyze(stream, "x.xlsx").Structure!; var learner = new ExampleSchemaLearner();
        var first = learner.Suggest(structure, bytes, null); Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(learner.Suggest(structure, bytes, null)));
        Assert.Equal(original, bytes); Assert.Contains(first, f => f.Label == "Số phiếu" && f.Status == "Ambiguous" && f.Candidates.Length == 2);
        Assert.Contains(first, f => f.Label == "Mã hàng" && f.Type == "Text"); Assert.DoesNotContain(first, f => f.Candidates.Any(c => c.Cell == "E6" || c.LabelCell == "A7"));
    }
    [Fact] public void FilledExampleBindsBothHashesAndCannotConfirm()
    {
        var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(actor, store); var s = Ready(w);
        var template = s.SchemaDraft!.TemplateSha256; var version = s.SchemaDraft.Revision;
        s = w.UploadLearning(s.Id, s.Version, Gate5AFixtures.Workbook("accounting", true), "example.xlsx", true);
        Assert.Equal(template, s.SchemaDraft!.TemplateSha256); Assert.NotEqual(template, s.SchemaDraft.ExampleSha256); Assert.Equal(version + 1, s.SchemaDraft.Revision);
        Assert.Equal("NeedsCorrection", s.SchemaDraft.State); Assert.Contains(s.SchemaDraft.Fields, f => f.Label == "Printed value" && f.Type == "Decimal"); Assert.Null(s.Mapping); Assert.Equal("NoDocument", s.ReviewState);
        Assert.Throws<StudioFault>(() => w.ConfirmMapping(s.Id, s.Version));
    }
    [Theory] [InlineData("sheet")] [InlineData("formula")] [InlineData("merge")] [InlineData("header")]
    public void StructuralMismatchRejectsWithoutMutatingSession(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        using var wb = new XLWorkbook(new MemoryStream(Gate5AFixtures.Workbook("accounting", true))); var ws = wb.Worksheet("Data");
        if (kind == "sheet") ws.Name = "Renamed"; else if (kind == "formula") ws.Cell("E6").FormulaA1 = "C6*3"; else if (kind == "merge") ws.Range("A8:C8").Unmerge(); else ws.Cell("A1").Value = "Changed";
        using var data = new MemoryStream(); wb.SaveAs(data);
        Assert.Equal("EXAMPLE_STRUCTURE_MISMATCH", Assert.Throws<StudioFault>(() => w.UploadLearning(s.Id, s.Version, data.ToArray(), "example.xlsx", true)).Code);
        Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
    [Theory] [InlineData("type")] [InlineData("key")] [InlineData("duplicate")] [InlineData("unresolved")] [InlineData("required-omit")] [InlineData("scope")] [InlineData("identifier")]
    public void InvalidCorrectionsFailClosed(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w); var fields = Corrections(s);
        fields = kind switch {
            "type" => fields.SetItem(0, fields[0] with { Type = "Object" }), "key" => fields.SetItem(0, fields[0] with { Key = "eval()" }),
            "duplicate" => fields.SetItem(1, fields[1] with { Key = fields[0].Key, Scope = fields[0].Scope }),
            "unresolved" => fields.SetItem(0, fields[0] with { Candidate = null }),
            "required-omit" => fields.SetItem(0, fields[0] with { Omit = true, Required = true }),
            "scope" => fields.SetItem(0, fields[0] with { Scope = "Header" }),
            _ => fields.SetItem(0, fields[0] with { Type = "Decimal" }) };
        Assert.Throws<StudioFault>(() => w.CorrectSchema(s.Id, s.Version, fields)); Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
    [Fact] public async Task ConcurrentRevisionsHaveExactlyOneWinner()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => { try { w.CorrectSchema(s.Id, s.Version, Corrections(s)); return 200; } catch (StudioFault f) { return f.Status; } })));
        Assert.Equal(new[] { 200, 409 }, results.Order().ToArray()); Assert.Equal(s.Version + 1, w.Read(s.Id).Version);
    }
    [Theory] [InlineData("owner")] [InlineData("tenant")] [InlineData("expired")]
    public void LearningSessionOwnershipAndExpiry(string kind)
    {
        var clock = new Clock(); var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(clock); var w = new UniversalDocWorkflowOrchestrator(actor, store); var s = Ready(w);
        if (kind == "owner") actor.Value = actor.Value with { UserId = "other" }; else if (kind == "tenant") actor.Value = actor.Value with { TenantId = "other" }; else clock.Now += TimeSpan.FromMinutes(21);
        Assert.Equal(kind == "expired" ? 410 : 404, Assert.Throws<StudioFault>(() => w.CorrectSchema(s.Id, s.Version, Corrections(s))).Status);
    }
    [Theory] [InlineData("table")] [InlineData("formula")] [InlineData("oversized")] [InlineData("missing")]
    public void UnsupportedInputCannotBecomeExportable(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = w.Create("example-template");
        using var wb = new XLWorkbook(new MemoryStream(Gate5AFixtures.Workbook("accounting"))); var ws = wb.Worksheet("Data");
        if (kind == "table") ws.Range("A5:D6").CreateTable(); else if (kind == "formula") ws.Cell("E6").FormulaA1 = "NOW()";
        else if (kind == "missing") ws.Range("A5:D5").Style.Font.Bold = false;
        using var data = new MemoryStream(); wb.SaveAs(data); var bytes = kind == "oversized" ? new byte[2097153] : data.ToArray();
        if (kind == "missing") { s = w.Upload(s.Id, s.Version, bytes, "x.xlsx"); Assert.Empty(s.SchemaDraft!.Fields); Assert.Equal("NeedsCorrection", s.SchemaDraft.State); }
        else Assert.Throws<StudioFault>(() => w.Upload(s.Id, s.Version, bytes, "x.xlsx"));
        Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version));
    }
    [Fact] public async Task RealHttpRejectsAnonymousForgedAuthorityNestedFieldsAndCrossTenant()
    {
        await using var host = await StudioHttpHost.Start(); using var anonymous = host.Client();
        using var denied = await anonymous.PatchAsJsonAsync("/api/universal-doc/sessions/anything/schema", new { expectedVersion = 1 }); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        async Task<HttpClient> Login(string username) { var c = host.Client(); using var response = await c.PostAsJsonAsync("/api/auth/login", new { username, password = "Studio@Test2026!" }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var b = await c.GetFromJsonAsync<JsonElement>("/api/universal-doc/bootstrap"); c.DefaultRequestHeaders.Add("X-Studio-CSRF", b.GetProperty("csrfToken").GetString()); return c; }
        using var owner = await Login("admin-a"); using var other = await Login("admin-b");
        var created = await owner.PostAsJsonAsync("/api/universal-doc/sessions", new { workflow = "example-template" }); var json = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = json.GetProperty("id").GetString();
        using var attack = await other.PatchAsJsonAsync($"/api/universal-doc/sessions/{id}/schema", new { expectedVersion = 1, fields = new[] { new { key = "x", label = "x", scope = "Header", type = "Text", required = false, omit = false, candidate = 0 } } }); Assert.Equal(HttpStatusCode.NotFound, attack.StatusCode);
        using var forged = await owner.PatchAsJsonAsync($"/api/universal-doc/sessions/{id}/schema", new { expectedVersion = 1, userConfirmed = true, fields = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        using var nested = await owner.PatchAsJsonAsync($"/api/universal-doc/sessions/{id}/schema", new { expectedVersion = 1, fields = new[] { new { key = "x", label = "x", scope = "Header", type = "Text", required = false, omit = false, candidate = 0, approval = true } } }); Assert.Equal(HttpStatusCode.BadRequest, nested.StatusCode);
    }
    [Theory] [InlineData("formula")] [InlineData("duplicate")] [InlineData("merged")] [InlineData("absent")]
    public void ExplicitDestinationsCannotBypassLayoutSafety(string kind)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w); var fields = Corrections(s);
        fields = fields.SetItem(1, fields[1] with { Worksheet = "Data", Column = kind == "formula" ? 5 : kind == "duplicate" ? 1 : 2,
            PrototypeRow = kind == "merged" ? 8 : kind == "absent" ? 999 : 6 });
        Assert.Throws<StudioFault>(() => w.CorrectSchema(s.Id, s.Version, fields)); Assert.Equal(s.Version, w.Read(s.Id).Version);
    }
    [Fact] public void HumanCanCorrectScopeWithExplicitSupportedDestinationAndOmitOptionalField()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w); var fields = Corrections(s);
        // Move the description field to an existing header value slot, omitting its competing metadata field.
        fields = fields.SetItem(1, fields[1] with { Scope = "Header", Worksheet = "Data", Cell = "B2" });
        fields = fields.SetItem(4, fields[4] with { Omit = true, Candidate = null });
        s = w.CorrectSchema(s.Id, s.Version, fields); Assert.Equal("Header", s.SchemaDraft!.Fields[1].Scope); Assert.True(s.SchemaDraft.Fields[4].Omit);
        Assert.Equal("B2", s.Mapping!.Headers[fields[1].Key]); s = w.ConfirmMapping(s.Id, s.Version); Assert.Equal("UserConfirmed", s.SchemaDraft.State);
    }
    [Theory] [InlineData("profile")] [InlineData("example")]
    public void StaleServerFingerprintsCannotConfirmOrExport(string kind)
    {
        var actor = new Actor(); using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(actor, store); var s = Ready(w);
        if (kind == "example") { s = w.UploadLearning(s.Id, s.Version, Gate5AFixtures.Workbook("accounting", true), "x.xlsx", true); s = w.CorrectSchema(s.Id, s.Version, Corrections(s)); }
        var altered = store.Update(actor.Value, s.Id, s.Version, x => kind == "profile" ? x with { MappingProfile = x.MappingProfile with { Version = 999 } } : x with { Example = x.Example.SetItem(0, 0) });
        Assert.Throws<StudioFault>(() => w.ConfirmMapping(s.Id, altered.Version)); Assert.Throws<StudioFault>(() => w.Export(s.Id, altered.Version));
    }
    [Fact] public void DecimalPrecisionProtectionRemainsAtRealExportBoundary()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w); s = w.ConfirmMapping(s.Id, s.Version);
        var sample = Sample(s); sample = sample with { Rows = sample.Rows.SetItem(0, sample.Rows[0].SetItem("field3", "1234567890123456.5")) };
        s = w.SetManualSample(s.Id, s.Version, sample); s = w.Review(s.Id, s.Version); s = w.Approve(s.Id, s.Version);
        Assert.Equal("EXPORT_VALIDATION_FAILED", Assert.Throws<StudioFault>(() => w.Export(s.Id, s.Version)).Code);
    }
    [Fact] public void ManualTypesAreValidatedAndUnknownFieldsNeverBecomeCanonical()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = Ready(w); var fields = Corrections(s);
        fields = fields.SetItem(1, fields[1] with { Type = "Date" }).SetItem(3, fields[3] with { Type = "Boolean" });
        s = w.CorrectSchema(s.Id, s.Version, fields); s = w.ConfirmMapping(s.Id, s.Version);
        var sample = Sample(s); sample = sample with { Rows = sample.Rows.Select(r => r.SetItem("field2", "2026-10-17").SetItem("field4", "true")).ToImmutableArray() };
        s = w.SetManualSample(s.Id, s.Version, sample); Assert.Contains(s.Fields, f => f.Type == "Date" && f.Value == "2026-10-17"); Assert.Contains(s.Fields, f => f.Type == "Boolean" && f.Value == "True");
        var invalid = sample with { Header = sample.Header.Add("unknown", "data") }; Assert.Throws<StudioFault>(() => w.SetManualSample(s.Id, s.Version, invalid));
        invalid = sample with { Rows = sample.Rows.SetItem(0, sample.Rows[0].SetItem("field2", "17/10/2026")) }; Assert.Throws<StudioFault>(() => w.SetManualSample(s.Id, s.Version, invalid));
    }

    [Fact] public void ExampleSharesTheExistingSessionByteBudget()
    {
        static byte[] Padded(byte[] original)
        {
            using var data = new MemoryStream(); data.Write(original);
            using (var zip = new System.IO.Compression.ZipArchive(data, System.IO.Compression.ZipArchiveMode.Update, true))
            { using var part = zip.CreateEntry("synthetic-padding.bin", System.IO.Compression.CompressionLevel.NoCompression).Open(); part.Write(new byte[1100000]); }
            return data.ToArray();
        }
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var w = new UniversalDocWorkflowOrchestrator(new Actor(), store); var s = w.Create("example-template");
        s = w.Upload(s.Id, s.Version, Padded(Gate5AFixtures.Workbook("accounting")), "template.xlsx");
        Assert.Equal("SESSION_BYTE_LIMIT", Assert.Throws<StudioFault>(() => w.UploadLearning(s.Id, s.Version, Padded(Gate5AFixtures.Workbook("accounting", true)), "example.xlsx", true)).Code);
        Assert.Null(w.Read(s.Id).SchemaDraft!.ExampleSha256); Assert.Equal(s.Version, w.Read(s.Id).Version);
    }

}
