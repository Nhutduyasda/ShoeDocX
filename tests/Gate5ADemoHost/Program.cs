using Microsoft.AspNetCore.Antiforgery;
using ShoeExportInvoice.Tests.UniversalDocEngine;
var port = int.TryParse(Environment.GetEnvironmentVariable("STUDIO_DEMO_PORT"), out var p) ? p : 5288;
await using var host = await StudioHttpHost.Start(port: port, configure: app => {
    app.MapGet("/api/__test/gate5a-template", () => Results.File(Gate5AFixtures.Workbook("accounting"), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")).RequireAuthorization();
    app.MapPost("/api/__test/gate5a-verify", async (HttpContext context, IAntiforgery csrf) => {
        await csrf.ValidateRequestAsync(context);
        if (context.Request.ContentLength is null or > 2097152) return Results.BadRequest();
        using var data = new MemoryStream(); await context.Request.Body.CopyToAsync(data);
        Gate5AFixtures.Verify(data.ToArray()); return Results.Ok(new { status = "PASS", values = true, formulas = true, formats = true, merges = true, reference = true });
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));
});
await host.App.WaitForShutdownAsync();
