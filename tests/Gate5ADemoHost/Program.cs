using Microsoft.AspNetCore.Antiforgery;
using ShoeExportInvoice.Tests.UniversalDocEngine;
var port = int.TryParse(Environment.GetEnvironmentVariable("STUDIO_DEMO_PORT"), out var p) ? p : 5288;
await using var host = await StudioHttpHost.Start(port: port, configure: app => {
    app.MapGet("/api/__test/gate5b-template", () => Results.File(Gate5BFixtures.Workbook(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")).RequireAuthorization();
    app.MapGet("/api/__test/gate5b-example", () => Results.File(Gate5BFixtures.Workbook(2, 2, true), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")).RequireAuthorization();
    app.MapPost("/api/__test/gate5b-verify", async (HttpContext context, IAntiforgery csrf) => {
        await csrf.ValidateRequestAsync(context);
        if (context.Request.ContentLength is null or > 2097152) return Results.BadRequest();
        using var data = new MemoryStream(); await context.Request.Body.CopyToAsync(data);
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(data.ToArray()));
        if (!wb.Worksheets.Select(s => s.Name).SequenceEqual(new[] { "Data1", "Data2", "Reference" })) return Results.BadRequest();
        foreach (var name in new[] { "Data1", "Data2" }) {
            var s = wb.Worksheet(name);
            if (s.Cell("A4").GetString() != "0001" || s.Cell("A5").GetString() != "0002" ||
                s.Cell("B1").GetValue<decimal>() != 15 || s.Cell("B4").GetValue<decimal>() != 2.5m ||
                s.Cell("C5").GetValue<decimal>() != 3 || s.Cell("D5").GetValue<decimal>() != 7.5m ||
                s.Cell("E5").FormulaA1 != "B5*C5" || s.Cell("E6").FormulaA1 != "SUM(E4:E5)" ||
                s.Cell("B5").Style.NumberFormat.Format != "0.00" ||
                s.Cell("B5").Style.Border.BottomBorder != ClosedXML.Excel.XLBorderStyleValues.Thin ||
                !s.MergedRanges.Any(r => r.RangeAddress.ToString() == "A7:C7")) return Results.BadRequest();
        }
        if (wb.Worksheet("Reference").Cell("A1").GetString() != "Dữ liệu tham chiếu 00123") return Results.BadRequest();
        return Results.Ok(new { status = "PASS", worksheets = 2, values = true, formulas = true, formats = true, merges = true, reference = true });
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));
    app.MapGet("/api/__test/gate5a-template", () => Results.File(Gate5AFixtures.Workbook("accounting"), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")).RequireAuthorization();
    app.MapPost("/api/__test/gate5a-verify", async (HttpContext context, IAntiforgery csrf) => {
        await csrf.ValidateRequestAsync(context);
        if (context.Request.ContentLength is null or > 2097152) return Results.BadRequest();
        using var data = new MemoryStream(); await context.Request.Body.CopyToAsync(data);
        Gate5AFixtures.Verify(data.ToArray()); return Results.Ok(new { status = "PASS", values = true, formulas = true, formats = true, merges = true, reference = true });
    }).RequireAuthorization(policy => policy.RequireRole("Admin"));
});
await host.App.WaitForShutdownAsync();
