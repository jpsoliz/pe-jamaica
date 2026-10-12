using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;
using Microsoft.Win32.SafeHandles;

namespace ParcelWorkflowAddIn.Tests.Workflow;

internal static class ToolboxContainedFileSystemTests
{
    [ThreadStatic] private static SafeFileHandle? callerFixtureTransaction;
    public static void RunAll()
    {
        PurePathAndAttestationContract();
        RunActualNtfs();
    }

    public static void PurePathAndAttestationContract()
    {
        string root = TestRoot();
        Code("unsafe_path", () => new ToolboxContainedFileSystem(root, false));
        foreach (string invalid in new[] { "", "relative", "\\\\server\\share", "\\\\?\\C:\\test", "C:/test", "C:\\a\\..\\b" })
            Code("unsafe_path", () => new ToolboxContainedFileSystem(invalid, true));
        Code("unsafe_path", () => new ToolboxContainedFileSystem(root, true, new[] { root }));
        Code("unsafe_path", () => new ToolboxContainedFileSystem(root, true, protectedRoots: new[] { root }));
        var fs = new ToolboxContainedFileSystem(root, true);
        foreach (string invalid in new[] { "", "../case", "/case", "case/", "a//b", "case:stream", "NUL.json", "COM1", "file.", "file ", "a/*", "a/..", "a/CON.txt" })
            Code("unsafe_path", () => fs.OpenCase(invalid));
        Code("limits_exceeded", () => fs.OpenCase(string.Join("/", Enumerable.Repeat("a", 13))));
    }

    public static void RunActualNtfs()
    {
        ImmutableWritesReadsAndStreamingVerification();
        ExclusiveClaimsAndLockContention();
        SealAndGuardedReplacement();
        HardLinksReparseAndPreopenedHandles();
        DeterministicPinnedBoundaryRaces();
        ReplacementFailureRetainsEvidence();
        ProcessContentionAndTermination();
        ReplacementCapabilityProbeDoesNotMutate();
        ExternalAmbientTransactionIsRejectedWithoutMutation();
    }

    public static void ImmutableWritesReadsAndStreamingVerification()
    {
        using var session = NewCase(out _);
        using var writer = session.AcquireWriter(createNew: true);
        byte[] bytes = Enumerable.Range(0, 180001).Select(i => (byte)(i % 251)).ToArray();
        session.WriteNew("toolbox/prototype-store/bundle.bin", bytes);
        session.Verify("toolbox/prototype-store/bundle.bin", bytes.Length, Digest(bytes));
        TestAssert.True(bytes.SequenceEqual(session.Read("toolbox/prototype-store/bundle.bin", bytes.Length)), "Owned bytes must round trip.");
        Code("limits_exceeded", () => session.Read("toolbox/prototype-store/bundle.bin", bytes.Length - 1));
        Code("corrupt", () => session.Verify("toolbox/prototype-store/bundle.bin", bytes.Length + 1, Digest(bytes)));
        Code("corrupt", () => session.Verify("toolbox/prototype-store/bundle.bin", bytes.Length, new string('0', 64)));
        Code("initialization_conflict", () => session.WriteNew("toolbox/prototype-store/bundle.bin", new byte[] { 99 }));
        TestAssert.True(bytes.SequenceEqual(session.Read("toolbox/prototype-store/bundle.bin", bytes.Length)), "Collision must preserve existing bytes.");
        session.WriteNew("toolbox/prototype-store/empty.bin", Array.Empty<byte>());
        session.Verify("toolbox/prototype-store/empty.bin", 0, Digest(Array.Empty<byte>()));
        var entries = session.Enumerate().ToArray();
        TestAssert.True(entries.Any(e => e.RelativePath == "toolbox/prototype-store/writer.lock"), "Exclusive lock must remain enumerable through its owned handle.");
        TestAssert.Equal((long)bytes.Length, entries.Single(e => e.RelativePath.EndsWith("bundle.bin", StringComparison.Ordinal)).Length, "Enumeration reports opened length.");
        TestAssert.Equal(entries.Length, session.Enumerate().Count(), "A fresh native enumeration restarts the pinned case cursor.");
        TestAssert.True(bytes.SequenceEqual(session.Read("toolbox/prototype-store/bundle.bin", bytes.Length)), "Enumeration must not dispose the session-owned case handle.");
    }

