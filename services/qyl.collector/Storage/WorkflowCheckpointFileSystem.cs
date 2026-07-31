using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Qyl.Collector.Storage;

internal readonly record struct WorkflowCheckpointFileSystemEntry(
    string Path,
    long Length,
    bool IsDirectory);

internal sealed partial class WorkflowCheckpointFileSystem : IDisposable
{
    private const string CheckpointNativeLibrary = "qyl_checkpoint_native";
    private const int ErrorInterrupted = 4;
    private const int AtRemovedDirectoryLinux = 0x200;
    private const int AtRemovedDirectoryMacOs = 0x80;
    private const int ErrorAlreadyExists = 17;
    private const int ErrorDirectoryNotEmptyLinux = 39;
    private const int ErrorDirectoryNotEmptyMacOs = 66;
    private const int ErrorIsDirectoryLinux = 21;
    private const int ErrorIsDirectoryMacOs = 21;
    private const int ErrorLoopLinux = 40;
    private const int ErrorLoopMacOs = 62;
    private const int ErrorNoEntry = 2;
    private const int ErrorNotDirectory = 20;
    private const int OpenCloseOnExecLinux = 0x80000;
    private const int OpenCloseOnExecMacOs = 0x1000000;
    private const int OpenDirectoryLinux = 0x10000;
    private const int OpenDirectoryLinuxArm64 = 0x4000;
    private const int OpenDirectoryMacOs = 0x100000;
    private const int OpenNoFollowLinux = 0x20000;
    private const int OpenNoFollowLinuxArm64 = 0x8000;
    private const int OpenNoFollowMacOs = 0x100;
    private const int OpenReadOnly = 0;
    private const int OpenNonBlockingLinux = 0x800;
    private const int OpenNonBlockingMacOs = 0x4;
    private const int OpenWriteOnly = 1;
    private const uint OwnerDirectoryMode = 0x1c0;
    private const uint OwnerFileMode = 0x180;

    private readonly string _root;
    private readonly SafeFileHandle _rootHandle;

