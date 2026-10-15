using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Extraction;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public class Gate4B1Tests
{
    private const string Root = "/api/universal-doc/sessions/";
    private static readonly UniversalDocActor Actor = new("studio-admin-a", "tenant-a", "Admin", DateTimeOffset.UtcNow.AddHours(24));
    public static byte[] Image(string type) => Convert.FromBase64String(type switch
    {
        "png" => "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC",
        "webp" => "UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoBAAEAAUAmJaQAA3AA/vz0AAA=",
        "jpg" => "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==",
        _ => throw new ArgumentException(type)
    });
    public static byte[] Pdf()
    {
        var s = "%PDF-1.4\n"; var offsets = new List<int>();
        foreach (var body in new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>" })
        { offsets.Add(s.Length); s += $"{offsets.Count} 0 obj\n{body}\nendobj\n"; }
        int xref = s.Length; s += "xref\n0 4\n0000000000 65535 f \n";
        foreach (var offset in offsets) s += offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n \n";
        return Encoding.ASCII.GetBytes(s + $"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
    private static string Mime(string type) => type == "pdf" ? "application/pdf" : type == "jpg" ? "image/jpeg" : "image/" + type;
    private static byte[] Bytes(string type) => type == "pdf" ? Pdf() : Image(type);
    private static async Task<HttpClient> Login(StudioHttpHost host, string name = "admin-a")
    {
        var c = host.Client(); using var login = await c.PostAsJsonAsync("/api/auth/login", new { username = name, password = "Studio@Test2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var bootstrap = await c.GetFromJsonAsync<JsonElement>("/api/universal-doc/bootstrap");
        c.DefaultRequestHeaders.Add("X-Studio-CSRF", bootstrap.GetProperty("csrfToken").GetString()); return c;
    }
    private static async Task<StudioSessionView> View(HttpResponseMessage response)
    { using (response) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<StudioSessionView>())!; } }
    private static Task<StudioSessionView> New(HttpClient c) => ViewTask(c.PostAsJsonAsync(Root.TrimEnd('/'), new { workflow = "invoice" }));
    private static async Task<StudioSessionView> ViewTask(Task<HttpResponseMessage> response) => await View(await response);
    private static async Task<HttpResponseMessage> Upload(HttpClient c, StudioSessionView s, byte[]? bytes = null, string filename = "synthetic.png", string mime = "image/png", string? forged = null)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(s.Version.ToString()), "expectedVersion");
        if (forged is not null) form.Add(new StringContent("attacker"), forged);
        var file = new ByteArrayContent(bytes ?? Image("png")); file.Headers.ContentType = new(mime); form.Add(file, "file", filename);
        return await c.PostAsync(Root + s.Id + "/sources", form);
    }
    private static Task<HttpResponseMessage> Delete(HttpClient c, StudioSessionView s, string sourceId) => c.SendAsync(new(HttpMethod.Delete, Root + s.Id + "/sources/" + sourceId)
        { Content = JsonContent.Create(new { expectedVersion = s.Version }) });
    private static Task<HttpResponseMessage> Action(HttpClient c, StudioSessionView s, string action) =>
        c.PostAsJsonAsync(Root + s.Id + "/" + action, new { expectedVersion = s.Version });
    private static async Task<StudioSessionView> Extracted(HttpClient c)
    {
        var s = await New(c); using var form = new MultipartFormDataContent(); form.Add(new StringContent(s.Version.ToString()), "expectedVersion");
        form.Add(new ByteArrayContent(StudioDemoCatalog.Template("invoice")), "file", "synthetic.xlsx");
        s = await View(await c.PostAsync(Root + s.Id + "/template", form));
        s = await View(await Action(c, s, "mapping/confirm")); return await View(await Action(c, s, "extract-demo"));
    }
    [Theory] [InlineData("png")] [InlineData("jpg")] [InlineData("webp")] [InlineData("pdf")]
    public async Task ValidSyntheticFormatsHaveServerIdentityActualHashAndSafeMetadata(string type)
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await New(c); var bytes = Bytes(type);
        s = await View(await Upload(c, s, bytes, "synthetic." + type, Mime(type))); var source = Assert.Single(s.Sources);
        Assert.Matches("^[A-F0-9]{64}$", source.SourceId); Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), source.Sha256);
        Assert.Equal(bytes.Length, source.ByteLength); Assert.Equal(Mime(type), source.MediaType); Assert.Equal("AdmittedNotRecognized", source.Status);
        Assert.Equal(type == "pdf" ? 1 : null, source.PageCount); Assert.Equal(type == "pdf" ? null : 1, source.Width); Assert.Equal(type == "pdf" ? null : 1, source.Height);
        var text = await c.GetStringAsync(Root + s.Id + "/sources");
        foreach (var secret in new[] { "tenant-a", "studio-admin-a", "synthetic." + type, Convert.ToBase64String(bytes), "\"bytes\"", "ownerId", "tenantId" }) Assert.DoesNotContain(secret, text);
        var store = host.App.Services.GetRequiredService<IUniversalDocSessionStore>(); var snapshot = store.Read(Actor, s.Id);
        var bridge = new StudioRecognitionInput(snapshot); var trusted = Assert.Single(bridge.Context.Sources);
        Assert.Equal(source.SourceId, trusted.SourceId); Assert.Equal(source.Sha256, trusted.Sha256);
        using var input = bridge.OpenRead(source.SourceId); using var copy = new MemoryStream(); await input.CopyToAsync(copy); Assert.Equal(bytes, copy.ToArray());
        Assert.Throws<StudioFault>(() => new StudioRecognitionInput(snapshot with { Sources = snapshot.Sources.SetItem(0, snapshot.Sources[0] with { Sha256 = new string('0', 64) }) }));
    }
    [Theory] [InlineData("empty")] [InlineData("extension")] [InlineData("mismatch")] [InlineData("mime")]
    [InlineData("signature")] [InlineData("truncated")] [InlineData("crc")] [InlineData("pdf-action")]
    [InlineData("webp-size")] [InlineData("jpeg-eof")]
    [InlineData("jpeg-quantization-reference")] [InlineData("jpeg-huffman-reference")]
    public async Task InvalidOrComplexFilesAreRejectedWithoutMutation(string kind)
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await New(c);
        var bytes = Image("png"); var filename = "synthetic.png"; var mime = "image/png";
        switch (kind)
        {
            case "empty": bytes = []; break;
            case "extension": filename = "synthetic.exe"; break;
            case "mismatch": filename = "synthetic.jpg"; mime = "image/jpeg"; break;
            case "mime": mime = "text/html"; break;
            case "signature": bytes[0] ^= 1; break;
            case "truncated": bytes = bytes[..^3]; break;
            case "crc": bytes[29] ^= 1; break;
            case "pdf-action": filename = "synthetic.pdf"; mime = "application/pdf"; bytes = Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(Pdf()).Replace("/Catalog", "/Catalog /OpenAction /JavaScript")); break;
            case "webp-size": filename = "synthetic.webp"; mime = "image/webp"; bytes = Image("webp"); bytes[4]++; break;
            case "jpeg-eof": filename = "synthetic.jpg"; mime = "image/jpeg"; bytes = Image("jpg")[..^2]; break;
            case "jpeg-quantization-reference": case "jpeg-huffman-reference":
                filename = "synthetic.jpg"; mime = "image/jpeg"; bytes = Image("jpg");
                int table = bytes.AsSpan().IndexOf(new byte[] { 255, kind == "jpeg-quantization-reference" ? (byte)0xDB : (byte)0xC4 });
                Assert.True(table > 0); bytes[table + 4] = 3; break;
        }
        using var response = await Upload(c, s, bytes, filename, mime); Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType });
        var current = await c.GetFromJsonAsync<StudioSessionView>(Root + s.Id); Assert.Equal(s.Version, current!.Version); Assert.Empty(current.Sources);
    }
    [Theory] [InlineData("../private.png")] [InlineData("..\\private.png")] [InlineData("C:private.png")]
    public void PathTraversalFilenamesAreNeverInterpreted(string filename) => Assert.Throws<StudioFault>(() => SourceIntake.Admit(Image("png"), filename, "image/png", Actor, DateTimeOffset.UtcNow));
    [Theory] [InlineData("sourceId")] [InlineData("sha256")] [InlineData("tenantId")]
    public async Task BrowserCannotSupplySourceIdentityFingerprintOrAuthority(string forged)
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await New(c);
        using var r = await Upload(c, s, forged: forged); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
    [Theory] [InlineData("admin-b")] [InlineData("other-a")]
    public async Task SourceAccessRequiresTenantAndOwnerDespiteForgedHeaders(string attacker)
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); using var other = await Login(host, attacker);
        var s = await View(await Upload(c, await New(c))); other.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-a");
        using var read = await other.GetAsync(Root + s.Id + "/sources"); using var add = await Upload(other, s); using var del = await Delete(other, s, s.Sources[0].SourceId);
        Assert.All(new[] { read, add, del }, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SourceMutationsRequireCsrf(bool forged)
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await View(await Upload(c, await New(c)));
        c.DefaultRequestHeaders.Remove("X-Studio-CSRF"); if (forged) c.DefaultRequestHeaders.Add("X-Studio-CSRF", "forged");
        using var add = await Upload(c, s); using var del = await Delete(c, s, s.Sources[0].SourceId);
        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode); Assert.Equal(HttpStatusCode.BadRequest, del.StatusCode);
    }
    [Fact] public async Task AnonymousSourceRoutesAreDenied()
    {
        await using var host = await StudioHttpHost.Start(); using var c = host.Client(); var id = new string('A', 64);
        foreach (var (method, suffix) in new[] { (HttpMethod.Get, ""), (HttpMethod.Post, ""), (HttpMethod.Delete, "/" + id) })
        { using var r = await c.SendAsync(new(method, Root + id + "/sources" + suffix)); Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode); }
    }
    [Theory] [InlineData("over-declared")] [InlineData("over-stream")] [InlineData("short")] [InlineData("long")]
    public async Task StreamSizeIsCheckedBeforeDuringAndAfterRead(string kind)
    {
        byte[] bytes = kind == "over-stream" ? new byte[SourceIntake.MaximumFileBytes + 1] : Image("png");
        long length = kind switch { "over-declared" => SourceIntake.MaximumFileBytes + 1, "short" => bytes.Length + 1, "long" => bytes.Length - 1, _ => 1 };
        using var stream = new MemoryStream(bytes); await Assert.ThrowsAsync<StudioFault>(() => SourceIntake.ReadBoundedAsync(stream, length, default));
        if (kind == "over-declared") Assert.Equal(0, stream.Position);
    }
    [Fact] public async Task HttpMultipartOversizeIsRejected()
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await New(c);
        using var response = await Upload(c, s, new byte[SourceIntake.MaximumFileBytes + 1]);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge });
    }
    [Fact] public async Task CountLimitAndStaleVersionCannotSilentlyOverwrite()
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await New(c); var old = s;
        for (int i = 0; i < SourceIntake.MaximumSources; i++) s = await View(await Upload(c, s));
        using var excess = await Upload(c, s); Assert.Equal(HttpStatusCode.BadRequest, excess.StatusCode);
        using var stale = await Upload(c, old); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var forged = await Delete(c, s, new string('F', 64)); Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
        Assert.Equal(SourceIntake.MaximumSources, (await c.GetFromJsonAsync<StudioSessionView>(Root + s.Id))!.Sources.Length);
    }
    [Fact] public void SharedTemplateAndSourceBudgetNeverExceedsGate4ABudget()
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var s = store.Create(Actor, "invoice");
        var source = SourceIntake.Admit(Image("png"), "s.png", "image/png", Actor, DateTimeOffset.UtcNow);
        s = store.Update(Actor, s.Id, s.Version, x => x with { Template = new byte[SourceIntake.MaximumSessionBytes - source.Bytes.Length].ToImmutableArray(), Sources = [source] });
        Assert.Equal(SourceIntake.MaximumSessionBytes, s.Template.Length + source.Bytes.Length);
        Assert.Throws<StudioFault>(() => store.Update(Actor, s.Id, s.Version, x => x with { Sources = x.Sources.Add(source with { Id = "second" }) }));
        Assert.Equal(s.Version, store.Read(Actor, s.Id).Version);
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Expire() => now += InMemoryUniversalDocSessionStore.Lifetime + TimeSpan.FromSeconds(1);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ExpiryReleasesSourcesAndPreventsMetadataAccess(bool cleanup)
    {
        var clock = new Clock(); await using var host = await StudioHttpHost.Start(clock); using var c = await Login(host);
        var s = await View(await Upload(c, await New(c))); clock.Expire(); var store = host.App.Services.GetRequiredService<IUniversalDocSessionStore>();
        if (cleanup) store.Cleanup(); using var r = await c.GetAsync(Root + s.Id + "/sources");
        Assert.Equal(cleanup ? HttpStatusCode.NotFound : HttpStatusCode.Gone, r.StatusCode); Assert.Throws<StudioFault>(() => store.Read(Actor, s.Id));
    }
    [Fact] public async Task SessionDeletionReleasesSourceReferencesAndStaticPathsNeverExposeBytes()
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await View(await Upload(c, await New(c))); var source = s.Sources[0];
        foreach (var path in new[] { "/sources/", "/uploads/", "/" })
        { using var r = await c.GetAsync(path + source.SourceId); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); }
        using var deleted = await c.SendAsync(new(HttpMethod.Delete, Root + s.Id) { Content = JsonContent.Create(new { expectedVersion = s.Version }) });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode); using var read = await c.GetAsync(Root + s.Id + "/sources"); Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }
    [Fact] public async Task ConcurrentUploadDeleteHasExactlyOneVersionWinner()
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await View(await Upload(c, await New(c)));
        var responses = await Task.WhenAll(Upload(c, s), Delete(c, s, s.Sources[0].SourceId));
        Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.OK)); Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(s.Version + 1, (await c.GetFromJsonAsync<StudioSessionView>(Root + s.Id))!.Version); foreach (var r in responses) r.Dispose();
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SourceAddOrDeleteClearsApprovedDocumentAndRequiresNewCycle(bool delete)
    {
        await using var host = await StudioHttpHost.Start(); using var c = await Login(host); var s = await Extracted(c);
        s = await View(await Upload(c, s)); s = await View(await Action(c, s, "extract-demo")); s = await View(await Action(c, await View(await Action(c, s, "review")), "approve"));
        Assert.Equal("Approved", s.ReviewState); s = await View(delete ? await Delete(c, s, s.Sources[0].SourceId) : await Upload(c, s));
        Assert.Equal("NoDocument", s.ReviewState); Assert.Null(s.DataRevision); Assert.Empty(s.Fields); Assert.Empty(s.Issues);
        foreach (var action in new[] { "review", "approve", "export" }) { using var r = await Action(c, s, action); Assert.Equal(HttpStatusCode.Conflict, r.StatusCode); }
        s = await View(await Action(c, s, "extract-demo")); s = await View(await Action(c, await View(await Action(c, s, "review")), "approve"));
        using var success = await Action(c, s, "export"); Assert.Equal(HttpStatusCode.OK, success.StatusCode);
    }
    private sealed class PausedStore(IUniversalDocSessionStore inner) : IUniversalDocSessionStore
    {
        public volatile bool PauseRead, PauseUpdate;
        public TaskCompletionSource Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(false);
        private void Pause() { Captured.TrySetResult(); if (!Release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException(); }
        public UniversalDocSession Create(UniversalDocActor a, string w) => inner.Create(a, w);
        public UniversalDocSession Read(UniversalDocActor a, string id) { var s = inner.Read(a, id); if (PauseRead) { PauseRead = false; Pause(); } return s; }
        public UniversalDocSession Update(UniversalDocActor a, string id, long v, Func<UniversalDocSession, UniversalDocSession> change)
        { if (PauseUpdate) { PauseUpdate = false; Pause(); } return inner.Update(a, id, v, change); }
        public void Remove(UniversalDocActor a, string id, long v) => inner.Remove(a, id, v);
        public void Cleanup() => inner.Cleanup();
    }
    [Theory] [InlineData("review")] [InlineData("approve")] [InlineData("export")]
    public async Task UploadWinningReviewApprovalOrExportRaceRejectsStaleTransition(string action)
    {
        PausedStore paused = null!; await using var host = await StudioHttpHost.Start(decorate: store => paused = new(store)); using var c = await Login(host); var s = await Extracted(c);
        if (action is "approve" or "export") s = await View(await Action(c, s, "review")); if (action == "export") s = await View(await Action(c, s, "approve"));
        paused.PauseRead = action == "export"; paused.PauseUpdate = action != "export";
        var pending = Action(c, s, action); await paused.Captured.Task.WaitAsync(TimeSpan.FromSeconds(30));
        try { s = await View(await Upload(c, s)); } finally { paused.Release.Set(); }
        using var denied = await pending; Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal("SESSION_VERSION_CONFLICT", (await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()); Assert.Equal("NoDocument", s.ReviewState);
    }
    [Fact] public void BoundedRandomMutationsNeverEscapeAsUnexpectedParserExceptions()
    {
        var random = new Random(431); foreach (var type in new[] { "png", "jpg", "webp", "pdf" })
        for (int i = 0; i < 128; i++)
        {
            var d = Bytes(type); d[random.Next(d.Length)] ^= (byte)random.Next(1, 256);
            try { SourceIntake.Admit(d, "synthetic." + type, Mime(type), Actor, DateTimeOffset.UtcNow); }
            catch (StudioFault) { } // Header admission is not complete entropy/harmlessness validation.
        }
    }
    private static byte[] PngWithRaw(byte[] raw)
    {
        using var output = new MemoryStream(); output.Write(Image("png").AsSpan(0, 33));
        using var compressed = new MemoryStream(); using (var z = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) z.Write(raw);
        var payload = "IDAT"u8.ToArray().Concat(compressed.ToArray()).ToArray();
        Span<byte> value = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(value, (uint)payload.Length - 4); output.Write(value); output.Write(payload);
        uint crc = uint.MaxValue; foreach (var b in payload) { crc ^= b; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0); }
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc); output.Write(value); output.Write(Image("png").AsSpan(Image("png").Length - 12)); return output.ToArray();
    }
    [Theory] [InlineData("png-bomb")] [InlineData("png-filter")] [InlineData("png-extra")]
    [InlineData("webp-animation")] [InlineData("jpeg-progressive")] [InlineData("pdf-xref")]
    [InlineData("png-trailing")] [InlineData("webp-trailing")]
    public void ComplexContainersInflationAndTrailingPayloadsAreRejected(string kind)
    {
        string type = kind.Split('-')[0] switch { "jpeg" => "jpg", var t => t }; byte[] data = Bytes(type);
        switch (kind)
        {
            case "png-bomb": data = PngWithRaw(new byte[4 * 1024 * 1024]); break;
            case "png-filter": data = PngWithRaw([5, 255, 255, 255]); break;
            case "png-extra": data = PngWithRaw([0, 255, 255, 255, 1]); break;
            case "webp-animation": "VP8X"u8.CopyTo(data.AsSpan(12)); break;
            case "jpeg-progressive": var marker = Array.IndexOf(data, (byte)0xC0); Assert.True(marker > 0); data[marker] = 0xC2; break;
            case "pdf-xref": data = Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(data).Replace("0000000009", "0000000008")); break;
            default: data = data.Concat("<script>synthetic</script>"u8.ToArray()).ToArray(); break;
        }
        Assert.Throws<StudioFault>(() => SourceIntake.Admit(data, "synthetic." + type, Mime(type), Actor, DateTimeOffset.UtcNow));
    }
    [Theory] [InlineData("source")] [InlineData("hash")] [InlineData("page")] [InlineData("region")] [InlineData("tenant")]
    public void TrustedHandoffUsesExistingPipelineToRejectForgedProviderObservations(string forged)
    {
        using var store = new InMemoryUniversalDocSessionStore(TimeProvider.System); var session = store.Create(Actor, "invoice");
        var source = SourceIntake.Admit(Pdf(), "synthetic.pdf", "application/pdf", Actor, DateTimeOffset.UtcNow);
        session = store.Update(Actor, session.Id, session.Version, s => s with { Sources = [source] });
        var input = new StudioRecognitionInput(session); var batch = StudioDemoCatalog.Observations(input.Context); var observation = batch.Observations[0];
        observation = forged switch
        {
            "source" => observation with { SourceId = "attacker" }, "hash" => observation with { SourceSha256 = new string('0', 64) },
            "page" => observation with { PageNumber = 2 }, "region" => observation with { Region = new(-1, 0, 1, 1) }, _ => observation
        };
        batch = batch with { Observations = batch.Observations.SetItem(0, observation), TenantId = forged == "tenant" ? "tenant-b" : batch.TenantId };
        var result = new ExtractionPipeline().Extract(input.Context, batch); Assert.NotEmpty(result.Issues); Assert.Empty(result.Documents);
    }
    [Fact] public void SourceAdmissionHasNoNetworkFilesystemOrRecognitionExecutionPath()
    {
        var directory = AppContext.BaseDirectory; while (!Directory.Exists(Path.Combine(directory, "backend"))) directory = Directory.GetParent(directory)!.FullName;
        var root = Path.Combine(directory, "backend/ShoeExportInvoice.Api/Features/UniversalDocEngine/Application");
        foreach (var filename in new[] { "SourceIntake.cs", "UniversalDocSourceWorkflow.cs" })
        {
            var code = File.ReadAllText(Path.Combine(root, filename));
            foreach (var forbidden in new[] { "HttpClient", "WebRequest", "File.Write", "FileStream", "Process.Start", "DbContext", "ObserveAsync(" }) Assert.DoesNotContain(forbidden, code);
        }
    }
}
