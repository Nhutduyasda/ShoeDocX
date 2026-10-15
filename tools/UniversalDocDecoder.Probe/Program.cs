using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using UniversalDocDecoder.Protocol;
using UniversalDocDecoder.Worker;

// Separate verification-only executable; the actual decoder has no probe operations or caller paths.
try
{
    SandboxGuard.Enter();
    using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
    await Wire.Write(output, new Message(1, "ready", Limits.Build));
    var request = await Wire.Read<ProbeRequest>(input);
    bool escaped = false;
    try
    {
        switch (request.Operation)
        {
            case "network": using (var socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
                { using var timeout = new CancellationTokenSource(1500); await socket.ConnectAsync(IPAddress.Loopback, request.Port, timeout.Token); escaped = true; } break;
            case "read": _ = File.ReadAllBytes(request.Canary); escaped = true; break;
            case "write": File.WriteAllText(request.Canary, "synthetic"); escaped = true; break;
            case "spawn": using (var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true }))
                { escaped = child is not null; child?.Kill(true); } break;
            case "hang": Thread.Sleep(Timeout.Infinite); break;
            case "cpu": while (true) { _ = Math.Sqrt(Environment.TickCount64); }
            case "memory":
                var allocations = new List<nint>();
                while (true)
                {
                    var block = Marshal.AllocHGlobal(8 * 1024 * 1024); allocations.Add(block);
                    for (int i = 0; i < 8 * 1024 * 1024; i += 4096) Marshal.WriteByte(block, i, 1);
                }
            case "crash": Environment.FailFast("SYNTHETIC_PROBE_CRASH"); break;
            case "oversize": await output.WriteAsync(BitConverter.GetBytes(Limits.Header + 1)); return 0;
            case "malformed": await output.WriteAsync(BitConverter.GetBytes(1)); await output.WriteAsync(new byte[] { (byte)'!' }); return 0;
            case "hash": await Wire.Write(output, new Message(1, "page-start", Limits.Build, new string('0', 64), 1, 1)); return 0;
            case "metadata": await Wire.Write(output, new Message(1, "page", Limits.Build, Page: -1, Width: int.MaxValue, Height: int.MaxValue)); return 0;
            case "incomplete": await output.WriteAsync(new byte[] { 10, 0, 0, 0, (byte)'{' }); return 0;
            default: throw new ProtocolFault("PROBE_UNKNOWN");
        }
    }
    catch (Exception e) when (e is UnauthorizedAccessException or IOException or SocketException or System.ComponentModel.Win32Exception or OperationCanceledException)
    { await Wire.Write(output, new ProbeResponse(false, e.GetType().Name)); return 0; }
    await Wire.Write(output, new ProbeResponse(escaped, escaped ? "OPERATION_SUCCEEDED" : "NO_OPERATION")); return escaped ? 3 : 0;
}
catch { return 2; }

record ProbeRequest(string Operation, string Canary = "", int Port = 0);
record ProbeResponse(bool Escaped, string Reason);
