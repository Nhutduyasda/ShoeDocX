using System.Diagnostics;
using System.Text;
using UniversalDocDecoder.Protocol;

namespace UniversalDocDecoder.Sandbox;

public abstract class SandboxProcess : IDisposable
{
    public required Process Process { get; init; }
    public required Stream Input { get; init; }
    public required Stream Output { get; init; }
    public required Stream Error { get; init; }
    public abstract void Kill();
    public abstract void Dispose();
    // The directory is trusted deployment configuration, never from a document or browser request.
    public static SandboxProcess Start(string trustedDirectory, bool verificationProbe = false)
    {
        var directory = Path.GetFullPath(Path.Combine(trustedDirectory, verificationProbe ? "probe" : "worker"));
        Limits.Require(Directory.Exists(directory) && !new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint), "SANDBOX_DIRECTORY_INVALID");
        return OperatingSystem.IsWindows() ? WindowsSandbox.Start(directory, verificationProbe) : OperatingSystem.IsLinux()
            ? LinuxSandbox.Start(directory, verificationProbe) : throw new ProtocolFault("SANDBOX_OS_UNSUPPORTED");
    }
    internal static Dictionary<string, string> EnvironmentBlock() => new()
    {
        ["DOTNET_EnableDiagnostics"] = "0", ["DOTNET_GCHeapHardLimit"] = "0x08000000",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1", ["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1",
        ["COMPlus_DbgEnableMiniDump"] = "0", ["LANG"] = "C"
    };
}

public sealed record Measurement(long PeakMemoryBytes, double CpuMilliseconds, double WallMilliseconds, int ExitCode, long InputBytes, long OutputBytes);
public sealed record DecodeResult(IReadOnlyList<DecodedPage> Pages, Measurement Measurement);

public static class Supervisor
{
    private static readonly SemaphoreSlim Capacity = new(2, 2);
    public static async Task<DecodeResult> Decode(string directory, DecodeRequest request, byte[] source, CancellationToken cancellation = default)
    {
        Wire.Validate(request); Wire.Hash(request, source);
        Limits.Require(source.Length == request.ByteLength, "SOURCE_LENGTH_MISMATCH");
        Limits.Require(await Capacity.WaitAsync(0, cancellation), "WORKER_CAPACITY");
        try
        {
            using var sandbox = SandboxProcess.Start(directory);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(Limits.JobWall);
            using var kill = deadline.Token.Register(sandbox.Kill);
            var timer = Stopwatch.StartNew(); long outputBytes = 0, textBytes = 0, pixels = 0, peak = 0;
            var errors = DrainErrors(sandbox.Error, deadline.Token);
            try
            {
                var ready = await Wire.Read<Message>(sandbox.Output, deadline.Token); Check(ready, "ready", "");
                await Wire.Write(sandbox.Input, request, deadline.Token); await sandbox.Input.WriteAsync(source, deadline.Token); sandbox.Input.Dispose();
                var pages = new List<DecodedPage>(); int count = 0;
                while (true)
                {
                    var start = await Wire.Read<Message>(sandbox.Output, deadline.Token);
                    if (start.Kind == "complete")
                    {
                        Check(start, "complete", request.Sha256);
                        Limits.Require(pages.Count > 0 && pages.Count == count && start.Count == count, "IPC_INCOMPLETE"); break;
                    }
                    Check(start, "page-start", request.Sha256);
                    Limits.Require(start.Page == pages.Count + 1 && start.Count is > 0 and <= Limits.Pages &&
                        (count == 0 || count == start.Count) && start.Page <= start.Count, "IPC_PAGE_INVALID"); count = start.Count;
                    using var pageTimeout = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token); pageTimeout.CancelAfter(Limits.PageWall);
                    using var pageKill = pageTimeout.Token.Register(sandbox.Kill);
                    var message = await Wire.Read<Message>(sandbox.Output, pageTimeout.Token); Check(message, "page", request.Sha256);
                    Wire.Dimensions(message.Width, message.Height);
                    Limits.Require(message.Page == start.Page && message.Count == count && message.PixelBytes == checked(message.Width * message.Height * 4) &&
                        message.TextBytes is >= 0 and <= Limits.TextBytes, "IPC_PAGE_INVALID");
                    pixels += (long)message.Width * message.Height; textBytes += message.TextBytes;
                    outputBytes += message.PixelBytes + message.TextBytes;
                    Limits.Require(pixels <= Limits.JobPixels && textBytes <= Limits.TextBytes && outputBytes <= Limits.Output, "IPC_OUTPUT_LIMIT");
                    var bgra = new byte[message.PixelBytes]; var text = new byte[message.TextBytes];
                    await sandbox.Output.ReadExactlyAsync(bgra, pageTimeout.Token); await sandbox.Output.ReadExactlyAsync(text, pageTimeout.Token);
                    pages.Add(new(message.Page, message.Width, message.Height, bgra, new UTF8Encoding(false, true).GetString(text)));
                    sandbox.Process.Refresh(); peak = Math.Max(peak, sandbox.Process.PeakWorkingSet64);
                }
                Limits.Require(await sandbox.Output.ReadAsync(new byte[1], deadline.Token) == 0, "IPC_TRAILING_OUTPUT");
                await sandbox.Process.WaitForExitAsync(deadline.Token); await errors;
                Limits.Require(sandbox.Process.ExitCode == 0, "WORKER_EXIT_FAILED");
                return new(pages, new(Math.Max(peak, sandbox.Process.PeakWorkingSet64), sandbox.Process.TotalProcessorTime.TotalMilliseconds,
                    timer.Elapsed.TotalMilliseconds, sandbox.Process.ExitCode, source.Length, outputBytes));
            }
            finally { sandbox.Kill(); }
        }
        finally { Capacity.Release(); }
    }
    private static void Check(Message message, string kind, string hash)
    {
        if (message.Version == 1 && message.Build == Limits.Build && message.Kind == "failed" && message.Code.Length is > 0 and <= 100 &&
            message.Code.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_')) throw new DecoderRejected(message.Code);
        Limits.Require(message.Version == 1 && message.Build == Limits.Build && message.Kind == kind && message.Sha256 == hash, "IPC_RESPONSE_INVALID");
    }
    public static async Task DrainErrors(Stream input, CancellationToken ct)
    {
        var buffer = new byte[1024]; int total = 0, read;
        while ((read = await input.ReadAsync(buffer, ct)) != 0) { total += read; Limits.Require(total <= 4096, "IPC_STDERR_LIMIT"); }
    }
}