    public static void ExclusiveClaimsAndLockContention()
    {
        using var first = NewCase(out string root);
        var fs = new ToolboxContainedFileSystem(root, true);
        Code("initialization_conflict", () => fs.OpenCase("case", createNew: true));
        using var second = fs.OpenCase("case");
        Code("missing", () => first.AcquireWriter());
        TestAssert.False(first.Exists("toolbox/prototype-store/writer.lock"), "Open must not repair a missing lock.");
        using (first.AcquireWriter(createNew: true))
        {
            Code("busy", () => second.AcquireWriter());
            Code("busy", () => first.AcquireWriter());
            IoRejected(() => Directory.Move(Path.Combine(root, "case", "toolbox", "prototype-store"), Path.Combine(root, "case", "toolbox", "moved")));
        }
        using (second.AcquireWriter()) { }
        using (first.AcquireWriter()) { }
    }

    public static void SealAndGuardedReplacement()
    {
        using var session = NewCase(out _);
        session.CreateDirectory("staging");
        session.CreateDirectory("revisions");
        session.CreateDirectory("staging/prepared");
        session.WriteNew("staging/prepared/bundle.bin", Encoding.UTF8.GetBytes("sealed"));
        session.SealDirectory("staging/prepared", "revisions/one");
        TestAssert.False(session.Exists("staging/prepared"), "Seal removes the staging name.");
        TestAssert.Equal("sealed", Encoding.UTF8.GetString(session.Read("revisions/one/bundle.bin", 32)), "Seal retains content.");
        session.CreateDirectory("staging/collision");
        Code("initialization_conflict", () => session.SealDirectory("staging/collision", "revisions/one"));
        TestAssert.True(session.Exists("staging/collision"), "Failed seal retains evidence.");
        session.WriteNew("toolbox/prototype-store/current.next.json", Encoding.UTF8.GetBytes("initial"));
        session.Replace("toolbox/prototype-store/current.next.json", "toolbox/prototype-store/current.json");
        session.WriteNew("toolbox/prototype-store/current.next.json", Encoding.UTF8.GetBytes("successor"));
        session.Replace("toolbox/prototype-store/current.next.json", "toolbox/prototype-store/current.json");
        TestAssert.Equal("successor", Encoding.UTF8.GetString(session.Read("toolbox/prototype-store/current.json", 32)), "Guarded replacement must actually succeed on NTFS.");
        TestAssert.False(session.Exists("toolbox/prototype-store/current.next.json"), "Successful replacement consumes only the sibling name.");
    }

