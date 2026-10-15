using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Api;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Domain;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class Gate4ATests
{
    private const string Root = "/api/universal-doc";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static async Task<HttpClient> Login(StudioHttpHost host, string name = "admin-a")
    {
        var client = host.Client(); using var login = await client.PostAsJsonAsync("/api/auth/login", new { username = name, password = "Studio@Test2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var bootstrap = await client.GetAsync(Root + "/bootstrap");
        if (name != "unmapped")
        {
            Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
            var data = await bootstrap.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Add("X-Studio-CSRF", data.GetProperty("csrfToken").GetString());
        }
        return client;
    }
    private static async Task<StudioSessionView> Body(HttpResponseMessage response)
    {
        using (response) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<StudioSessionView>(Json))!; }
    }
    private static async Task<StudioSessionView> New(HttpClient client) => await Body(await client.PostAsJsonAsync(Root + "/sessions", new { workflow = "invoice" }));
    private static async Task<StudioSessionView> Act(HttpClient client, StudioSessionView session, string action) =>
        await Body(await client.PostAsJsonAsync($"{Root}/sessions/{session.Id}/{action}", new { expectedVersion = session.Version }));
    private static async Task<StudioSessionView> Upload(HttpClient client, StudioSessionView session, byte[]? bytes = null)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(session.Version.ToString()), "expectedVersion");
        form.Add(new ByteArrayContent(bytes ?? StudioDemoCatalog.Template(session.Workflow)), "file", "synthetic.xlsx");
        return await Body(await client.PostAsync($"{Root}/sessions/{session.Id}/template", form));
    }
    private static async Task<StudioSessionView> Extracted(HttpClient client)
    { var s = await Upload(client, await New(client)); s = await Act(client, s, "mapping/confirm"); return await Act(client, s, "extract-demo"); }
    private static async Task<StudioSessionView> Approved(HttpClient client)
    { var s = await Extracted(client); return await Act(client, await Act(client, s, "review"), "approve"); }
    private static Task<HttpResponseMessage> Edit(HttpClient client, StudioSessionView s, string value = "DEMO-EDITED") => client.PatchAsJsonAsync($"{Root}/sessions/{s.Id}/fields",
        new { expectedVersion = s.Version, scope = "Header", key = "number", row = (int?)null, value });
    private static Task<HttpResponseMessage> Mapping(HttpClient client, StudioSessionView s, string numberCell = "C1") => client.PatchAsJsonAsync($"{Root}/sessions/{s.Id}/mapping",
        new { expectedVersion = s.Version, sheet = "Document", startRow = 6, headers = new { number = numberCell, total = "B3" },
            columns = new { description = 1, quantity = 2, price = 3, amount = 4 } });
    private static Task<HttpResponseMessage> Export(HttpClient client, StudioSessionView s) => client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/export", new { expectedVersion = s.Version });

    [Fact] public async Task AuthenticatedSixStepWorkflowExportsRealWorkbookAndReapprovesAfterEdit()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host);
        var s = await New(client); Assert.Equal("NoTemplate", s.MappingState);
        s = await Upload(client, s); Assert.Equal("DraftSuggested", s.MappingState); Assert.NotEmpty(s.Suggestions); Assert.NotNull(s.Mapping);
        using (var denied = await Export(client, s)) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        s = await Act(client, s, "mapping/confirm"); Assert.Equal("UserConfirmed", s.MappingState);
        s = await Act(client, s, "extract-demo"); Assert.Equal("Draft", s.ReviewState);
        Assert.Equal(10, s.Fields.Length); Assert.All(s.Fields, f => Assert.Equal("Unreviewed", f.Review));
        Assert.Contains(s.Fields, f => f.Value == "Vải tổng hợp" && f.SourceId == "synthetic-json-fixture" && f.Page == 1);
        var before = s.DataRevision; s = await Body(await Edit(client, s, "=literal")); Assert.True(s.DataRevision > before); Assert.Equal("NeedsReview", s.ReviewState);
        s = await Act(client, s, "review"); Assert.Equal("ReadyForApproval", s.ReviewState); s = await Act(client, s, "approve");
        using (var exported = await Export(client, s))
        {
            Assert.Equal(HttpStatusCode.OK, exported.StatusCode); Assert.Equal(StudioExport.ContentType, exported.Content.Headers.ContentType!.MediaType);
            Assert.Contains(StudioExport.Filename, exported.Content.Headers.ContentDisposition!.ToString());
            using var wb = new XLWorkbook(new MemoryStream(await exported.Content.ReadAsByteArrayAsync())); var sheet = wb.Worksheet("Document");
            Assert.Equal("=literal", sheet.Cell("B1").GetString()); Assert.False(sheet.Cell("B1").HasFormula);
            Assert.Equal(19.5m, sheet.Cell("B3").GetValue<decimal>()); Assert.Equal(2m, sheet.Cell("B6").GetValue<decimal>());
            Assert.Equal("B7*C7", sheet.Cell("E7").FormulaA1); Assert.Equal("SUM(E6:E7)", sheet.Cell("E8").FormulaA1);
            Assert.Equal("0.00", sheet.Cell("B7").Style.NumberFormat.Format); Assert.Contains(sheet.MergedRanges, r => r.RangeAddress.ToString() == "A9:C9");
            Assert.Equal("Synthetic invoice only", wb.Worksheet("Reference").Cell("A1").GetString());
        }
        s = await Body(await client.GetAsync($"{Root}/sessions/{s.Id}"));
        s = await Body(await Edit(client, s)); using (var denied = await Export(client, s)) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        s = await Act(client, await Act(client, s, "review"), "approve"); using var success = await Export(client, s); Assert.Equal(HttpStatusCode.OK, success.StatusCode);
    }
    [Theory] [InlineData("no-template")] [InlineData("suggested")] [InlineData("confirmed")]
    [InlineData("extracted")] [InlineData("reviewed")]
    public async Task DirectExportCannotSkipHumanApproval(string stage)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await New(client);
        if (stage != "no-template") s = await Upload(client, s);
        if (stage is "confirmed" or "extracted" or "reviewed") s = await Act(client, s, "mapping/confirm");
        if (stage is "extracted" or "reviewed") s = await Act(client, s, "extract-demo");
        if (stage == "reviewed") s = await Act(client, s, "review");
        using var denied = await Export(client, s); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
    }
    [Fact] public async Task AnonymousCannotAccessAnyStudioEndpoint()
    {
        await using var host = await StudioHttpHost.Start(); using var client = host.Client(); var id = new string('A', 64);
        foreach (var (method, path) in new[] { ("GET", "/bootstrap"), ("GET", "/templates/invoice"), ("POST", "/sessions"),
            ("GET", $"/sessions/{id}"), ("DELETE", $"/sessions/{id}"), ("POST", $"/sessions/{id}/template"), ("GET", $"/sessions/{id}/mapping"),
            ("PATCH", $"/sessions/{id}/mapping"), ("POST", $"/sessions/{id}/mapping/confirm"), ("POST", $"/sessions/{id}/extract-demo"),
            ("PATCH", $"/sessions/{id}/fields"), ("POST", $"/sessions/{id}/review"), ("POST", $"/sessions/{id}/approve"), ("POST", $"/sessions/{id}/export") })
        { using var response = await client.SendAsync(new(new HttpMethod(method), Root + path)); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    }
    [Theory] [InlineData("isApproved", true)] [InlineData("canExport", true)] [InlineData("reviewState", "Approved")]
    [InlineData("approval", "forged")] [InlineData("approvedByUserId", "studio-admin-b")]
    [InlineData("tenantId", "tenant-b")] [InlineData("dataRevision", 999)] [InlineData("profileVersion", 999)]
    public async Task ForgedAuthorityJsonIsRejected(string field, object value)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Extracted(client);
        using var response = await client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/approve", new Dictionary<string, object> { ["expectedVersion"] = s.Version, [field] = value });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var current = await Body(await client.GetAsync($"{Root}/sessions/{s.Id}")); Assert.Equal("Draft", current.ReviewState); Assert.Equal(s.Version, current.Version);
    }
    [Theory] [InlineData("admin-b")] [InlineData("other-a")]
    public async Task CrossTenantAndCrossUserReadEditApproveExportAreDenied(string other)
    {
        await using var host = await StudioHttpHost.Start(); using var owner = await Login(host); using var attacker = await Login(host, other); var s = await Approved(owner);
        using var read = await attacker.GetAsync($"{Root}/sessions/{s.Id}"); Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var edit = await Edit(attacker, s); Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        using var approve = await attacker.PostAsJsonAsync($"{Root}/sessions/{s.Id}/approve", new { expectedVersion = s.Version }); Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
        using var export = await Export(attacker, s); Assert.Equal(HttpStatusCode.NotFound, export.StatusCode);
    }
    [Fact] public async Task HeaderTenantSpoofCannotSwitchAuthorizedTenantOrRevealSession()
    {
        await using var host = await StudioHttpHost.Start(); using var a = await Login(host); using var b = await Login(host, "admin-b"); var s = await New(a);
        a.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-b"); Assert.Equal(s.Id, (await Body(await a.GetAsync($"{Root}/sessions/{s.Id}"))).Id);
        b.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-a"); using var denied = await b.GetAsync($"{Root}/sessions/{s.Id}"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
    [Fact] public async Task MissingServerMembershipFailsClosed()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host, "unmapped");
        using var bootstrap = await client.GetAsync(Root + "/bootstrap"); Assert.Equal(HttpStatusCode.Forbidden, bootstrap.StatusCode);
    }
    [Theory] [InlineData("reader-a")] [InlineData("reviewer-a")]
    public async Task RoleWithoutApprovalAuthorityCannotApproveOrExport(string name)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host, name); var s = await Extracted(client);
        using var approve = await client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/approve", new { expectedVersion = s.Version }); Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        using var export = await Export(client, s); Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
        using var review = await client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/review", new { expectedVersion = s.Version });
        Assert.Equal(name == "reviewer-a" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, review.StatusCode);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CookieAuthenticationRequiresValidCsrfTokenForMutations(bool forged)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); client.DefaultRequestHeaders.Remove("X-Studio-CSRF");
        if (forged) client.DefaultRequestHeaders.Add("X-Studio-CSRF", "forged");
        using var response = await client.PostAsJsonAsync(Root + "/sessions", new { workflow = "invoice" }); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact] public async Task MappingEditInvalidatesApprovalAndReconfirmationDoesNotReapprove()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Approved(client); var version = s.ProfileVersion; var revision = s.DataRevision;
        s = await Body(await Mapping(client, s)); Assert.Equal("DraftSuggested", s.MappingState); Assert.Equal("NeedsReview", s.ReviewState);
        Assert.Equal(version + 1, s.ProfileVersion); Assert.True(s.DataRevision > revision);
        using (var denied = await Export(client, s)) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using (var denied = await client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/review", new { expectedVersion = s.Version })) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        s = await Act(client, s, "mapping/confirm"); Assert.Equal("NeedsReview", s.ReviewState);
        using var stillDenied = await Export(client, s); Assert.Equal(HttpStatusCode.Conflict, stillDenied.StatusCode);
        s = await Act(client, await Act(client, s, "review"), "approve"); using var success = await Export(client, s); Assert.Equal(HttpStatusCode.OK, success.StatusCode);
    }
    [Fact] public async Task FieldEditInvalidatesApprovalAndRecomputesValidation()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Approved(client);
        s = await Body(await client.PatchAsJsonAsync($"{Root}/sessions/{s.Id}/fields", new { expectedVersion = s.Version, scope = "Header", key = "total", row = (int?)null, value = "999" }));
        Assert.Equal("NeedsReview", s.ReviewState); Assert.Contains(s.Issues, i => i.Code == "SOURCE_TOTAL_MISMATCH");
        using var export = await Export(client, s); Assert.Equal(HttpStatusCode.Conflict, export.StatusCode);
        using var review = await client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/review", new { expectedVersion = s.Version }); Assert.Equal(HttpStatusCode.Conflict, review.StatusCode);
    }
    [Theory] [InlineData("field")] [InlineData("scope")] [InlineData("row")] [InlineData("type")]
    public async Task InvalidFieldEditsCannotReplaceCanonicalState(string kind)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Extracted(client);
        using var response = await client.PatchAsJsonAsync($"{Root}/sessions/{s.Id}/fields", new { expectedVersion = s.Version,
            scope = kind == "scope" ? "Unknown" : "Header", key = kind == "field" ? "evil" : kind == "type" ? "total" : "number",
            row = kind == "row" ? (int?)99 : null, value = kind == "type" ? "not-number" : "changed" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Theory] [InlineData("unknown")] [InlineData("formula-cell")]
    public async Task IncorrectMappingReferencesAreRejected(string kind)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Upload(client, await New(client));
        using var response = kind == "formula-cell" ? await Mapping(client, s, "E6") : await client.PatchAsJsonAsync($"{Root}/sessions/{s.Id}/mapping",
            new { expectedVersion = s.Version, sheet = "Document", startRow = 6, headers = new { unknown = "B1", total = "B3" }, columns = new { description = 1, quantity = 2, price = 3, amount = 4 } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Theory] [InlineData("extract-demo")] [InlineData("review")] [InlineData("approve")]
    public async Task BrowserCannotSkipMappingConfirmation(string action)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Upload(client, await New(client));
        using var denied = await client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/{action}", new { expectedVersion = s.Version }); Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
    }
    [Theory] [InlineData("profile")] [InlineData("template")] [InlineData("confirmation")]
    public async Task StaleServerHeldIdentitiesPreventExport(string kind)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Approved(client);
        using var scope = host.App.Services.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<IUniversalDocSessionStore>();
        var actor = new UniversalDocActor("studio-admin-a", "tenant-a", "Admin", DateTimeOffset.UtcNow.AddHours(1));
        var altered = store.Update(actor, s.Id, s.Version, state => kind switch {
            "profile" => state with { Profile = state.Profile with { Version = state.Profile.Version + 1 } },
            "template" => state with { Template = state.Template.SetItem(0, (byte)(state.Template[0] ^ 1)) },
            _ => state with { Proposal = state.Proposal! with { Revision = state.Proposal.Revision + 1 } } });
        using var response = await Export(client, s with { Version = altered.Version }); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(kind switch { "profile" => "PROFILE_IDENTITY_CHANGED", "template" => "TEMPLATE_IDENTITY_CHANGED", _ => "MAPPING_CONFIRMATION_STALE" }, error.GetProperty("code").GetString());
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ExpiryAndCleanupPreventSessionReuse(bool cleanup)
    {
        var clock = new Clock(); await using var host = await StudioHttpHost.Start(clock); using var client = await Login(host); var s = await Approved(client);
        clock.Advance(InMemoryUniversalDocSessionStore.Lifetime + TimeSpan.FromSeconds(1));
        if (cleanup) host.App.Services.GetRequiredService<IUniversalDocSessionStore>().Cleanup();
        using var response = await Export(client, s); Assert.Equal(cleanup ? HttpStatusCode.NotFound : HttpStatusCode.Gone, response.StatusCode);
        using var repeated = await client.GetAsync($"{Root}/sessions/{s.Id}"); Assert.Equal(HttpStatusCode.NotFound, repeated.StatusCode);
    }
    [Fact] public async Task ExpiredAuthenticatedIdentityCannotReuseSession()
    {
        var clock = new Clock(); await using var host = await StudioHttpHost.Start(clock); using var client = await Login(host); var s = await New(client);
        clock.Advance(TimeSpan.FromHours(25)); using var response = await client.GetAsync($"{Root}/sessions/{s.Id}"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact] public async Task SessionCapacityAndDiscardAreEnforced()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var first = await New(client); await New(client);
        using var denied = await client.PostAsJsonAsync(Root + "/sessions", new { workflow = "invoice" }); Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        using var deleted = await client.SendAsync(new(HttpMethod.Delete, $"{Root}/sessions/{first.Id}") { Content = JsonContent.Create(new { expectedVersion = first.Version }) });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode); await New(client);
    }
    [Fact] public async Task ConcurrentPatchRequestsCannotSilentlyOverwrite()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Extracted(client);
        var responses = await Task.WhenAll(Edit(client, s, "one"), Edit(client, s, "two"));
        Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.OK)); Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.Conflict));
        var current = await Body(await client.GetAsync($"{Root}/sessions/{s.Id}")); Assert.Equal(s.Version + 1, current.Version); Assert.Equal(s.DataRevision + 1, current.DataRevision);
        foreach (var response in responses) response.Dispose();
    }
    private sealed class PausedStore(IUniversalDocSessionStore inner) : IUniversalDocSessionStore
    {
        public volatile bool PauseRead, PauseUpdate;
        public TaskCompletionSource Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(false);
        private void Pause() { Captured.TrySetResult(); if (!Release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException(); }
        public UniversalDocSession Create(UniversalDocActor actor, string workflow) => inner.Create(actor, workflow);
        public UniversalDocSession Read(UniversalDocActor actor, string id) { var s = inner.Read(actor, id); if (PauseRead) { PauseRead = false; Pause(); } return s; }
        public UniversalDocSession Update(UniversalDocActor actor, string id, long version, Func<UniversalDocSession, UniversalDocSession> change)
        { if (PauseUpdate) { PauseUpdate = false; Pause(); } return inner.Update(actor, id, version, change); }
        public void Remove(UniversalDocActor actor, string id, long version) => inner.Remove(actor, id, version);
        public void Cleanup() => inner.Cleanup();
    }
    [Theory] [InlineData("field")] [InlineData("mapping")]
    public async Task ExportRacingWithMutationRejectsPreparedApprovedSnapshot(string mutation)
    {
        PausedStore paused = null!; await using var host = await StudioHttpHost.Start(decorate: store => paused = new(store)); using var client = await Login(host); var s = await Approved(client);
        paused.PauseRead = true; var exporting = Export(client, s); await paused.Captured.Task.WaitAsync(TimeSpan.FromSeconds(30));
        try { s = await Body(mutation == "field" ? await Edit(client, s) : await Mapping(client, s)); } finally { paused.Release.Set(); }
        using var denied = await exporting; Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode); Assert.NotEqual("Approved", s.ReviewState);
        Assert.Equal("SESSION_VERSION_CONFLICT", (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
    [Theory] [InlineData("field")] [InlineData("mapping")]
    public async Task ApprovalRacingWithMutationCannotPreserveOldReview(string mutation)
    {
        PausedStore paused = null!; await using var host = await StudioHttpHost.Start(decorate: store => paused = new(store)); using var client = await Login(host); var s = await Act(client, await Extracted(client), "review");
        paused.PauseUpdate = true; var approving = client.PostAsJsonAsync($"{Root}/sessions/{s.Id}/approve", new { expectedVersion = s.Version });
        await paused.Captured.Task.WaitAsync(TimeSpan.FromSeconds(30));
        try { s = await Body(mutation == "field" ? await Edit(client, s) : await Mapping(client, s)); } finally { paused.Release.Set(); }
        using var denied = await approving; Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode); Assert.Equal("NeedsReview", s.ReviewState);
        Assert.Equal("SESSION_VERSION_CONFLICT", (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
    [Fact] public async Task UnsupportedWorkbookStructureIsRejected()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await New(client);
        using var wb = new XLWorkbook(new MemoryStream(StudioDemoCatalog.Template("invoice"))); wb.Worksheet("Document").Range("A5:D6").CreateTable();
        using var bytes = new MemoryStream(); wb.SaveAs(bytes); using var form = new MultipartFormDataContent(); form.Add(new StringContent(s.Version.ToString()), "expectedVersion");
        form.Add(new ByteArrayContent(bytes.ToArray()), "file", "synthetic.xlsx"); using var response = await client.PostAsync($"{Root}/sessions/{s.Id}/template", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact] public async Task SafeViewsDoNotExposeInternalSessionOrApprovalEvidence()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Approved(client);
        using var response = await client.GetAsync($"{Root}/sessions/{s.Id}"); var text = await response.Content.ReadAsStringAsync();
        foreach (var name in new[] { "templateSha256", "profileFingerprint", "confirmation", "reviewEvidence", "approval", "ownerId", "tenantId" })
            Assert.False(JsonDocument.Parse(text).RootElement.TryGetProperty(name, out _));
    }
    [Fact] public void MissingIdentityAndUnmappedIdentityFailClosedInApplicationResolver()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["UniversalDocGate4A:Users:known"] = "tenant-a" }).Build();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var resolver = new UniversalDocCurrentActor(accessor, config, TimeProvider.System);
        Assert.Equal(401, Assert.Throws<StudioFault>(() => resolver.Resolve()).Status);
        accessor.HttpContext.User = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin"), new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()) }, "validated"));
        Assert.Equal(401, Assert.Throws<StudioFault>(() => resolver.Resolve()).Status);
    }
    [Fact] public void NewControllerIsAuthenticatedAndOnlyOrchestratorCallsProjector()
    {
        Assert.NotEmpty(typeof(UniversalDocStudioController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "backend"))) directory = directory.Parent;
        Assert.NotNull(directory); var root = Path.Combine(directory!.FullName, "backend", "ShoeExportInvoice.Api", "Features", "UniversalDocEngine");
        var callers = new[] { "Api", "Application" }.SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(file => File.ReadAllText(file).Contains("new ExcelTemplateProjector()", StringComparison.Ordinal)).ToArray();
        Assert.Equal("UniversalDocWorkflowOrchestrator.cs", Path.GetFileName(Assert.Single(callers)));
    }
    [Theory] [InlineData("create")] [InlineData("fields")] [InlineData("mapping")]
    public async Task ForgedCanonicalObjectsAndTenantBodiesAreRejectedAtEveryWriteBoundary(string endpoint)
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await Extracted(client);
        var payload = endpoint switch {
            "create" => new Dictionary<string, object?> { ["workflow"] = "invoice", ["tenantId"] = "tenant-b" },
            "fields" => new Dictionary<string, object?> { ["expectedVersion"] = s.Version, ["scope"] = "Header", ["key"] = "number", ["value"] = "n", ["approval"] = new { reviewerId = "attacker" } },
            _ => new Dictionary<string, object?> { ["expectedVersion"] = s.Version, ["sheet"] = "Document", ["startRow"] = 6,
                ["headers"] = new { number = "B1", total = "B3" }, ["columns"] = new { description = 1, quantity = 2, price = 3, amount = 4 }, ["profileVersion"] = 999 } };
        using var response = endpoint == "create" ? await client.PostAsJsonAsync(Root + "/sessions", payload)
            : await client.PatchAsJsonAsync($"{Root}/sessions/{s.Id}/{endpoint}", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact] public async Task GuessingSessionIdAndReusingAnotherUsersCsrfTokenFail()
    {
        await using var host = await StudioHttpHost.Start(); using var a = await Login(host); using var b = await Login(host, "other-a");
        using var guess = await a.GetAsync($"{Root}/sessions/{new string('A', 64)}"); Assert.Equal(HttpStatusCode.NotFound, guess.StatusCode);
        b.DefaultRequestHeaders.Remove("X-Studio-CSRF"); b.DefaultRequestHeaders.Add("X-Studio-CSRF", a.DefaultRequestHeaders.GetValues("X-Studio-CSRF").Single());
        using var forged = await b.PostAsJsonAsync(Root + "/sessions", new { workflow = "invoice" }); Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
    }
    [Fact] public async Task OversizedTemplateIsRejectedBeforeStorage()
    {
        await using var host = await StudioHttpHost.Start(); using var client = await Login(host); var s = await New(client);
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(s.Version.ToString()), "expectedVersion");
        form.Add(new ByteArrayContent(new byte[InMemoryUniversalDocSessionStore.MaximumTemplateBytes + 1]), "file", "oversized.xlsx");
        using var response = await client.PostAsync($"{Root}/sessions/{s.Id}/template", form);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge });
        var current = await Body(await client.GetAsync($"{Root}/sessions/{s.Id}")); Assert.Equal("NoTemplate", current.MappingState); Assert.Equal(s.Version, current.Version);
    }
    [Fact] public void SessionAdmissionBoundsTenantGlobalCapacityAndDisposal()
    {
        var clock = new Clock(); using var store = new InMemoryUniversalDocSessionStore(clock);
        UniversalDocActor Actor(string id, string tenant) => new(id, tenant, "Admin", clock.GetUtcNow().AddHours(1));
        for (int i = 0; i < InMemoryUniversalDocSessionStore.PerTenant; i++) store.Create(Actor("user" + i, "shared"), "invoice");
        Assert.Equal(429, Assert.Throws<StudioFault>(() => store.Create(Actor("extra", "shared"), "invoice")).Status);
        for (int i = InMemoryUniversalDocSessionStore.PerTenant; i < InMemoryUniversalDocSessionStore.MaximumSessions; i++) store.Create(Actor("user" + i, "tenant" + i), "receipt");
        Assert.Equal(429, Assert.Throws<StudioFault>(() => store.Create(Actor("extra", "new"), "invoice")).Status);
        clock.Advance(InMemoryUniversalDocSessionStore.Lifetime + TimeSpan.FromSeconds(1)); store.Cleanup();
        var session = store.Create(Actor("new", "new"), "packing"); Assert.Equal(64, session.Id.Length);
        store.Dispose(); Assert.Equal(404, Assert.Throws<StudioFault>(() => store.Read(Actor("new", "new"), session.Id)).Status);
    }
}
