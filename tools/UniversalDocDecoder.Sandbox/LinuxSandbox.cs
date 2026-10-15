using System.Diagnostics;
using System.Runtime.InteropServices;
using UniversalDocDecoder.Protocol;

namespace UniversalDocDecoder.Sandbox;

internal sealed class LinuxSandbox : SandboxProcess
{
    public required string Group { get; init; }
    public new static SandboxProcess Start(string directory, bool probe)
    {
        Limits.Require(File.Exists("/usr/bin/bwrap") && geteuid() != 0, "SANDBOX_BWRAP_NONROOT_REQUIRED");
        string parent = "/sys/fs/cgroup/shoedocx-decoder-" + geteuid();
        Limits.Require(Directory.Exists(parent) && File.ReadAllText(Path.Combine(parent, "cgroup.controllers")).Contains("memory"), "SANDBOX_CGROUP_DELEGATION_REQUIRED");
        string group = Path.Combine(parent, "job-" + Guid.NewGuid().ToString("N")); Process? process = null;
        try
        {
            Directory.CreateDirectory(group);
            File.WriteAllText(Path.Combine(group, "memory.max"), Limits.Memory.ToString());
            File.WriteAllText(Path.Combine(group, "memory.swap.max"), "0");
            File.WriteAllText(Path.Combine(group, "pids.max"), "64");
            File.WriteAllText(Path.Combine(group, "cpu.max"), "100000 100000");
            var start = new ProcessStartInfo("/usr/bin/bwrap") { RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
            start.Environment.Clear(); foreach (var pair in EnvironmentBlock()) start.Environment[pair.Key] = pair.Value;
            var arguments = new List<string> { "--unshare-all", "--die-with-parent", "--new-session", "--cap-drop", "ALL",
                "--uid", "65534", "--gid", "65534", "--ro-bind", directory, "/app", "--ro-bind", group, "/limits",
                "--proc", "/proc", "--dev", "/dev", "--tmpfs", "/tmp", "--chdir", "/app" };
            foreach (string path in new[] { "/lib", "/lib64", "/usr/lib" }) if (Directory.Exists(path)) arguments.AddRange(new[] { "--ro-bind", path, path });
            arguments.Add(probe ? "/app/UniversalDocDecoder.Probe" : "/app/UniversalDocDecoder.Worker");
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            process = Process.Start(start) ?? throw new ProtocolFault("SANDBOX_LAUNCH_FAILED");
            // Only trusted bootstrap executes before attachment; no source bytes are sent until confined ready.
            File.WriteAllText(Path.Combine(group, "cgroup.procs"), process.Id.ToString());
            return new LinuxSandbox { Process = process, Input = process.StandardInput.BaseStream, Output = process.StandardOutput.BaseStream,
                Error = process.StandardError.BaseStream, Group = group };
        }
        catch
        {
            try { if (process is { HasExited: false }) process.Kill(true); } catch { }
            try { if (Directory.Exists(group)) { File.WriteAllText(Path.Combine(group, "cgroup.kill"), "1"); Directory.Delete(group); } } catch { }
            throw new ProtocolFault("SANDBOX_LINUX_SETUP_FAILED");
        }
    }
    public override void Kill()
    {
        try { File.WriteAllText(Path.Combine(Group, "cgroup.kill"), "1"); } catch { }
        try { if (!Process.HasExited) Process.Kill(true); } catch { }
    }
    public override void Dispose()
    {
        Kill(); try { Process.WaitForExit(2000); } catch { }
        Input.Dispose(); Output.Dispose(); Error.Dispose(); Process.Dispose();
        try { Directory.Delete(Group); } catch { /* Visible leftover is a verification failure, never a success claim. */ }
    }
    [DllImport("libc")] private static extern uint geteuid();
}