    public static void HardLinksReparseAndPreopenedHandles()
    {
        using var session = NewCase(out string root);
        string caseRoot = Path.Combine(root, "case");
        string sentinel = Path.Combine(root, "outside.bin");
        byte[] original = Encoding.UTF8.GetBytes("outside-sentinel");
        File.WriteAllBytes(sentinel, original);
        string alias = Path.Combine(caseRoot, "alias.bin");
        if (!CreateHardLinkW(alias, sentinel, IntPtr.Zero))
            throw new InvalidOperationException("UNVERIFIED hard-link fixture: Windows error " + Marshal.GetLastWin32Error());
        Code("unsafe_path", () => session.Read("alias.bin", 128));
        Code("unsafe_path", () => session.Verify("alias.bin", original.Length, Digest(original)));
        session.WriteNew("replacement.bin", new byte[] { 7 });
        Code("unsafe_path", () => session.Replace("replacement.bin", "alias.bin"));
        TestAssert.True(original.SequenceEqual(File.ReadAllBytes(sentinel)), "Hard-link rejection preserves sentinel bytes.");
        session.WriteNew("ordinary.bin", original);
        using (var preopened = new FileStream(Path.Combine(caseRoot, "ordinary.bin"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            Code("busy", () => session.Read("ordinary.bin", 128));
        using (var preopened = new FileStream(Path.Combine(caseRoot, "ordinary.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            Code("busy", () => session.Replace("replacement.bin", "ordinary.bin"));
        string outside = Path.Combine(root, "outside-directory");
        Directory.CreateDirectory(outside);
        string junction = Path.Combine(caseRoot, "junction");
        using var process = Process.Start(new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "/d", "/c", "mklink", "/J", junction, outside }
        }) ?? throw new InvalidOperationException("Could not start junction fixture.");
        if (!process.WaitForExit(10000)) { process.Kill(true); process.WaitForExit(); throw new InvalidOperationException("Junction fixture timed out."); }
        if (process.ExitCode != 0) throw new InvalidOperationException("UNVERIFIED junction fixture: " + process.StandardError.ReadToEnd());
        Code("unsafe_path", () => session.WriteNew("junction/escape.bin", new byte[] { 1 }));
        TestAssert.False(File.Exists(Path.Combine(outside, "escape.bin")), "Reparse parent must never receive writes.");
        Code("unsafe_path", () => new ToolboxContainedFileSystem(junction, true).OpenCase("new-case", true));
    }

    public static void DeterministicPinnedBoundaryRaces()
    {
        using var session = NewCase(out string root);
        string caseRoot = Path.Combine(root, "case");
        session.WriteNew("target.bin", new byte[] { 1 });
        session.WriteNew("sibling.bin", new byte[] { 2 });
        string sentinel = Path.Combine(root, "outside-sentinel.bin");
        byte[] sentinelBytes = Encoding.UTF8.GetBytes("replacement-outside-sentinel");
        File.WriteAllBytes(sentinel, sentinelBytes);
        string alias = Path.Combine(caseRoot, "attacker-alias.bin");
        if (!CreateHardLinkW(alias, sentinel, IntPtr.Zero))
            throw new InvalidOperationException("UNVERIFIED replacement hard-link race fixture: Windows error " + Marshal.GetLastWin32Error());
        string reparseProbe = Path.Combine(root, "reparse-capability.bin");
        File.WriteAllBytes(reparseProbe, new byte[] { 0 });
        if (!TrySetFixtureReparsePoint(reparseProbe))
            throw new InvalidOperationException("UNVERIFIED reparse conversion fixture: Windows error " + Marshal.GetLastWin32Error());
        int reached = 0;
        WithBoundary(boundary =>
        {
            if (boundary is not ("replace_opened" or "replace_target_closed" or "replace_before_commit")) return;
            reached++;
            IoRejected(() => File.Move(Path.Combine(caseRoot, "target.bin"), Path.Combine(caseRoot, "target-moved.bin")));
            IoRejected(() => File.WriteAllBytes(Path.Combine(caseRoot, "target.bin"), new byte[] { 8 }));
            IoRejected(() => File.Move(Path.Combine(caseRoot, "sibling.bin"), Path.Combine(caseRoot, "sibling-moved.bin")));
            IoRejected(() => File.Move(alias, Path.Combine(caseRoot, "target.bin"), overwrite: true));
            TestAssert.False(CreateHardLinkW(Path.Combine(root, "new-target-link.bin"), Path.Combine(caseRoot, "target.bin"), IntPtr.Zero), "Transaction must reject creation of a target hard-link alias.");
            TestAssert.False(TrySetFixtureReparsePoint(Path.Combine(caseRoot, "target.bin")), "Transaction must reject target reparse conversion.");
            IoRejected(() => Directory.Move(caseRoot, Path.Combine(root, "case-moved")));
        }, () => session.Replace("sibling.bin", "target.bin"));
        TestAssert.Equal(3, reached, "Transaction protection must cover open handles, closed target, and closed-handle precommit boundaries.");
        TestAssert.Equal((byte)2, session.Read("target.bin", 1)[0], "Only the rooted replacement may publish bytes.");
        TestAssert.True(sentinelBytes.SequenceEqual(File.ReadAllBytes(sentinel)), "Replacement races must preserve outside sentinel bytes.");
        TestAssert.True(File.Exists(alias), "Rejected substitution must retain the attacker's source alias.");
        TestAssert.False(File.Exists(Path.Combine(root, "new-target-link.bin")), "Rejected link creation must leave no outside alias.");
        WithBoundary(boundary =>
        {
            if (boundary == "read_opened")
                IoRejected(() => File.WriteAllBytes(Path.Combine(caseRoot, "target.bin"), new byte[] { 9 }));
        }, () => session.Read("target.bin", 1));
        WithBoundary(boundary =>
        {
            if (boundary == "write_new_opened")
                IoRejected(() => File.Move(Path.Combine(caseRoot, "new.bin"), Path.Combine(caseRoot, "new-moved.bin")));
        }, () => session.WriteNew("new.bin", new byte[] { 3 }));
    }

    public static void ReplacementFailureRetainsEvidence()
    {
        using var session = NewCase(out string root);
        session.WriteNew("target.bin", new byte[] { 1 });
        session.WriteNew("sibling.bin", new byte[] { 2 });
        File.SetAttributes(Path.Combine(root, "case", "target.bin"), FileAttributes.ReadOnly);
        try
        {
            session.Replace("sibling.bin", "target.bin");
            throw new InvalidOperationException("Replacing a read-only target unexpectedly succeeded.");
        }
        catch (ToolboxFileSystemException)
        {
            TestAssert.Equal((byte)1, session.Read("target.bin", 1)[0], "Observe actual old target after native failure.");
            TestAssert.Equal((byte)2, session.Read("sibling.bin", 1)[0], "Observe retained sibling after native failure.");
        }
    }

    public static void ReplacementCapabilityProbeDoesNotMutate()
    {
        using var session = NewCase(out string root);
        using var lease = session.AcquireWriter(createNew: true);
        const string owner = "toolbox/prototype-store/owner.json";
        session.WriteNew(owner, Encoding.UTF8.GetBytes("capability-owner-fixture"));
        session.WriteNew("toolbox/prototype-store/retained.bin", new byte[] { 1, 2, 3 });
        var before = session.Enumerate().OrderBy(e => e.RelativePath, StringComparer.Ordinal).ToArray();
        var bytes = before.Where(e => !e.IsDirectory && !e.RelativePath.EndsWith("writer.lock", StringComparison.Ordinal))
            .ToDictionary(e => e.RelativePath, e => session.Read(e.RelativePath, 4096), StringComparer.Ordinal);
        session.ValidateReplacementCapability(owner);
        var after = session.Enumerate().OrderBy(e => e.RelativePath, StringComparer.Ordinal).ToArray();
        TestAssert.True(before.SequenceEqual(after), "Capability probe must preserve exact paths, types and lengths.");
        foreach (var entry in bytes)
            TestAssert.True(entry.Value.SequenceEqual(session.Read(entry.Key, 4096)), "Capability probe must preserve every store byte.");
        TestAssert.Equal(0L, new FileInfo(Path.Combine(root, "case", "toolbox", "prototype-store", "writer.lock")).Length, "Capability probe must preserve lock bytes.");
        using (var competitor = new FileStream(Path.Combine(root, "case", "toolbox", "prototype-store", "owner.json"),
            FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            Code("busy", () => session.ValidateReplacementCapability(owner));
        session.ValidateReplacementCapability(owner);
    }

    public static void ExternalAmbientTransactionIsRejectedWithoutMutation()
    {
        using var session = NewCase(out string root);
        using var lease = session.AcquireWriter(createNew: true);
        session.WriteNew("baseline.bin", new byte[] { 1, 2, 3 });
        var before = session.Enumerate().OrderBy(e => e.RelativePath, StringComparer.Ordinal).ToArray();
        var bytes = before.Where(e => !e.IsDirectory && !e.RelativePath.EndsWith("writer.lock", StringComparison.Ordinal))
            .ToDictionary(e => e.RelativePath, e => session.Read(e.RelativePath, 4096), StringComparer.Ordinal);
        ExceptionDispatchInfo? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                TestAssert.Equal(IntPtr.Zero, RtlGetCurrentTransaction(), "Fixture thread must start without a native transaction.");
                var external = CreateTransaction(IntPtr.Zero, IntPtr.Zero, 1, 0, 0, 10000, "Toolbox external ambient refusal fixture");
                if (external.IsInvalid) { external.Dispose(); throw new InvalidOperationException("UNVERIFIED external KTM fixture."); }
                callerFixtureTransaction = external;
                TestAssert.True(ReferenceEquals(callerFixtureTransaction, external), "Fixture retains ownership of its native transaction.");
                try
                {
                    TestAssert.True(RtlSetCurrentTransaction(external.DangerousGetHandle()), "Install caller-owned fixture transaction.");
                    var fs = new ToolboxContainedFileSystem(root, true);
                    Code("unsupported", () => fs.OpenCase("ambient-case", createNew: true));
                    Code("unsupported", () => session.WriteNew("ambient-created.bin", new byte[] { 9 }));
                    Code("unsupported", () => session.Enumerate().ToArray());
                    Code("unsupported", () => session.ValidateReplacementCapability("baseline.bin"));
                    TestAssert.Equal(external.DangerousGetHandle(), RtlGetCurrentTransaction(), "Refusal must preserve the caller-owned transaction context.");
                }
                finally
                {
                    RtlSetCurrentTransaction(IntPtr.Zero);
                    if (RtlGetCurrentTransaction() != IntPtr.Zero)
                        throw new InvalidOperationException("Fixture could not detach its context; transaction remains owned until this dedicated thread exits.");
                    RollbackTransaction(external);
                    external.Dispose();
                    callerFixtureTransaction = null;
                }
            }
            catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
        }) { IsBackground = true };
        worker.Start();
        if (!worker.Join(10000)) throw new InvalidOperationException("External ambient refusal fixture exceeded its deadline.");
        failure?.Throw();
        TestAssert.False(Directory.Exists(Path.Combine(root, "ambient-case")), "Ambient case creation must leave no directory.");
        TestAssert.True(before.SequenceEqual(session.Enumerate().OrderBy(e => e.RelativePath, StringComparer.Ordinal)), "Ambient refusal must preserve exact paths, types and lengths.");
        foreach (var entry in bytes)
            TestAssert.True(entry.Value.SequenceEqual(session.Read(entry.Key, 4096)), "Ambient refusal must preserve every file byte.");
    }

    // Parent Program.cs dispatches --toolbox-contained-worker before its normal registry.
    public static int RunWorker(string[] args)
    {
        if (args.Length != 3 || args[1] != "case" || args[2] is not ("hold" or "contend")) return 64;
        string root = Path.GetFullPath(args[0]);
        string allowed = TestRoot().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(root[allowed.Length..], "N", out _)) return 64;
        try
        {
            using var session = new ToolboxContainedFileSystem(root, true).OpenCase("case");
            using var lease = session.AcquireWriter();
            if (args[2] == "contend") return 1;
            Console.WriteLine("TOOLBOX_LOCK_READY");
            Console.Out.Flush();
            return Console.In.ReadLineAsync().Wait(30000) ? 0 : 2;
        }
        catch (ToolboxFileSystemException e) when (args[2] == "contend" && e.Code == "busy") { return 0; }
    }

    public static void ProcessContentionAndTermination()
    {
        using var session = NewCase(out string root);
        using (session.AcquireWriter(true)) { }
        using var holder = StartWorker(root, "hold");
        try
        {
            Task<string?> ready = holder.StandardOutput.ReadLineAsync();
            if (!ready.Wait(10000) || ready.Result != "TOOLBOX_LOCK_READY")
                throw new InvalidOperationException("Worker dispatch absent or lock worker failed; Program.cs must dispatch the reserved argument.");
            Code("busy", () => session.AcquireWriter());
            using var contender = StartWorker(root, "contend");
            try
            {
                if (!contender.WaitForExit(10000)) throw new InvalidOperationException("Contender timed out.");
                TestAssert.Equal(0, contender.ExitCode, "Independent process must receive busy.");
            }
            finally { if (!contender.HasExited) { contender.Kill(true); contender.WaitForExit(); } }
            holder.Kill(true);
            TestAssert.True(holder.WaitForExit(10000), "Terminated worker must exit.");
            using (session.AcquireWriter()) { }
            TestAssert.Equal(0L, new FileInfo(Path.Combine(root, "case", "toolbox", "prototype-store", "writer.lock")).Length, "Process locking never truncates the existing lock.");
        }
        finally { if (!holder.HasExited) { holder.Kill(true); holder.WaitForExit(); } }
    }

    private static Process StartWorker(string root, string mode)
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("No process executable.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        start.ArgumentList.Add("--toolbox-contained-worker");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add("case");
        start.ArgumentList.Add(mode);
        return Process.Start(start) ?? throw new InvalidOperationException("Unable to start lock worker.");
    }

    private static ToolboxFileSession NewCase(out string root)
    {
        root = Path.Combine(TestRoot(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ToolboxFileSession session;
        try { session = new ToolboxContainedFileSystem(root, true).OpenCase("case", true); }
        catch (ToolboxFileSystemException e) when (e.Code == "unsupported")
        { throw new InvalidOperationException("UNVERIFIED actual NTFS fixtures: " + e.Message, e); }
        session.CreateDirectory("toolbox");
        session.CreateDirectory("toolbox/prototype-store");
        return session;
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string TestRoot() => Path.GetFullPath(Environment.GetEnvironmentVariable("TOOLBOX_DISK_TEST_ROOT")
        ?? Path.Combine(Environment.CurrentDirectory, ".artifacts", "toolbox-disk-tests"));
    private static void Code(string code, Action action)
    {
        try { action(); }
        catch (ToolboxFileSystemException e) { TestAssert.Equal(code, e.Code, "Typed adapter diagnostic."); return; }
        throw new InvalidOperationException("Expected adapter diagnostic " + code + ".");
    }
    private static void IoRejected(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Competing ordinary mutation unexpectedly succeeded.");
    }
    private static void WithBoundary(Action<string> hook, Action action)
    {
        FieldInfo field = typeof(ToolboxContainedFileSystem).Assembly.GetType("ParcelWorkflowAddIn.Workflow.Toolbox.Persistence.Native")!
            .GetField("TestBoundary", BindingFlags.NonPublic | BindingFlags.Static)!;
        var workers = new List<Thread>();
        field.SetValue(null, new Action<string>(boundary =>
        {
            ExceptionDispatchInfo? failure = null;
            var competitor = new Thread(() =>
            {
                try
                {
                    TestAssert.Equal(IntPtr.Zero, RtlGetCurrentTransaction(), "Independent competitor must have no native transaction.");
                    hook(boundary);
                }
                catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
            }) { IsBackground = true };
            workers.Add(competitor);
            competitor.Start();
            if (!competitor.Join(10000)) throw new InvalidOperationException("Independent competing I/O did not fail within the fixture deadline.");
            failure?.Throw();
        }));
        IntPtr before = RtlGetCurrentTransaction();
        try { action(); }
        finally
        {
            field.SetValue(null, null);
            foreach (Thread worker in workers)
                if (worker.IsAlive && !worker.Join(10000)) throw new InvalidOperationException("Fixture competitor remained active after transaction release.");
            TestAssert.Equal(before, RtlGetCurrentTransaction(), "Adapter must restore the caller's native transaction context.");
        }
    }

    private static bool TrySetFixtureReparsePoint(string path)
    {
        using SafeFileHandle handle = CreateFileW(path, 0x40000000, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid) return false;
        // Non-Microsoft GUID reparse data: no name-surrogate or redirect to outside content.
        byte[] buffer = new byte[28];
        BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), 0x00001234u);
        BitConverter.TryWriteBytes(buffer.AsSpan(4, 2), (ushort)4);
        new Guid("6d2be553-0d17-4f26-9fd7-a13b8c3d19a0").TryWriteBytes(buffer.AsSpan(8, 16));
        buffer[24] = 1;
        return DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr security);
    [DllImport("ntdll.dll")] private static extern IntPtr RtlGetCurrentTransaction();
    [DllImport("ntdll.dll")]
    [return: MarshalAs(UnmanagedType.U1)] private static extern bool RtlSetCurrentTransaction(IntPtr transaction);
    [DllImport("KtmW32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateTransaction(
        IntPtr security, IntPtr unitOfWork, uint options, uint isolationLevel, uint isolationFlags, uint timeout, string description);
    [DllImport("KtmW32.dll", SetLastError = true)] private static extern bool RollbackTransaction(SafeFileHandle transaction);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(
        string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle handle,
        uint code, byte[] input, int inputLength, IntPtr output, int outputLength, out int returned, IntPtr overlapped);
}
