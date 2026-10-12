using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ParcelWorkflowAddIn.Workflow.Toolbox.Persistence;

public sealed class ToolboxFileSystemException : IOException
{
    public string Code { get; }
    public ToolboxFileSystemException(string code, string message) : base(message) => Code = code;
}

public sealed record ToolboxFileEntry(string RelativePath, bool IsDirectory, long Length);

/// <summary>Windows-only, caller-approved ordinary-file storage. No schema or recovery decisions.</summary>
public sealed class ToolboxContainedFileSystem
{
    private readonly string root;
    private readonly string[] excludedRoots;

    public ToolboxContainedFileSystem(string approvedRoot, bool unmanagedRootAttested,
        IEnumerable<string>? synchronizationRoots = null, IEnumerable<string>? protectedRoots = null)
    {
        if (!unmanagedRootAttested) throw Native.Error("unsafe_path", "An unmanaged root attestation is required.");
        root = ValidateRoot(approvedRoot);
        var excluded = new List<string>();
        foreach (string sync in (synchronizationRoots ?? Array.Empty<string>()).Concat(protectedRoots ?? Array.Empty<string>()))
        {
            string other = ValidateRoot(sync);
            if (Within(root, other) || Within(other, root))
                throw Native.Error("unsafe_path", "The approved root overlaps an excluded root.");
            excluded.Add(other);
        }
        foreach (string variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(value))
            {
                string sync = ValidateRoot(value);
                if (Within(root, sync) || Within(sync, root))
                    throw Native.Error("unsafe_path", "The approved root is synchronization managed.");
                excluded.Add(sync);
            }
        }
        excludedRoots = excluded.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public ToolboxFileSession OpenCase(string relativeCasePath, bool createNew = false)
    {
        string[] parts = Native.Components(relativeCasePath);
        var session = new ToolboxFileSession(root, Array.Empty<string>(), false);
        try
        {
            foreach (string excluded in excludedRoots)
            {
                var other = new ToolboxFileSession(excluded, Array.Empty<string>(), false);
                try { session.RetainExcludedRoot(other); }
                catch { other.Dispose(); throw; }
            }
            session.ClaimCase(parts, createNew);
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    private static bool Within(string path, string parent) => path.Equals(parent, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    private static string ValidateRoot(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 3 || !char.IsAsciiLetter(path[0])
            || path[1] != ':' || path[2] != '\\' || path.Contains('/'))
            throw Native.Error("unsafe_path", "Use an absolute local DOS drive path.");
        string tail = path[3..].TrimEnd('\\');
        foreach (string part in tail.Split('\\', StringSplitOptions.RemoveEmptyEntries))
            if (part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                || part.Any(c => char.IsControl(c) || "<>:\"|?*".Contains(c)))
                throw Native.Error("unsafe_path", "Invalid absolute root component.");
        if (tail.Contains("\\\\")) throw Native.Error("unsafe_path", "Empty root component.");
        return char.ToUpperInvariant(path[0]) + path[1..3] + tail;
    }
}

/// <summary>Owns the drive-to-case pin chain. Dispose only after all operations and locks finish.</summary>
public sealed class ToolboxFileSession : IDisposable
{
    private readonly List<SafeFileHandle> pins = new();
    private readonly List<ToolboxFileSession> excludedPins = new();
    private readonly object gate = new();
    private readonly uint volume;
    private SafeFileHandle Case => pins[^1];
    private bool disposed;
    private SafeFileHandle? writer;
    private ParentPin? writerParent;
    private int rootPinCount;

    internal ToolboxFileSession(string root, string[] caseParts, bool createNew)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw Native.Error("unsupported", "Windows x64 fixed NTFS is required.");
        Native.RequireOrdinaryContext();
        try
        {
            string drive = root[..3];
            var device = new StringBuilder(1024);
            if (Native.GetDriveTypeW(drive) != 3 || Native.QueryDosDeviceW(root[..2], device, device.Capacity) == 0
                || !device.ToString().StartsWith("\\Device\\HarddiskVolume", StringComparison.OrdinalIgnoreCase))
                throw Native.Error("unsupported", "Mapped, remote and non-fixed drives are unsupported.");
            SafeFileHandle first = Native.CreateFileW(drive, Native.DirectoryAccess, 3, IntPtr.Zero, 3,
                0x02200000, IntPtr.Zero);
            if (first.IsInvalid) { first.Dispose(); Native.Win32("Opening drive root"); }
            pins.Add(first);
            volume = Native.Verify(first, true, null).VolumeSerialNumber;
            var fs = new StringBuilder(64);
            if (!Native.GetVolumeInformationByHandleW(first, null, 0, out _, out _, out _, fs, fs.Capacity))
                Native.Win32("Inspecting actual volume filesystem");
            if (!fs.ToString().Equals("NTFS", StringComparison.OrdinalIgnoreCase))
                throw Native.Error("unsupported", "The actual volume must be NTFS (observed " + fs + ").");
            var final = new StringBuilder(32768);
            if (Native.GetFinalPathNameByHandleW(first, final, final.Capacity, 0) == 0
                || !final.ToString().Equals("\\\\?\\" + drive, StringComparison.OrdinalIgnoreCase))
                throw Native.Error("unsafe_path", "Drive identity does not match its physical root.");
            foreach (string part in root[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
                pins.Add(Native.Open(pins[^1], part, true, false, volume));
            final.Clear();
            if (Native.GetFinalPathNameByHandleW(pins[^1], final, final.Capacity, 0) == 0
                || !final.ToString().TrimEnd('\\').Equals(("\\\\?\\" + root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw Native.Error("unsafe_path", "Approved root does not match its actual opened identity path.");
            rootPinCount = pins.Count;
            for (int i = 0; i < caseParts.Length; i++)
                pins.Add(Native.Open(pins[^1], caseParts[i], true, createNew && i == caseParts.Length - 1, volume));
        }
        catch { Dispose(); throw; }
    }

    internal void RetainExcludedRoot(ToolboxFileSession excluded)
    {
        var ours = Native.Verify(pins[rootPinCount - 1], true, volume);
        var theirs = Native.Verify(excluded.pins[excluded.rootPinCount - 1], true, excluded.volume);
        bool overlap = excluded.pins.Take(excluded.rootPinCount).Any(p => Native.IdentityEquals(ours, Native.Verify(p, true, excluded.volume)))
            || pins.Take(rootPinCount).Any(p => Native.IdentityEquals(theirs, Native.Verify(p, true, volume)));
        if (overlap) throw Native.Error("unsafe_path", "Opened root identity overlaps an excluded root or ancestor.");
        excludedPins.Add(excluded);
    }

    internal void ClaimCase(string[] parts, bool createNew)
    {
        for (int i = 0; i < parts.Length; i++)
            pins.Add(Native.Open(pins[^1], parts[i], true, createNew && i == parts.Length - 1, volume));
    }

    public IDisposable AcquireWriter(bool createNew = false)
    {
        lock (gate)
        {
            Check();
            if (writer != null) throw Native.Error("busy", "This session already holds the writer lock.");
            var parent = Parent("toolbox/prototype-store/writer.lock");
            try
            {
                writer = Native.Open(parent.Handle, parent.Leaf, false, createNew, volume, exclusive: true);
                if (createNew)
                {
                    Native.TestBoundary?.Invoke("before_native_lock_flush");
                    if (!Native.FlushFileBuffers(writer)) Native.Win32("Flushing new writer lock");
                    Native.TestBoundary?.Invoke("after_native_lock_flush");
                }
                writerParent = parent;
            }
            catch { writer?.Dispose(); writer = null; parent.Dispose(); throw; }
            return new WriterLease(this, writer);
        }
    }

    public bool Exists(string relative)
    {
        lock (gate)
        {
            Check();
            try
            {
                using var parent = Parent(relative);
                using var handle = Native.Open(parent.Handle, parent.Leaf, null, false, volume);
                return true;
            }
            catch (ToolboxFileSystemException e) when (e.Code == "missing") { return false; }
        }
    }

    public byte[] Read(string relative, int maxBytes)
    {
        if (maxBytes < 0) throw Native.Error("limits_exceeded", "A nonnegative read bound is required.");
        lock (gate)
        {
            Check();
            using var parent = Parent(relative);
            using var file = Native.Open(parent.Handle, parent.Leaf, false, false, volume);
            Native.TestBoundary?.Invoke("read_opened");
            long length = Native.Length(Native.Verify(file, false, volume));
            if (length > maxBytes) throw Native.Error("limits_exceeded", "File exceeds the read bound.");
            byte[] bytes = new byte[checked((int)length)];
            Native.ReadExactly(file, bytes);
            if (Native.Length(Native.Verify(file, false, volume)) != length)
                throw Native.Error("corrupt", "File changed during read.");
            return bytes;
        }
    }

    public void WriteNew(string relative, ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            Check();
            using var parent = Parent(relative);
            using var file = Native.Open(parent.Handle, parent.Leaf, false, true, volume, write: true);
            Native.TestBoundary?.Invoke("write_new_opened");
            Native.TestBoundary?.Invoke("before_native_write:" + relative);
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = Math.Min(65536, bytes.Length - offset);
                byte[] block = bytes.Slice(offset, count).ToArray();
                if (!Native.WriteFile(file, block, count, out int written, IntPtr.Zero)) Native.Win32("Writing new file");
                if (written != count) throw Native.Error("io_error", "Short file write; retain the new leaf.");
                offset += count;
                Native.TestBoundary?.Invoke("after_native_write:" + relative + ":" + offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            Native.TestBoundary?.Invoke("before_native_flush:" + relative);
            if (!Native.FlushFileBuffers(file)) Native.Win32("Flushing new file");
            Native.TestBoundary?.Invoke("after_native_flush:" + relative);
            if (Native.Length(Native.Verify(file, false, volume)) != bytes.Length)
                throw Native.Error("corrupt", "New file length failed read-back.");
            Native.SeekStart(file);
            byte[] readback = new byte[Math.Min(65536, bytes.Length)];
            for (offset = 0; offset < bytes.Length; offset += readback.Length)
            {
                int count = Math.Min(readback.Length, bytes.Length - offset);
                Native.ReadExactly(file, readback, count);
                if (!bytes.Slice(offset, count).SequenceEqual(readback.AsSpan(0, count)))
                    throw Native.Error("corrupt", "New file bytes failed read-back.");
            }
            Native.TestBoundary?.Invoke("after_native_readback:" + relative);
        }
    }

    public void Verify(string relative, long expectedLength, string sha256)
    {
        if (expectedLength < 0 || sha256 == null || sha256.Length != 64
            || sha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Native.Error("corrupt", "Invalid expected byte length or SHA-256 digest.");
        lock (gate)
        {
            Check();
            using var parent = Parent(relative);
            using var file = Native.Open(parent.Handle, parent.Leaf, false, false, volume);
            Native.TestBoundary?.Invoke("verify_opened");
            var before = Native.Verify(file, false, volume);
            if (Native.Length(before) != expectedLength) throw Native.Error("corrupt", "File length does not match inventory.");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] block = new byte[65536];
            for (long left = expectedLength; left != 0;)
            {
                int count = (int)Math.Min(left, block.Length);
                Native.ReadExactly(file, block, count);
                hash.AppendData(block, 0, count);
                left -= count;
            }
            var after = Native.Verify(file, false, volume);
            Native.SameIdentity(before, after);
            if (Native.Length(after) != expectedLength || !Convert.ToHexString(hash.GetHashAndReset()).Equals(sha256, StringComparison.OrdinalIgnoreCase))
                throw Native.Error("corrupt", "File digest does not match inventory.");
        }
    }

    public void CreateDirectory(string relative)
    {
        lock (gate)
        {
            Check();
            using var parent = Parent(relative);
            using var directory = Native.Open(parent.Handle, parent.Leaf, true, true, volume);
        }
    }

    public void SealDirectory(string sourceRelative, string targetRelative)
    {
        lock (gate)
        {
            Check();
            using var source = Parent(sourceRelative);
            using var target = Parent(targetRelative);
            using var directory = Native.Open(source.Handle, source.Leaf, true, false, volume, rename: true);
            var before = Native.Verify(directory, true, volume);
            Native.TestBoundary?.Invoke("seal_opened");
            Native.Rename(directory, target.Handle, target.Leaf, false);
            Native.SameIdentity(before, Native.Verify(directory, true, volume));
        }
    }

    public void ValidateReplacementCapability(string existingRelative)
    {
        lock (gate)
        {
            Check();
            using var parent = Parent(existingRelative);
            using var transaction = new Native.ReplacementTransaction();
            using var file = Native.Open(parent.Handle, parent.Leaf, false, false, volume, write: true, transactional: true);
            Native.VerifyTransactedWriter(file);
            // Disposal closes the verified handle before rolling back the kernel-only probe.
        }
    }

    public void Replace(string siblingRelative, string targetRelative)
    {
        lock (gate)
        {
            Check();
            string[] sourceParts = Native.Components(siblingRelative);
            string[] targetParts = Native.Components(targetRelative);
            if (sourceParts.Length != targetParts.Length
                || !sourceParts[..^1].SequenceEqual(targetParts[..^1], StringComparer.OrdinalIgnoreCase)
                || sourceParts[^1].Equals(targetParts[^1], StringComparison.OrdinalIgnoreCase))
                throw Native.Error("unsafe_path", "Replacement requires distinct sibling names.");
            using var parent = Parent(siblingRelative);
            using var transaction = new Native.ReplacementTransaction();
            using (var file = Native.Open(parent.Handle, parent.Leaf, false, false, volume, rename: true, write: true, transactional: true))
            {
                Native.VerifyTransactedWriter(file);
                SafeFileHandle? target = null;
                try
                {
                    try { target = Native.Open(parent.Handle, targetParts[^1], false, false, volume, write: true, transactional: true); }
                    catch (ToolboxFileSystemException e) when (e.Code == "missing") { }
                    if (target != null) Native.VerifyTransactedWriter(target);
                    Native.TestBoundary?.Invoke("before_native_replacement_flush");
                    if (!Native.FlushFileBuffers(file)) Native.Win32("Flushing replacement sibling");
                    Native.TestBoundary?.Invoke("after_native_replacement_flush");
                    var before = Native.Verify(file, false, volume);
                    Native.TestBoundary?.Invoke("replace_opened");
                    bool replaceExisting = target != null;
                    if (replaceExisting)
                    {
                        // Standard rename requires a closed destination. Its verified TxF writer lock
                        // remains owned by the transaction, protecting this handoff through commit.
                        target!.Dispose();
                        target = null;
                        Native.TestBoundary?.Invoke("replace_target_closed");
                    }
                    Native.Rename(file, parent.Handle, targetParts[^1], replaceExisting);
                    Native.SameIdentity(before, Native.Verify(file, false, volume));
                }
                finally { target?.Dispose(); }
            }
            // TxF locks persist through this close-to-commit handoff, unlike ordinary sharing guards.
            Native.TestBoundary?.Invoke("replace_before_commit");
            transaction.Commit();
            Native.TestBoundary?.Invoke("replace_after_commit");
        }
    }

    public IEnumerable<ToolboxFileEntry> Enumerate()
    {
        // Each iterator frame retains only its directory handle and one 64 KiB native buffer.
        Check();
        long count = 0;
        foreach (var entry in Walk("", 0))
        {
            if (++count > 3_000_000) throw Native.Error("limits_exceeded", "Too many filesystem entries.");
            yield return entry;
        }
    }

    private IEnumerable<ToolboxFileEntry> Walk(string relative, int depth)
    {
        if (depth > 10) throw Native.Error("limits_exceeded", "Case directory depth exceeded.");
        using var parent = relative.Length == 0 ? new ParentPin(Case, "") : Parent(relative);
        using var ownedDirectory = relative.Length == 0
            ? null
            : Native.Open(parent.Handle, parent.Leaf, true, false, volume, list: true);
        SafeFileHandle directory = ownedDirectory ?? Case;
        foreach (string name in Native.Names(directory))
        {
            Check();
            Native.Components(name);
            string path = relative.Length == 0 ? name : relative + "/" + name;
            if (writer != null && path.Equals("toolbox/prototype-store/writer.lock", StringComparison.OrdinalIgnoreCase))
            {
                var lockInfo = Native.Verify(writer, false, volume);
                yield return new ToolboxFileEntry(path, false, Native.Length(lockInfo));
                continue;
            }
            using var child = Native.Open(directory, name, null, false, volume);
            var info = Native.Verify(child, null, volume);
            bool isDirectory = (info.FileAttributes & 0x10) != 0;
            yield return new ToolboxFileEntry(path, isDirectory, isDirectory ? 0 : Native.Length(info));
            child.Dispose();
            if (isDirectory)
                foreach (var nested in Walk(path, depth + 1)) yield return nested;
        }
    }

    private ParentPin Parent(string relative)
    {
        string[] parts = Native.Components(relative);
        var result = new ParentPin(Case, parts[^1]);
        try
        {
            foreach (string part in parts[..^1]) result.Add(Native.Open(result.Handle, part, true, false, volume));
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private void Check()
    {
        if (disposed) throw new ObjectDisposedException(nameof(ToolboxFileSession));
        Native.RequireOrdinaryContext();
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            writer?.Dispose();
            writer = null;
            writerParent?.Dispose();
            writerParent = null;
            for (int i = pins.Count - 1; i >= 0; i--) pins[i].Dispose();
            pins.Clear();
            for (int i = excludedPins.Count - 1; i >= 0; i--) excludedPins[i].Dispose();
            excludedPins.Clear();
        }
    }

    private sealed class WriterLease(ToolboxFileSession owner, SafeFileHandle handle) : IDisposable
    {
        public void Dispose()
        {
            lock (owner.gate)
            {
                handle.Dispose();
                if (ReferenceEquals(owner.writer, handle))
                {
                    owner.writer = null;
                    owner.writerParent?.Dispose();
                    owner.writerParent = null;
                }
            }
        }
    }

    private sealed class ParentPin(SafeFileHandle initial, string leaf) : IDisposable
    {
        private readonly List<SafeFileHandle> owned = new();
        public SafeFileHandle Handle => owned.Count == 0 ? initial : owned[^1];
        public string Leaf { get; } = leaf;
        public void Add(SafeFileHandle handle) => owned.Add(handle);
        public void Dispose() { for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose(); }
    }
}

internal static class Native
{
    // Internal deterministic fixture boundary; production callers never set this observer.
    internal static Action<string>? TestBoundary = null;
    [ThreadStatic] private static SafeFileHandle? strandedTransaction;
    [ThreadStatic] private static SafeFileHandle? activeTransaction;
    internal const uint DirectoryAccess = 0x001000A1; // SYNCHRONIZE | READ_ATTRIBUTES | TRAVERSE | LIST_DIRECTORY
    private const uint FileAccess = 0x00100081;
    internal static ToolboxFileSystemException Error(string code, string message) => new(code, message);

    internal static void RequireOrdinaryContext()
    {
        CheckTransactionThread();
        if (RtlGetCurrentTransaction() != IntPtr.Zero)
            throw Error("unsupported", "Caller-owned native transactions are unsupported for contained I/O.");
    }

    internal static void CheckTransactionThread()
    {
        SafeFileHandle? stranded = strandedTransaction;
        if (stranded == null) return;
        IntPtr installed = RtlGetCurrentTransaction();
        if (installed == stranded.DangerousGetHandle())
        {
            RtlSetCurrentTransaction(IntPtr.Zero);
            installed = RtlGetCurrentTransaction();
            if (installed == stranded.DangerousGetHandle())
                throw Error("unsupported", "Native transaction detachment failed; this thread cannot perform contained I/O.");
        }
        // Close only after observing that the owned transaction is no longer installed in this TEB.
        strandedTransaction = null;
        stranded.Dispose();
    }

    private static void RestoreTransactionAndClose(SafeFileHandle handle, IntPtr previous)
    {
        if (RtlSetCurrentTransaction(previous) && RtlGetCurrentTransaction() == previous
            && previous != handle.DangerousGetHandle())
        {
            handle.Dispose();
            return;
        }
        RtlSetCurrentTransaction(IntPtr.Zero);
        IntPtr installed = RtlGetCurrentTransaction();
        if (installed != handle.DangerousGetHandle())
        {
            handle.Dispose();
            throw Error("unsupported", installed == IntPtr.Zero
                ? "Previous native transaction context could not be restored; the thread was safely detached."
                : "Previous native transaction context could not be restored; the owned handle was confirmed detached.");
        }
        // The per-thread guard forbids allocating another transaction while this slot is occupied.
        SafeFileHandle? previouslyStranded = strandedTransaction;
        strandedTransaction = handle;
        if (previouslyStranded != null && !ReferenceEquals(previouslyStranded, handle))
            previouslyStranded.Dispose(); // The observed installed value is this handle, not the prior slot's handle.
        throw Error("unsupported", "Native transaction restoration and detachment failed; the handle remains owned and this thread is blocked.");
    }

    internal static string[] Components(string relative)
    {
        if (string.IsNullOrEmpty(relative) || relative.Length > 2048)
            throw Error("unsafe_path", "A bounded relative path is required.");
        string[] parts = relative.Replace('\\', '/').Split('/');
        if (parts.Length > 12) throw Error("limits_exceeded", "Too many path components.");
        foreach (string part in parts)
        {
            if (part.Length == 0 || part.Length > 255 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                || part.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))
                throw Error("unsafe_path", "Only strict ordinary relative names are admitted.");
            string stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
                || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9'))
                throw Error("unsafe_path", "Device names are not ordinary files.");
        }
        return parts;
    }

    internal static SafeFileHandle Open(SafeFileHandle parent, string leaf, bool? directory, bool create,
        uint volume, bool exclusive = false, bool write = false, bool rename = false, bool list = false,
        bool transactional = false)
    {
        CheckTransactionThread();
        if (transactional)
        {
            if (activeTransaction == null || RtlGetCurrentTransaction() != activeTransaction.DangerousGetHandle())
                throw Error("unsupported", "Transactional rooted opens require the adapter-owned transaction context.");
        }
        else RequireOrdinaryContext();
        IntPtr text = Marshal.StringToHGlobalUni(leaf);
        IntPtr unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        try
        {
            Marshal.StructureToPtr(new UnicodeString { Length = checked((ushort)(leaf.Length * 2)),
                MaximumLength = checked((ushort)(leaf.Length * 2 + 2)), Buffer = text }, unicode, false);
            var attributes = new ObjectAttributes { Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(), ObjectName = unicode, Attributes = 0x40 };
            uint access = directory == true ? DirectoryAccess : FileAccess;
            if (list) access |= 1;
            if (write || exclusive && create) access |= 2;
            if (rename) access |= 0x10000;
            uint options = 0x00200020u | (directory == true ? 1u : directory == false ? 0x40u : 0u);
            int status = NtCreateFile(out var handle, access, ref attributes, out _, IntPtr.Zero, 0,
                exclusive ? 0u : transactional ? 5u : directory == true ? 3u : 1u, create ? 2u : 1u, options, IntPtr.Zero, 0);
            GC.KeepAlive(parent);
            if (status < 0) { handle.Dispose(); Status(status, create ? "Claiming new leaf" : "Opening existing leaf"); }
            try
            {
                Verify(handle, directory, volume);
                if (!SetHandleInformation(handle, 1, 0)) Win32("Disabling handle inheritance");
                return handle;
            }
            catch { handle.Dispose(); throw; }
        }
        finally { Marshal.FreeHGlobal(unicode); Marshal.FreeHGlobal(text); }
    }

    internal static ByHandleInfo Verify(SafeFileHandle handle, bool? directory, uint? volume)
    {
        if (!GetFileInformationByHandle(handle, out var info)) Win32("Inspecting opened handle");
        if ((info.FileAttributes & (0x400u | 0x1000u | 0x40000u | 0x400000u)) != 0
            || (volume.HasValue && volume.Value != info.VolumeSerialNumber)
            || (directory.HasValue && ((info.FileAttributes & 0x10) != 0) != directory.Value)
            || ((info.FileAttributes & 0x10) == 0 && info.NumberOfLinks != 1))
            throw Error("unsafe_path", "Opened object is reparse, remote, aliased or has the wrong type/volume.");
        return info;
    }

    internal static long Length(ByHandleInfo info) => checked((long)(((ulong)info.FileSizeHigh << 32) | info.FileSizeLow));
    internal static void SameIdentity(ByHandleInfo before, ByHandleInfo after)
    {
        if (!IdentityEquals(before, after))
            throw Error("corrupt", "Opened identity changed during the operation.");
    }
    internal static bool IdentityEquals(ByHandleInfo first, ByHandleInfo second)
        => first.VolumeSerialNumber == second.VolumeSerialNumber && first.FileIndexHigh == second.FileIndexHigh
            && first.FileIndexLow == second.FileIndexLow;
    internal static void SeekStart(SafeFileHandle file)
    {
        if (!SetFilePointerEx(file, 0, out _, 0)) Win32("Seeking opened file");
    }
    internal static void ReadExactly(SafeFileHandle file, byte[] bytes, int? count = null)
    {
        int length = count ?? bytes.Length;
        if (length == 0) return;
        GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            int offset = 0;
            while (offset < length)
            {
                int requested = Math.Min(65536, length - offset);
                if (!ReadFile(file, pinned.AddrOfPinnedObject() + offset, requested, out int read, IntPtr.Zero)) Win32("Reading opened file");
                if (read == 0) throw Error("corrupt", "Opened file returned a short read.");
                offset += read;
            }
        }
        finally { pinned.Free(); }
    }

    internal static void Rename(SafeFileHandle source, SafeFileHandle parent, string leaf, bool replace)
    {
        byte[] name = Encoding.Unicode.GetBytes(leaf);
        // Native x64 FILE_RENAME_INFORMATION: flags@0, root@8, name length@16, name@20.
        IntPtr buffer = Marshal.AllocHGlobal(24 + name.Length);
        try
        {
            Marshal.Copy(new byte[24 + name.Length], 0, buffer, 24 + name.Length);
            Marshal.WriteInt32(buffer, replace ? 1 : 0);
            Marshal.WriteIntPtr(buffer, 8, parent.DangerousGetHandle());
            Marshal.WriteInt32(buffer, 16, name.Length);
            Marshal.Copy(name, 0, buffer + 20, name.Length);
            int status = NtSetInformationFile(source, out _, buffer, (uint)(24 + name.Length), 10);
            GC.KeepAlive(parent);
            if (status < 0) Status(status, "Rooted rename failed; observe retained names before recovery");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static void VerifyTransactedWriter(SafeFileHandle handle)
    {
        byte[] version = new byte[16];
        if (!DeviceIoControl(handle, 0x00094170, IntPtr.Zero, 0, version, version.Length, out int returned, IntPtr.Zero))
            throw Error("unsupported", "Rooted TxF writer verification unavailable (Windows error " + Marshal.GetLastWin32Error() + ").");
        if (returned < 14 || BitConverter.ToUInt32(version, 0) != uint.MaxValue)
            throw Error("unsupported", "Native rooted open did not enlist as a transacted writer; replacement refused.");
    }

    internal sealed class ReplacementTransaction : IDisposable
    {
        private readonly SafeFileHandle handle;
        private readonly IntPtr previous;
        private bool committed;
        private bool disposed;

        internal ReplacementTransaction()
        {
            RequireOrdinaryContext();
            SafeFileHandle? created = null;
            try
            {
                previous = RtlGetCurrentTransaction();
                handle = created = CreateTransaction(IntPtr.Zero, IntPtr.Zero, 1, 0, 0, 10000, "Toolbox contained leaf replacement");
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    throw Error("unsupported", "KTM/TxF replacement transaction unavailable (Windows error " + error + ").");
                }
                if (!RtlSetCurrentTransaction(handle.DangerousGetHandle()) || RtlGetCurrentTransaction() != handle.DangerousGetHandle())
                {
                    RestoreTransactionAndClose(handle, previous);
                    throw Error("unsupported", "Native transaction context could not be installed.");
                }
                activeTransaction = handle;
            }
            catch (EntryPointNotFoundException) { created?.Dispose(); throw Error("unsupported", "Native KTM/TxF entry points are unavailable."); }
            catch (DllNotFoundException) { created?.Dispose(); throw Error("unsupported", "Native KTM/TxF libraries are unavailable."); }
        }

        internal void Commit()
        {
            CheckTransactionThread();
            if (!CommitTransaction(handle)) Win32("Committing contained replacement; observe actual names before recovery");
            committed = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (!committed) RollbackTransaction(handle);
            }
            finally
            {
                if (ReferenceEquals(activeTransaction, handle)) activeTransaction = null;
                RestoreTransactionAndClose(handle, previous);
            }
        }
    }

    internal static IEnumerable<string> Names(SafeFileHandle directory)
    {
        RequireOrdinaryContext();
        IntPtr buffer = Marshal.AllocHGlobal(65536);
        try
        {
            bool restart = true;
            while (true)
            {
                int status = NtQueryDirectoryFile(directory, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    out var io, buffer, 65536, 1, false, IntPtr.Zero, restart);
                restart = false;
                if (status == unchecked((int)0x80000006)) yield break;
                if (status < 0) Status(status, "Enumerating opened directory");
                int used = checked((int)io.Information.ToUInt64());
                int offset = 0;
                do
                {
                    if (offset > used - 64) throw Error("corrupt", "Malformed native directory result.");
                    int next = Marshal.ReadInt32(buffer, offset);
                    int bytes = Marshal.ReadInt32(buffer, offset + 60);
                    if (bytes < 0 || bytes % 2 != 0 || bytes > used - offset - 64)
                        throw Error("corrupt", "Malformed native entry name.");
                    string name = Marshal.PtrToStringUni(buffer + offset + 64, bytes / 2)!;
                    if (name != "." && name != "..") yield return name;
                    if (next == 0) break;
                    if (next < 64 || next > used - offset) throw Error("corrupt", "Malformed native entry offset.");
                    offset += next;
                } while (true);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static void Win32(string operation) => ThrowWin32(Marshal.GetLastWin32Error(), operation);
    private static void Status(int status, string operation) => ThrowWin32(unchecked((int)RtlNtStatusToDosError(status)), operation);
    private static void ThrowWin32(int error, string operation)
    {
        string code = error switch { 2 or 3 => "missing", 32 or 33 => "busy", 80 or 183 => "initialization_conflict",
            50 or 87 or 120 or 6805 or 6822 => "unsupported", 6800 => "busy", 4390 or 4392 or 1920 => "unsafe_path", _ => "io_error" };
        throw Error(code, $"{operation} (Windows error {error}).");
    }

    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes
    { public int Length; public IntPtr RootDirectory, ObjectName; public uint Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
    [StructLayout(LayoutKind.Sequential)] private struct IoStatus { public IntPtr Status; public UIntPtr Information; }
    [StructLayout(LayoutKind.Sequential)] internal struct ByHandleInfo
    {
        public uint FileAttributes; public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }
    [DllImport("ntdll.dll")] private static extern int NtCreateFile(out SafeFileHandle handle, uint access,
        ref ObjectAttributes attributes, out IoStatus io, IntPtr allocation, uint fileAttributes,
        uint share, uint disposition, uint options, IntPtr ea, uint eaLength);
    [DllImport("ntdll.dll")] private static extern int NtSetInformationFile(SafeFileHandle handle, out IoStatus io, IntPtr buffer, uint length, int infoClass);
    [DllImport("ntdll.dll")] private static extern int NtQueryDirectoryFile(SafeFileHandle handle, IntPtr evt, IntPtr apc, IntPtr context,
        out IoStatus io, IntPtr buffer, uint length, int infoClass, [MarshalAs(UnmanagedType.U1)] bool single,
        IntPtr name, [MarshalAs(UnmanagedType.U1)] bool restart);
    [DllImport("ntdll.dll")] private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("ntdll.dll")] private static extern IntPtr RtlGetCurrentTransaction();
    [DllImport("ntdll.dll")]
    [return: MarshalAs(UnmanagedType.U1)] private static extern bool RtlSetCurrentTransaction(IntPtr transaction);
    [DllImport("KtmW32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateTransaction(
        IntPtr security, IntPtr unitOfWork, uint options, uint isolationLevel, uint isolationFlags, uint timeout, string description);
    [DllImport("KtmW32.dll", SetLastError = true)] private static extern bool CommitTransaction(SafeFileHandle transaction);
    [DllImport("KtmW32.dll", SetLastError = true)] private static extern bool RollbackTransaction(SafeFileHandle transaction);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle handle,
        uint code, IntPtr input, int inputLength, byte[] output, int outputLength, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern uint GetDriveTypeW(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern uint QueryDosDeviceW(string name, StringBuilder target, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool GetVolumeInformationByHandleW(
        SafeFileHandle handle, StringBuilder? label, int labelLength, out uint serial, out uint maxComponent,
        out uint flags, StringBuilder fs, int fsLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, int length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleInfo info);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(SafeFileHandle file, IntPtr bytes, int count, out int read, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool WriteFile(SafeFileHandle file, byte[] bytes, int count, out int written, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool FlushFileBuffers(SafeFileHandle file);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFilePointerEx(SafeFileHandle file, long offset, out long position, uint origin);
}
