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
        // Non-root migration may not pull a process in from outside delegation.
        string membership = File.ReadAllText("/proc/self/cgroup");
        Limits.Require(membership.Contains("/shoedocx-decoder-" + geteuid() + "/", StringComparison.Ordinal),
            "SANDBOX_CGROUP_SUPERVISOR_OUTSIDE_DELEGATION");
        string group = Path.Combine(parent, "job-" + Guid.NewGuid().ToString("N")); Process? process = null; string stage = "CREATE_GROUP";
        try
        {
            Directory.CreateDirectory(group);
            stage = "MEMORY_LIMIT"; WriteControl(group, "memory.max", Limits.Memory.ToString());
            stage = "SWAP_LIMIT"; WriteControl(group, "memory.swap.max", "0");
            stage = "PID_LIMIT"; WriteControl(group, "pids.max", "64");
            stage = "CPU_LIMIT"; WriteControl(group, "cpu.max", "100000 100000");
            var start = new ProcessStartInfo("/bin/sh") { RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
            start.Environment.Clear(); foreach (var pair in EnvironmentBlock()) start.Environment[pair.Key] = pair.Value;
            var arguments = new List<string> { "--unshare-all", "--die-with-parent", "--new-session", "--cap-drop", "ALL",
                "--uid", "65534", "--gid", "65534", "--ro-bind", directory, "/app", "--ro-bind", group, "/limits",
                "--proc", "/proc", "--dev", "/dev", "--tmpfs", "/tmp", "--chdir", "/app" };
            foreach (string path in new[] { "/lib", "/lib64", "/usr/lib" }) if (Directory.Exists(path)) arguments.AddRange(new[] { "--ro-bind", path, path });
            arguments.Add(probe ? "/app/UniversalDocDecoder.Probe" : "/app/UniversalDocDecoder.Worker");
            // Trusted bootstrap waits before exec. No bwrap descendants can race cgroup attachment.
            // Only the release line exists in stdin at this point; source bytes wait for confined ready.
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("IFS= read -r release && [ \"$release\" = ATTACHED ] && exec /usr/bin/bwrap \"$@\"");
            start.ArgumentList.Add("shoedocx-bootstrap");
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            stage = "START_TRUSTED_BARRIER";
            process = Process.Start(start) ?? throw new ProtocolFault("SANDBOX_LAUNCH_FAILED");
            // Only trusted bootstrap executes before attachment; no source bytes are sent until confined ready.
            stage = "ATTACH_CGROUP"; WriteControl(group, "cgroup.procs", process.Id.ToString());
            Limits.Require(File.ReadAllLines(Path.Combine(group, "cgroup.procs")).Contains(process.Id.ToString()), "SANDBOX_CGROUP_ATTACHMENT_MISSING");
            stage = "RELEASE_BARRIER";
            process.StandardInput.WriteLine("ATTACHED"); process.StandardInput.Flush();
            return new LinuxSandbox { Process = process, Input = process.StandardInput.BaseStream, Output = process.StandardOutput.BaseStream,
                Error = process.StandardError.BaseStream, Group = group };
        }
        catch (Exception error)
        {
            try { if (process is { HasExited: false }) process.Kill(true); } catch { }
            try { if (Directory.Exists(group)) { WriteControl(group, "cgroup.kill", "1"); Directory.Delete(group); } } catch { }
            if (error is ProtocolFault) throw;
            // Stage/type only: exceptions can contain private host paths.
            throw new ProtocolFault("SANDBOX_LINUX_" + stage + "_" + (error is UnauthorizedAccessException ? "DENIED" : error is IOException ? "IO" : "FAILED"));
        }
    }
    public override void Kill()
    {
        try { WriteControl(Group, "cgroup.kill", "1"); } catch { }
        try { if (!Process.HasExited) Process.Kill(true); } catch { }
    }
    public override void Dispose()
    {
        Kill(); try { Process.WaitForExit(2000); } catch { }
        Input.Dispose(); Output.Dispose(); Error.Dispose(); Process.Dispose();
        try { Directory.Delete(Group); } catch { /* Visible leftover is a verification failure, never a success claim. */ }
    }
    // cgroupfs is a kernel control API, not a regular file. Never create/truncate its files.
    private static void WriteControl(string group, string name, string value)
    {
        using var file = new FileStream(Path.Combine(group, name), FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        file.Write(System.Text.Encoding.ASCII.GetBytes(value + "\n"));
    }
    [DllImport("libc")] private static extern uint geteuid();
}
