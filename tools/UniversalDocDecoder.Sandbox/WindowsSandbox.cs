using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using UniversalDocDecoder.Protocol;

namespace UniversalDocDecoder.Sandbox;

[SupportedOSPlatform("windows")]
internal sealed class WindowsSandbox : SandboxProcess
{
    public required nint Job { get; init; }
    public required string Profile { get; init; }
    public required string DirectoryPath { get; init; }
    public required FileSystemAccessRule RuntimeRule { get; init; }
    public new static SandboxProcess Start(string directory, bool probe)
    {
        string profile = "ShoeDocX.Decoder.Poc." + Guid.NewGuid().ToString("N"); nint sid = 0, attributes = 0, job = 0;
        var allocated = new List<nint>(); AnonymousPipeServerStream? input = null, output = null, error = null;
        FileSystemAccessRule? rule = null; string? privateProfilePath = null; ProcessInformation process = default; bool created = false, attributesInitialized = false;
        try
        {
            int hr = CreateAppContainerProfile(profile, profile, "ShoeDocX synthetic isolated decoder POC", 0, 0, out sid);
            if (hr < 0) throw new ProtocolFault("SANDBOX_PROFILE_CREATE_" + hr.ToString("X8"));
            var identity = new SecurityIdentifier(sid);
            rule = new FileSystemAccessRule(identity, FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
            var folder = new DirectoryInfo(directory); var acl = folder.GetAccessControl(); acl.AddAccessRule(rule); folder.SetAccessControl(acl);
            // Deny the container's default writable profile storage: this worker requires pipes only.
            if (ConvertSidToStringSid(sid, out var sidText))
            {
                try
                {
                    hr = GetAppContainerFolderPath(Marshal.PtrToStringUni(sidText)!, out var profilePath);
                    if (hr >= 0)
                    {
                        try
                        {
                            privateProfilePath = Marshal.PtrToStringUni(profilePath)!;
                            var storage = new DirectoryInfo(privateProfilePath); var access = storage.GetAccessControl();
                            access.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles,
                                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny)); storage.SetAccessControl(access);
                        }
                        finally { Marshal.FreeCoTaskMem(profilePath); }
                    }
                    else throw new ProtocolFault("SANDBOX_PROFILE_STORAGE_FAILED");
                }
                finally { LocalFree(sidText); }
            }
            else throw new ProtocolFault("SANDBOX_PROFILE_SID_FAILED");
            input = new(PipeDirection.Out, HandleInheritability.Inheritable); output = new(PipeDirection.In, HandleInheritability.Inheritable); error = new(PipeDirection.In, HandleInheritability.Inheritable);
            nint size = 0; InitializeProcThreadAttributeList(0, 3, 0, ref size); attributes = Marshal.AllocHGlobal(size);
            Check(InitializeProcThreadAttributeList(attributes, 3, 0, ref size), "attributes"); attributesInitialized = true;
            nint capability = Allocate(new SecurityCapabilities { AppContainerSid = sid }, allocated);
            Check(UpdateProcThreadAttribute(attributes, 0, 0x20009, capability, (nuint)Marshal.SizeOf<SecurityCapabilities>(), 0, 0));
            var handles = new[] { input.ClientSafePipeHandle.DangerousGetHandle(), output.ClientSafePipeHandle.DangerousGetHandle(), error.ClientSafePipeHandle.DangerousGetHandle() };
            nint handleList = Marshal.AllocHGlobal(nint.Size * 3); allocated.Add(handleList); Marshal.Copy(handles, 0, handleList, 3);
            Check(UpdateProcThreadAttribute(attributes, 0, 0x20002, handleList, (nuint)(nint.Size * 3), 0, 0));
            nint childPolicy = Marshal.AllocHGlobal(4); allocated.Add(childPolicy); Marshal.WriteInt32(childPolicy, 1);
            Check(UpdateProcThreadAttribute(attributes, 0, 0x2000e, childPolicy, 4, 0, 0));
            job = CreateJobObject(0, null); Check(job != 0, "job-create");
            var limits = new ExtendedLimits { Basic = new BasicLimits { PerProcessUserTimeLimit = 15 * 10_000_000L,
                PerJobUserTimeLimit = 15 * 10_000_000L, ActiveProcessLimit = 1, LimitFlags = 0x2000 | 0x100 | 0x200 | 0x2 | 0x4 | 0x8 },
                ProcessMemoryLimit = Limits.Memory, JobMemoryLimit = Limits.Memory };
            Check(SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()), "job-limits");
            var startup = new StartupInfoEx { StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100,
                StdInput = handles[0], StdOutput = handles[1], StdError = handles[2] }, AttributeList = attributes };
            var environment = EnvironmentBlock(); environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            environment["windir"] = environment["SystemRoot"]; environment["SystemDrive"] = Path.GetPathRoot(environment["SystemRoot"])!.TrimEnd('\\');
            environment["USERPROFILE"] = environment["LOCALAPPDATA"] = environment["APPDATA"] = privateProfilePath!;
            environment["TEMP"] = environment["TMP"] = "C:\\ShoeDocX-No-Writable-Temp";
            string block = string.Join('\0', environment.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Key + "=" + x.Value)) + "\0\0";
            nint env = Marshal.StringToHGlobalUni(block); allocated.Add(env);
            string executable = Path.Combine(directory, probe ? "UniversalDocDecoder.Probe.exe" : "UniversalDocDecoder.Worker.exe");
            Check(CreateProcess(executable, new System.Text.StringBuilder('"' + executable + '"'), 0, 0, true,
                0x80000 | 0x400 | 4 | 0x08000000, env, directory, ref startup, out process), "create-process"); created = true;
            Check(AssignProcessToJobObject(job, process.Process), "job-assign");
            var managed = Process.GetProcessById((int)process.ProcessId);
            _ = managed.Handle; // Retain a query/wait handle even when bootstrap fails immediately.
            Check(ResumeThread(process.Thread) != uint.MaxValue, "resume");
            input.DisposeLocalCopyOfClientHandle(); output.DisposeLocalCopyOfClientHandle(); error.DisposeLocalCopyOfClientHandle();
            return new WindowsSandbox { Process = managed, Input = input, Output = output, Error = error, Job = job,
                Profile = profile, DirectoryPath = directory, RuntimeRule = rule };
        }
        catch
        {
            if (created) TerminateProcess(process.Process, 2);
            if (job != 0) CloseHandle(job);
            input?.Dispose(); output?.Dispose(); error?.Dispose();
            if (rule is not null) RemoveRule(directory, rule);
            DeleteAppContainerProfile(profile); throw;
        }
        finally
        {
            if (created) { CloseHandle(process.Process); CloseHandle(process.Thread); }
            if (attributesInitialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != 0) Marshal.FreeHGlobal(attributes);
            foreach (var value in allocated) Marshal.FreeHGlobal(value);
            if (sid != 0) FreeSid(sid);
        }
    }
    public override void Kill() { try { TerminateJobObject(Job, 2); } catch { } }
    public override void Dispose()
    {
        Kill(); try { Process.WaitForExit(2000); } catch { }
        Input.Dispose(); Output.Dispose(); Error.Dispose(); Process.Dispose(); CloseHandle(Job);
        RemoveRule(DirectoryPath, RuntimeRule); DeleteAppContainerProfile(Profile);
    }
    private static void RemoveRule(string directory, FileSystemAccessRule rule)
    {
        var folder = new DirectoryInfo(directory); var acl = folder.GetAccessControl(); acl.RemoveAccessRuleSpecific(rule); folder.SetAccessControl(acl);
    }
    private static nint Allocate<T>(T value, List<nint> allocated) where T : struct
    { nint result = Marshal.AllocHGlobal(Marshal.SizeOf<T>()); allocated.Add(result); Marshal.StructureToPtr(value, result, false); return result; }
    private static void Check(bool value, string stage = "attribute-update") { if (!value) { var error = new Win32Exception(Marshal.GetLastWin32Error()); error.Data["stage"] = stage; throw error; } }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityCapabilities { public nint AppContainerSid, Capabilities; public uint Count, Reserved; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
    { public int Size; public string? Reserved, Desktop, Title; public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags; public ushort Show, Reserved2; public nint ReservedPointer, StdInput, StdOutput, StdError; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo StartupInfo; public nint AttributeList; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public nint Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public nuint MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcessLimit; public nuint Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemory, PeakJobMemory; }
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern int CreateAppContainerProfile(string name, string display, string description, nint capabilities, uint count, out nint sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern int DeleteAppContainerProfile(string name);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern int GetAppContainerFolderPath(string sid, out nint path);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertSidToStringSid(nint sid, out nint text);
    [DllImport("advapi32.dll")] private static extern nint FreeSid(nint sid);
    [DllImport("kernel32.dll")] private static extern nint LocalFree(nint value);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(nint attributes, int count, int flags, ref nint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(nint attributes, uint flags, nuint attribute, nint value, nuint size, nint previous, nint returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(nint attributes);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcess(string application, System.Text.StringBuilder command, nint processSecurity, nint threadSecurity, bool inherit, uint flags, nint environment, string directory, ref StartupInfoEx startup, out ProcessInformation process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateJobObject(nint security, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(nint job, int information, ref ExtendedLimits limits, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(nint process, uint exit);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateJobObject(nint job, uint exit);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