    public WorkflowCheckpointFileSystem(string root)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "On-disk workflow checkpoints require Linux or macOS no-follow filesystem operations.");
        }
        if (RuntimeInformation.ProcessArchitecture is not
            (Architecture.X64 or Architecture.Arm64))
        {
            throw new PlatformNotSupportedException(
                "On-disk workflow checkpoints require an x64 or arm64 process.");
        }

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(_root);
        var descriptor = OpenRoot(
            _root,
            OpenReadOnly | OpenDirectory | OpenNoFollow | OpenCloseOnExec);
        _rootHandle = TakeHandle(descriptor, _root);
    }

    public void CreateDirectory(string path)
    {
        using var directory = OpenRelativeDirectory(path, create: true);
    }

    public SafeFileHandle CreateFile(string path)
    {
        using var parent = OpenParent(path, createParents: true, out var name);
        var descriptor = OpenAtCreateHandle(
            parent,
            name,
            OpenWriteOnly | OpenNoFollow | OpenCloseOnExec,
            OwnerFileMode);
        return TakeHandle(descriptor, path);
    }

    public SafeFileHandle OpenFile(string path)
    {
        using var parent = OpenParent(path, createParents: false, out var name);
        var descriptor = OpenAtHandle(
            parent,
            name,
            OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenCloseOnExec);
        return TakeHandle(descriptor, path);
    }

    public bool Exists(string path)
    {
        try
        {
            using var handle = OpenFile(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public long Length(string path)
    {
        try
        {
            using var handle = OpenFile(path);
            return RandomAccess.GetLength(handle);
        }
        catch (FileNotFoundException)
        {
            return 0;
        }
        catch (DirectoryNotFoundException)
        {
            return 0;
        }
    }

    public bool MoveFileNoReplace(string source, string destination)
    {
        using (var sourceHandle = OpenFile(source))
        {
        }
        using var sourceParent = OpenParent(
            source,
            createParents: false,
            out var sourceName);
        using var destinationParent = OpenParent(
            destination,
            createParents: true,
            out var destinationName);
        if (LinkAtHandles(
                sourceParent,
                sourceName,
                destinationParent,
                destinationName,
                0) is not 0)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error is ErrorAlreadyExists)
                return false;
            ThrowPathError(error, source);
        }
        if (UnlinkAtHandle(sourceParent, sourceName, 0) is not 0)
            ThrowPathError(Marshal.GetLastPInvokeError(), source);
        return true;
    }

    public bool MoveFileToQuarantine(string source, string destination)
    {
        try
        {
            return MoveFileNoReplace(source, destination);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public bool DeleteFile(string path)
    {
        try
        {
            using (var handle = OpenFile(path))
            {
            }
            using var parent = OpenParent(path, createParents: false, out var name);
            if (UnlinkAtHandle(parent, name, 0) is 0)
                return true;
            var error = Marshal.GetLastPInvokeError();
            if (error is ErrorNoEntry)
                return false;
            ThrowPathError(error, path);
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public bool DeleteEmptyDirectory(string path)
    {
        try
        {
            using (var directory = OpenRelativeDirectory(path, create: false))
            {
            }
            using var parent = OpenParent(path, createParents: false, out var name);
            if (UnlinkAtHandle(parent, name, AtRemovedDirectory) is 0)
                return true;
            var error = Marshal.GetLastPInvokeError();
            if (error is ErrorNoEntry)
                return true;
            if (error == ErrorDirectoryNotEmpty)
                return false;
            ThrowPathError(error, path);
            return false;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
    }

    public IEnumerable<WorkflowCheckpointFileSystemEntry> EnumerateTree(string path)
    {
        using var directory = TryOpenTreeRoot(path);
        if (directory is null)
            return [];
        var entries = new List<WorkflowCheckpointFileSystemEntry>();
        CollectDirectory(directory, Path.GetFullPath(path), entries);
        return entries;
    }

    private SafeFileHandle? TryOpenTreeRoot(string path)
    {
        try
        {
            return OpenRelativeDirectory(path, create: false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void Dispose() => _rootHandle.Dispose();

    private static void CollectDirectory(
        SafeFileHandle directory,
        string path,
        List<WorkflowCheckpointFileSystemEntry> entries)
    {
        foreach (var name in ReadDirectoryNames(directory))
        {
            var childPath = Path.Combine(path, name);
            SafeFileHandle? childDirectory = null;
            bool wasDirectory;
            try
            {
                childDirectory = TryOpenDirectoryAt(directory, name);
                wasDirectory = childDirectory is not null;
                if (childDirectory is not null)
                {
                    CollectDirectory(childDirectory, childPath, entries);
                    entries.Add(new WorkflowCheckpointFileSystemEntry(
                        childPath,
                        0,
                        IsDirectory: true));
                }
            }
            finally
            {
                childDirectory?.Dispose();
            }

            if (wasDirectory)
                continue;

            SafeFileHandle? file = null;
            try
            {
                file = TryOpenFileAt(directory, name);
                if (file is not null)
                {
                    entries.Add(new WorkflowCheckpointFileSystemEntry(
                        childPath,
                        RandomAccess.GetLength(file),
                        IsDirectory: false));
                }
            }
            finally
            {
                file?.Dispose();
            }
        }
    }

    private static IReadOnlyList<string> ReadDirectoryNames(
        SafeFileHandle directory)
    {
        var descriptor = DuplicateHandle(directory);
        var stream = FdOpenDirectory(descriptor);
        if (stream == IntPtr.Zero)
        {
            var error = Marshal.GetLastPInvokeError();
            CloseDescriptor(descriptor);
            ThrowPathError(error, "directory");
        }

        var names = new List<string>();
        try
        {
            while (true)
            {
                var errorLocation = GetErrorLocation();
                Marshal.WriteInt32(errorLocation, 0);
                var entry = ReadDirectory(stream);
                if (entry == IntPtr.Zero)
                {
                    var error = Marshal.ReadInt32(errorLocation);
                    if (error is not 0)
                        ThrowPathError(error, "directory");
                    break;
                }

                var nameOffset = OperatingSystem.IsMacOS() ? 21 : 19;
                var recordLength = Marshal.ReadInt16(entry, 16);
                if (recordLength <= nameOffset)
                    throw new InvalidDataException(
                        "Workflow checkpoint directory entry is malformed.");
                var name = Marshal.PtrToStringUTF8(entry + nameOffset);
                if (string.IsNullOrEmpty(name) || name is "." or "..")
                    continue;
                if (name.Contains(Path.DirectorySeparatorChar) ||
                    name.Contains('\0'))
                {
                    throw new InvalidDataException(
                        "Workflow checkpoint directory entry contains an invalid name.");
                }
                names.Add(name);
            }
        }
        finally
        {
            CloseDirectory(stream);
        }
        return names;
    }

    private static SafeFileHandle? TryOpenDirectoryAt(
        SafeFileHandle parent,
        string name)
    {
        var descriptor = OpenAtHandle(
            parent,
            name,
            OpenReadOnly |
            OpenNonBlocking |
            OpenDirectory |
            OpenNoFollow |
            OpenCloseOnExec);
        if (descriptor >= 0)
            return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        var error = Marshal.GetLastPInvokeError();
        if (error is ErrorNoEntry or ErrorNotDirectory)
            return null;
        ThrowPathError(error, name);
        return null;
    }

    private static SafeFileHandle? TryOpenFileAt(
        SafeFileHandle parent,
        string name)
    {
        var descriptor = OpenAtHandle(
            parent,
            name,
            OpenReadOnly | OpenNonBlocking | OpenNoFollow | OpenCloseOnExec);
        if (descriptor >= 0)
            return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        var error = Marshal.GetLastPInvokeError();
        if (error is ErrorNoEntry || error == ErrorLoop)
            return null;
        ThrowPathError(error, name);
        return null;
    }

    private SafeFileHandle OpenParent(
        string path,
        bool createParents,
        out string name)
    {
        var components = Components(path);
        if (components.Length is 0)
            throw new InvalidDataException("Workflow checkpoint root has no parent entry.");
        name = components[^1];
        return OpenComponents(components.AsSpan(0, components.Length - 1), createParents);
    }

    private SafeFileHandle OpenRelativeDirectory(string path, bool create)
    {
        var components = Components(path);
        return OpenComponents(components, create);
    }

    private SafeFileHandle OpenComponents(
        ReadOnlySpan<string> components,
        bool create)
    {
        var descriptor = OpenAtHandle(
            _rootHandle,
            ".",
            OpenReadOnly | OpenDirectory | OpenNoFollow | OpenCloseOnExec);
        var current = TakeHandle(descriptor, _root);
        try
        {
            foreach (var component in components)
            {
                if (create &&
                    MkdirAtHandle(current, component, OwnerDirectoryMode) is not 0)
                {
                    var error = Marshal.GetLastPInvokeError();
                    if (error is not ErrorAlreadyExists)
                        ThrowPathError(error, component);
                }

                descriptor = OpenAtHandle(
                    current,
                    component,
                    OpenReadOnly | OpenDirectory | OpenNoFollow | OpenCloseOnExec);
                var next = TakeHandle(descriptor, component);
                current.Dispose();
                current = next;
            }
            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private string[] Components(string path)
    {
        var candidate = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(_root, candidate);
        if (relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Workflow checkpoint path escapes its storage directory.");
        }
        if (relative is ".")
            return [];
        var components = relative.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var component in components)
        {
            if (component is "." or ".." ||
                component.Contains(Path.AltDirectorySeparatorChar))
            {
                throw new InvalidDataException(
                    "Workflow checkpoint path contains an invalid component.");
            }
        }
        return components;
    }

    private static SafeFileHandle TakeHandle(int descriptor, string path)
    {
        if (descriptor >= 0)
            return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        ThrowPathError(Marshal.GetLastPInvokeError(), path);
        throw new UnreachableException();
    }

    [DoesNotReturn]
    private static void ThrowPathError(int error, string path)
    {
        if (error is ErrorNoEntry)
            throw new FileNotFoundException("Workflow checkpoint path is missing.", path);
        if (error is ErrorNotDirectory)
        {
            throw new InvalidDataException(
                "Workflow checkpoint path contains a non-directory component.");
        }
        if (error == ErrorLoop)
        {
            throw new InvalidDataException(
                "Workflow checkpoint path contains a symbolic link.");
        }
        if (error == ErrorIsDirectory)
        {
            throw new InvalidDataException(
                "Workflow checkpoint path unexpectedly names a directory.");
        }
        throw new IOException(
            $"Workflow checkpoint filesystem operation failed for '{path}': " +
            Marshal.GetPInvokeErrorMessage(error));
    }

    private static int AtRemovedDirectory =>
        OperatingSystem.IsLinux()
            ? AtRemovedDirectoryLinux
            : AtRemovedDirectoryMacOs;

    private static int ErrorDirectoryNotEmpty =>
        OperatingSystem.IsLinux()
            ? ErrorDirectoryNotEmptyLinux
            : ErrorDirectoryNotEmptyMacOs;

    private static int ErrorIsDirectory =>
        OperatingSystem.IsLinux()
            ? ErrorIsDirectoryLinux
            : ErrorIsDirectoryMacOs;

    private static int ErrorLoop =>
        OperatingSystem.IsLinux()
            ? ErrorLoopLinux
            : ErrorLoopMacOs;

    private static int OpenCloseOnExec =>
        OperatingSystem.IsLinux()
            ? OpenCloseOnExecLinux
            : OpenCloseOnExecMacOs;

    private static int OpenDirectory =>
        OperatingSystem.IsMacOS()
            ? OpenDirectoryMacOs
            : RuntimeInformation.ProcessArchitecture is Architecture.Arm64
                ? OpenDirectoryLinuxArm64
                : OpenDirectoryLinux;

    private static int OpenNoFollow =>
        OperatingSystem.IsMacOS()
            ? OpenNoFollowMacOs
            : RuntimeInformation.ProcessArchitecture is Architecture.Arm64
                ? OpenNoFollowLinuxArm64
                : OpenNoFollowLinux;

    private static int OpenNonBlocking =>
        OperatingSystem.IsLinux()
            ? OpenNonBlockingLinux
            : OpenNonBlockingMacOs;

    private static IntPtr GetErrorLocation() =>
        OperatingSystem.IsLinux()
            ? GetLinuxErrorLocation()
            : GetMacOsErrorLocation();

    private static int LinkAtHandles(
        SafeFileHandle oldDirectory,
        string oldPath,
        SafeFileHandle newDirectory,
        string newPath,
        int flags)
    {
        var oldAdded = false;
        var newAdded = false;
        try
        {
            oldDirectory.DangerousAddRef(ref oldAdded);
            newDirectory.DangerousAddRef(ref newAdded);
            while (true)
            {
                var result = LinkAt(
                    oldDirectory.DangerousGetHandle().ToInt32(),
                    oldPath,
                    newDirectory.DangerousGetHandle().ToInt32(),
                    newPath,
                    flags);
                if (result is 0)
                    return result;
                var error = Marshal.GetLastPInvokeError();
                if (error is ErrorInterrupted)
                    continue;
                Marshal.SetLastPInvokeError(error);
                return result;
            }
        }
        finally
        {
            if (newAdded)
                newDirectory.DangerousRelease();
            if (oldAdded)
                oldDirectory.DangerousRelease();
        }
    }

    private static int MkdirAtHandle(
        SafeFileHandle directory,
        string path,
        uint mode)
    {
        var added = false;
        try
        {
            directory.DangerousAddRef(ref added);
            while (true)
            {
                var result = MkdirAt(
                    directory.DangerousGetHandle().ToInt32(),
                    path,
                    mode);
                if (result is 0)
                    return result;
                var error = Marshal.GetLastPInvokeError();
                if (error is ErrorInterrupted)
                    continue;
                Marshal.SetLastPInvokeError(error);
                return result;
            }
        }
        finally
        {
            if (added)
                directory.DangerousRelease();
        }
    }

    private static int OpenAtCreateHandle(
        SafeFileHandle directory,
        string path,
        int flags,
        uint mode)
    {
        var added = false;
        try
        {
            directory.DangerousAddRef(ref added);
            while (true)
            {
                var result = OpenAtCreate(
                    directory.DangerousGetHandle().ToInt32(),
                    path,
                    flags,
                    mode);
                if (result >= 0)
                    return result;
                var error = Marshal.GetLastPInvokeError();
                if (error is ErrorInterrupted)
                    continue;
                Marshal.SetLastPInvokeError(error);
                return result;
            }
        }
        finally
        {
            if (added)
                directory.DangerousRelease();
        }
    }

    private static int OpenAtHandle(
        SafeFileHandle directory,
        string path,
        int flags)
    {
        var added = false;
        try
        {
            directory.DangerousAddRef(ref added);
            while (true)
            {
                var result = OpenAt(
                    directory.DangerousGetHandle().ToInt32(),
                    path,
                    flags);
                if (result >= 0)
                    return result;
                var error = Marshal.GetLastPInvokeError();
                if (error is ErrorInterrupted)
                    continue;
                Marshal.SetLastPInvokeError(error);
                return result;
            }
        }
        finally
        {
            if (added)
                directory.DangerousRelease();
        }
    }

    private static int UnlinkAtHandle(
        SafeFileHandle directory,
        string path,
        int flags)
    {
        var added = false;
        try
        {
            directory.DangerousAddRef(ref added);
            while (true)
            {
                var result = UnlinkAt(
                    directory.DangerousGetHandle().ToInt32(),
                    path,
                    flags);
                if (result is 0)
                    return result;
                var error = Marshal.GetLastPInvokeError();
                if (error is ErrorInterrupted)
                    continue;
                Marshal.SetLastPInvokeError(error);
                return result;
            }
        }
        finally
        {
            if (added)
                directory.DangerousRelease();
        }
    }

    private static int DuplicateHandle(SafeFileHandle handle)
    {
        var added = false;
        try
        {
            handle.DangerousAddRef(ref added);
            while (true)
            {
                var descriptor = Duplicate(
                    handle.DangerousGetHandle().ToInt32());
                if (descriptor >= 0)
                    return descriptor;
                var error = Marshal.GetLastPInvokeError();
                if (error is ErrorInterrupted)
                    continue;
                ThrowPathError(error, "directory");
            }
        }
        finally
        {
            if (added)
                handle.DangerousRelease();
        }
    }

    private static int OpenRoot(string path, int flags)
    {
        while (true)
        {
            var descriptor = Open(path, flags);
            if (descriptor >= 0)
                return descriptor;
            var error = Marshal.GetLastPInvokeError();
            if (error is ErrorInterrupted)
                continue;
            Marshal.SetLastPInvokeError(error);
            return descriptor;
        }
    }

    private static void CloseDescriptor(int descriptor)
    {
        if (Close(descriptor) is not 0 &&
            Marshal.GetLastPInvokeError() is not ErrorInterrupted)
        {
            ThrowPathError(Marshal.GetLastPInvokeError(), "directory");
        }
    }

    [LibraryImport("libc", EntryPoint = "linkat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int LinkAt(
        int oldDirectory,
        string oldPath,
        int newDirectory,
        string newPath,
        int flags);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Close(int descriptor);

    [LibraryImport("libc", EntryPoint = "closedir", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int CloseDirectory(IntPtr directory);

    [LibraryImport("libc", EntryPoint = "dup", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Duplicate(int descriptor);

    [LibraryImport("libc", EntryPoint = "fdopendir", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr FdOpenDirectory(int descriptor);

    [LibraryImport("libc", EntryPoint = "__errno_location")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr GetLinuxErrorLocation();

    [LibraryImport("libc", EntryPoint = "__error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr GetMacOsErrorLocation();

    [LibraryImport("libc", EntryPoint = "mkdirat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int MkdirAt(
        int directory,
        string path,
        uint mode);

    [LibraryImport(CheckpointNativeLibrary, EntryPoint = "qyl_openat_create", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int OpenAtCreate(
        int directory,
        string path,
        int flags,
        uint mode);

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "openat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int OpenAt(
        int directory,
        string path,
        int flags);

    [LibraryImport("libc", EntryPoint = "readdir", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr ReadDirectory(IntPtr directory);

    [LibraryImport("libc", EntryPoint = "unlinkat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int UnlinkAt(
        int directory,
        string path,
        int flags);
}
