using Microsoft.AspNetCore.Antiforgery;
using ShoeExportInvoice.Tests.UniversalDocEngine;

// Test-only loopback fixture. No production registration; deterministic browser TTL without sleeps.
var clock = new FixtureClock();
var port = int.TryParse(Environment.GetEnvironmentVariable("STUDIO_DEMO_PORT"), out var p) ? p : 5288;
await using var host = await StudioHttpHost.Start(clock, port: port, configure: app =>
app.MapPost("/api/__test/expire-sessions", async (HttpContext context, IAntiforgery csrf) =>
{
    await csrf.ValidateRequestAsync(context); clock.Expire(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin")));
Console.WriteLine("Synthetic Gate 4B.1 loopback host ready.");
await host.App.WaitForShutdownAsync();

sealed class FixtureClock : TimeProvider
{
    private long offset;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref offset));
    public void Expire() => Interlocked.Add(ref offset, TimeSpan.FromMinutes(21).Ticks);
}
