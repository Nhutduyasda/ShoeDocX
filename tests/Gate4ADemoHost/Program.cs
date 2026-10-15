using ShoeExportInvoice.Tests.UniversalDocEngine;

// Explicit loopback-only synthetic acceptance host, never production registration or deployment.
var port = int.TryParse(Environment.GetEnvironmentVariable("STUDIO_DEMO_PORT"), out var value) ? value : 5288;
await using var host = await StudioHttpHost.Start(port: port);
Console.WriteLine("Synthetic Studio acceptance host: " + host.Address);
Console.WriteLine("Fixture accounts only: admin-a / Studio@Test2026!; no durable data.");
await host.App.WaitForShutdownAsync();
