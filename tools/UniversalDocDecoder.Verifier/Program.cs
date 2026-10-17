using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using UniversalDocDecoder.Protocol;
using UniversalDocDecoder.Sandbox;

// Operator-supplied locations belong only to this isolated synthetic verification executable.
if (args.Length != 3) { Console.Error.WriteLine("Expected trusted publish directory, synthetic fixture manifest, evidence path"); return 2; }
string runtime = Path.GetFullPath(args[0]), manifest = Path.GetFullPath(args[1]), evidence = Path.GetFullPath(args[2]);
var checks = new List<object>(); var measurements = new List<object>(); bool confinement = false;
string canaryDirectory = Path.Combine(Path.GetDirectoryName(evidence)!, "private-canary-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(canaryDirectory); string canary = Path.Combine(canaryDirectory, "synthetic.secret"); File.WriteAllText(canary, "SYNTHETIC_CANARY");
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
try
{
    using (var accessible = new TcpClient()) await accessible.ConnectAsync(IPAddress.Loopback, port);
    Limits.Require(File.ReadAllText(canary) == "SYNTHETIC_CANARY", "CANARY_PARENT_CONTROL_FAILED");
    foreach (string operation in new[] { "network", "read", "write", "spawn" })
    {
        var watch = Stopwatch.StartNew();
        try
        {
            using var sandbox = SandboxProcess.Start(runtime, verificationProbe: true);
            using var deadline = new CancellationTokenSource(8000); using var registration = deadline.Token.Register(sandbox.Kill);
            Message ready;
            try { ready = await Wire.Read<Message>(sandbox.Output, deadline.Token); }
            catch (EndOfStreamException)
            {
                var diagnostic = new byte[4096]; int size = await sandbox.Error.ReadAsync(diagnostic, deadline.Token);
                string text = System.Text.Encoding.UTF8.GetString(diagnostic, 0, size);
                string codes = string.Join('_', System.Text.RegularExpressions.Regex.Matches(text, @"0x[0-9A-Fa-f]{8}").Select(m => m.Value));
                await sandbox.Process.WaitForExitAsync(deadline.Token);
                throw new ProtocolFault("BOOTSTRAP_EXIT_" + sandbox.Process.ExitCode.ToString("X8") + "_" + codes);
            }
            Limits.Require(ready.Kind == "ready", ready.Code.Length > 0 ? ready.Code : "SANDBOX_NOT_READY");
            await Wire.Write(sandbox.Input, new ProbeRequest(operation, operation == "write" ? Path.Combine(canaryDirectory, "escape") : canary, port), deadline.Token);
            sandbox.Input.Dispose(); var response = await Wire.Read<ProbeResponse>(sandbox.Output, deadline.Token);
            Limits.Require(!response.Escaped && response.Reason is "UnauthorizedAccessException" or "IOException" or "DirectoryNotFoundException" or "FileNotFoundException" or "SocketException" or "Win32Exception", "OS_DENIAL_NOT_DEMONSTRATED");
            await sandbox.Process.WaitForExitAsync(deadline.Token); Limits.Require(sandbox.Process.ExitCode == 0, "PROBE_EXIT_FAILED");
            checks.Add(new { check = operation, status = "PASS", reason = response.Reason, elapsedMs = watch.ElapsedMilliseconds });
            confinement = true;
        }
        catch (Exception error)
        {
            checks.Add(new { check = operation, status = error is ProtocolFault && error.Message == "OS_DENIAL_NOT_DEMONSTRATED" ? "FAIL" : "BLOCKED", code = Safe(error), elapsedMs = watch.ElapsedMilliseconds }); confinement = false;
            // Do not run a decoder outside confinement or pretend dependent checks executed.
            break;
        }
    }
    if (confinement)
    {
        foreach (string operation in new[] { "hang", "crash", "memory", "cpu", "oversize", "malformed", "incomplete", "metadata", "hash" })
        {
            var watch = Stopwatch.StartNew();
            try
            {
                using var sandbox = SandboxProcess.Start(runtime, verificationProbe: true);
                using var deadline = new CancellationTokenSource(operation == "cpu" ? 22000 : 8000); using var registration = deadline.Token.Register(sandbox.Kill);
                var ready = await Wire.Read<Message>(sandbox.Output, deadline.Token); Limits.Require(ready.Kind == "ready", "SANDBOX_NOT_READY");
                await Wire.Write(sandbox.Input, new ProbeRequest(operation), deadline.Token); sandbox.Input.Dispose();
                string result = "";
                if (operation == "hang")
                {
                    using var cancel = new CancellationTokenSource(300); using var cancelRegistration = cancel.Token.Register(sandbox.Kill);
                    await sandbox.Process.WaitForExitAsync(deadline.Token); result = "CANCELLED_PROCESS_EXITED";
                }
                else if (operation is "memory" or "cpu" or "crash")
                {
                    await sandbox.Process.WaitForExitAsync(deadline.Token);
                    Limits.Require(!deadline.IsCancellationRequested && sandbox.Process.ExitCode != 0, "RESOURCE_LIMIT_NOT_DEMONSTRATED"); result = "NONZERO_EXIT";
                }
                else
                {
                    try
                    {
                        var response = await Wire.Read<Message>(sandbox.Output, deadline.Token);
                        if (operation == "metadata") Wire.Dimensions(response.Width, response.Height);
                        else Limits.Require(response.Sha256 == new string('A', 64), "IPC_SOURCE_MISMATCH");
                        throw new Exception("INVALID_OUTPUT_ACCEPTED");
                    }
                    catch (Exception error) when (error is ProtocolFault or JsonException or EndOfStreamException) { result = error.GetType().Name; }
                    sandbox.Kill(); await sandbox.Process.WaitForExitAsync(deadline.Token);
                }
                measurements.Add(new { operation, peakMemoryBytes = sandbox.Process.PeakWorkingSet64, cpuMs = sandbox.Process.TotalProcessorTime.TotalMilliseconds,
                    wallMs = watch.ElapsedMilliseconds, exitCode = sandbox.Process.ExitCode, processLimit = OperatingSystem.IsWindows() ? 1 : 64 });
                checks.Add(new { check = operation, status = operation is "memory" or "cpu" ? "UNPROVEN" : "PASS", result });
            }
            catch (Exception error) { checks.Add(new { check = operation, status = "FAIL", code = Safe(error) }); }
        }
        var fixtures = JsonSerializer.Deserialize<List<Fixture>>(File.ReadAllText(manifest))!;
        foreach (var fixture in fixtures)
        {
            try
            {
                var bytes = Convert.FromBase64String(fixture.Base64); var request = new DecodeRequest(1, fixture.MediaType, Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length);
                var result = await Supervisor.Decode(runtime, request, bytes);
                Limits.Require(fixture.Pages > 0 && result.Pages.Count == fixture.Pages, "FIXTURE_PAGE_COUNT");
                foreach (var page in result.Pages)
                {
                    Limits.Require(page.Bgra.Length == page.Width * page.Height * 4, "FIXTURE_PIXEL_BUFFER");
                    if (fixture.Width > 0) Limits.Require(page.Width == fixture.Width && page.Height == fixture.Height, "FIXTURE_DIMENSIONS");
                }
                if (fixture.Text is { Length: > 0 }) Limits.Require(result.Pages.Any(p => p.Text.Contains(fixture.Text, StringComparison.Ordinal)), "FIXTURE_TEXT");
                measurements.Add(new { fixture = fixture.Name, result.Measurement }); checks.Add(new { check = fixture.Name, status = "PASS", pages = result.Pages.Count });
            }
            catch (Exception error)
            {
                checks.Add(new { check = fixture.Name, status = fixture.Pages == 0 && error is DecoderRejected && error.Message == fixture.ExpectedCode ? "PASS" : "FAIL", code = Safe(error) });
            }
        }
    }
    else checks.Add(new { check = "dependent_decoder_and_resource_tests", status = "BLOCKED", code = "NO_UNCONFINED_FALLBACK" });
    // PASS is a conjunction over the mandatory inventory, not a success exit-code toggle.
    // Missing implementations/evidence remain absent checks and produce explicit blockers.
    var required = new[] { "network", "read", "write", "spawn", "hang", "crash", "memory", "cpu", "oversize", "malformed", "incomplete", "metadata", "hash",
        "handle-inheritance", "parent-death", "descendant-cleanup", "concurrent-admission", "process-count", "wall-timeout", "bounded-output",
        "crash-recovery", "pdf-expanded-stream-budget", "pdf-object-recursion-policy", "pdf-feature-policy", "per-page-cpu", "native-review" }
        .Concat(JsonSerializer.Deserialize<List<Fixture>>(File.ReadAllText(manifest))!.Select(f => f.Name)).ToArray();
    var observed = checks.Select(c => JsonSerializer.SerializeToElement(c)).ToArray();
    var blockers = required.Where(name => !observed.Any(c => c.GetProperty("check").GetString() == name && c.GetProperty("status").GetString() == "PASS"))
        .Select(name => "MISSING_OR_UNPASSED:" + name).ToArray();
    foreach (var name in required.Where(name => !observed.Any(c => c.GetProperty("check").GetString() == name)))
        checks.Add(new { check = name, status = "NOT_RUN", code = "MANDATORY_EVIDENCE_ABSENT" });
    bool failed = observed.Any(c => c.GetProperty("status").GetString() == "FAIL");
    string decision = failed ? "FAIL" : blockers.Length > 0 || !confinement ? "PARTIAL" : "PASS";
    var report = new { phase = "4B.2B-1", status = decision, phase2Authorized = decision == "PASS", os = Environment.OSVersion.ToString(),
        workerBuild = Limits.Build, confinementChecksCompleted = confinement, checks, measurements, blockers };
    File.WriteAllText(evidence, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(new { report.phase, report.status, report.phase2Authorized, report.confinementChecksCompleted, checkCount = checks.Count, report.blockers }));
    return decision == "PASS" ? 0 : 1;
}
finally { listener.Stop(); File.Delete(canary); if (File.Exists(Path.Combine(canaryDirectory, "escape"))) File.Delete(Path.Combine(canaryDirectory, "escape")); Directory.Delete(canaryDirectory); }

static string Safe(Exception error) => error is ProtocolFault or DecoderRejected ? error.Message : error is System.ComponentModel.Win32Exception win ? "WIN32_" + win.NativeErrorCode + "_" + win.Data["stage"] : error.GetType().Name;
record ProbeRequest(string Operation, string Canary = "", int Port = 0);
record ProbeResponse(bool Escaped, string Reason);
record Fixture(string Name, string MediaType, string Base64, int Pages, int Width = 0, int Height = 0, string? Text = null, string? ExpectedCode = null);
