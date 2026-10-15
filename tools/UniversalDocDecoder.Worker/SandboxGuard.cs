using System.Runtime.InteropServices;
using UniversalDocDecoder.Protocol;

namespace UniversalDocDecoder.Worker;

internal static class SandboxGuard
{
    public static void Enter()
    {
        if (OperatingSystem.IsWindows()) Windows();
        else if (OperatingSystem.IsLinux()) Linux();
        else throw new ProtocolFault("SANDBOX_OS_UNSUPPORTED");
    }
    private static void Windows()
    {
        Limits.Require(OpenProcessToken(GetCurrentProcess(), 8, out var token), "SANDBOX_TOKEN_UNAVAILABLE");
        try
        {
            Limits.Require(GetTokenInformation(token, 29, out int appContainer, 4, out _) && appContainer == 1, "SANDBOX_APPCONTAINER_REQUIRED");
            Limits.Require(IsProcessInJob(GetCurrentProcess(), 0, out bool inJob) && inJob, "SANDBOX_JOB_REQUIRED");
        }
        finally { CloseHandle(token); }
    }
    private static void Linux()
    {
        Limits.Require(geteuid() != 0, "SANDBOX_NONROOT_REQUIRED");
        Limits.Require(File.Exists("/limits/memory.max") && File.ReadAllText("/limits/memory.max").Trim() == Limits.Memory.ToString() &&
            File.ReadAllText("/limits/pids.max").Trim() == "64", "SANDBOX_CGROUP_REQUIRED");
        var cpu = new RLimit { Current = 15, Maximum = 15 };
        Limits.Require(setrlimit(0, ref cpu) == 0 && prctl(38, 1, 0, 0, 0) == 0, "SANDBOX_RLIMIT_FAILED");
        var filter = seccomp_init(0x7fff0000); Limits.Require(filter != 0, "SANDBOX_SECCOMP_UNAVAILABLE");
        try
        {
            Limits.Require(seccomp_attr_set(filter, 4, 1) == 0, "SANDBOX_SECCOMP_TSYNC_FAILED");
            foreach (var name in new[] { "execve", "execveat", "fork", "vfork", "socket", "socketpair", "connect", "bind", "listen",
                "accept", "accept4", "ptrace", "process_vm_readv", "process_vm_writev", "mount", "umount2", "pivot_root", "setns", "unshare",
                "bpf", "keyctl", "add_key", "request_key", "userfaultfd", "io_uring_setup", "reboot", "kexec_load", "init_module", "finit_module" })
            {
                int syscall = seccomp_syscall_resolve_name(name);
                if (syscall >= 0) Limits.Require(seccomp_rule_add(filter, 0x00050001, syscall, 0) == 0, "SANDBOX_SECCOMP_RULE_FAILED");
            }
            int clone3 = seccomp_syscall_resolve_name("clone3");
            if (clone3 >= 0) Limits.Require(seccomp_rule_add(filter, 0x00050026, clone3, 0) == 0, "SANDBOX_SECCOMP_RULE_FAILED");
            // Permit clone only for threads (CLONE_THREAD); prohibit process creation while allowing CLR threads.
            int clone = seccomp_syscall_resolve_name("clone");
            var comparison = new ArgCompare { Argument = 0, Operation = 7, Mask = 0x10000, Value = 0 };
            Limits.Require(clone >= 0 && seccomp_rule_add_array(filter, 0x00050001, clone, 1, ref comparison) == 0 &&
                seccomp_load(filter) == 0, "SANDBOX_SECCOMP_LOAD_FAILED");
        }
        finally { seccomp_release(filter); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct RLimit { public ulong Current, Maximum; }
    [StructLayout(LayoutKind.Sequential)] private struct ArgCompare { public uint Argument, Operation; public ulong Mask, Value; }
    [DllImport("libc")] private static extern uint geteuid();
    [DllImport("libc")] private static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);
    [DllImport("libc")] private static extern int setrlimit(int resource, ref RLimit limit);
    [DllImport("libseccomp.so.2")] private static extern nint seccomp_init(uint action);
    [DllImport("libseccomp.so.2")] private static extern int seccomp_attr_set(nint context, int attribute, uint value);
    [DllImport("libseccomp.so.2")] private static extern int seccomp_syscall_resolve_name([MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport("libseccomp.so.2")] private static extern int seccomp_rule_add(nint context, uint action, int syscall, uint count);
    [DllImport("libseccomp.so.2")] private static extern int seccomp_rule_add_array(nint context, uint action, int syscall, uint count, ref ArgCompare comparison);
    [DllImport("libseccomp.so.2")] private static extern int seccomp_load(nint context);
    [DllImport("libseccomp.so.2")] private static extern void seccomp_release(nint context);
    [DllImport("kernel32.dll")] private static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsProcessInJob(nint process, nint job, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [DllImport("advapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(nint token, int kind, out int value, int length, out int returned);
}
