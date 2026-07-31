using Microsoft.Win32.SafeHandles;

namespace Qyl.Collector.Storage;

/// <summary>
/// Owns the Windows checkpoint filesystem boundary. Windows has no <c>openat</c>,
/// so every operation validates the rooted path and rejects reparse points before
/// using the platform's atomic create-new and no-overwrite move primitives.
/// </summary>
internal sealed class WorkflowCheckpointWindowsFileSystem
{
    private const FileShare ReadShare = FileShare.Read | FileShare.Write;
    private readonly string _root;

    public WorkflowCheckpointWindowsFileSystem(string root)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(_root);
        RejectReparsePoint(_root);
    }

    public void CreateDirectory(string path) => EnsureDirectory(Resolve(path));

    public SafeFileHandle CreateFile(string path)
    {
        var resolved = Resolve(path);
        EnsureDirectory(Path.GetDirectoryName(resolved)!);
        return File.OpenHandle(
            resolved,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            FileOptions.WriteThrough);
    }

    public SafeFileHandle OpenFile(string path)
    {
        var resolved = Resolve(path);
        ValidateExistingComponents(resolved, includeLeaf: true);
        return File.OpenHandle(
            resolved,
            FileMode.Open,
            FileAccess.Read,
            ReadShare,
            FileOptions.RandomAccess);
    }

    public bool Exists(string path)
    {
        var resolved = Resolve(path);
        if (!File.Exists(resolved))
            return false;
        ValidateExistingComponents(resolved, includeLeaf: true);
        return true;
    }

    public DateTime LastWriteTimeUtc(string path)
    {
        try
        {
            using var handle = OpenFile(path);
            return File.GetLastWriteTimeUtc(handle);
        }
        catch (FileNotFoundException)
        {
            return DateTime.MinValue;
        }
        catch (DirectoryNotFoundException)
        {
            return DateTime.MinValue;
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
        var resolvedSource = Resolve(source);
        var resolvedDestination = Resolve(destination);
        ValidateExistingComponents(resolvedSource, includeLeaf: true);
        EnsureDirectory(Path.GetDirectoryName(resolvedDestination)!);
        try
        {
            File.Move(resolvedSource, resolvedDestination, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(resolvedDestination))
        {
            ValidateExistingComponents(resolvedDestination, includeLeaf: true);
            return false;
        }
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
        var resolved = Resolve(path);
        if (!File.Exists(resolved))
            return false;
        ValidateExistingComponents(resolved, includeLeaf: true);
        File.Delete(resolved);
        return true;
    }

    public bool DeleteEmptyDirectory(string path)
    {
        var resolved = Resolve(path);
        if (!Directory.Exists(resolved))
            return true;
        ValidateExistingComponents(resolved, includeLeaf: true);
        try
        {
            Directory.Delete(resolved, recursive: false);
            return true;
        }
        catch (IOException) when (Directory.Exists(resolved))
        {
            return false;
        }
    }

    public IEnumerable<WorkflowCheckpointFileSystemEntry> EnumerateTree(string path)
    {
        var resolved = Resolve(path);
        if (!Directory.Exists(resolved))
            return [];
        ValidateExistingComponents(resolved, includeLeaf: true);
        var entries = new List<WorkflowCheckpointFileSystemEntry>();
        CollectDirectory(resolved, entries);
        return entries;
    }

    private void CollectDirectory(
        string directory,
        List<WorkflowCheckpointFileSystemEntry> entries)
    {
        foreach (var child in Directory.EnumerateFileSystemEntries(directory))
        {
            var resolved = Resolve(child);
            var attributes = File.GetAttributes(resolved);
            if ((attributes & FileAttributes.ReparsePoint) is not 0)
                continue;
            if ((attributes & FileAttributes.Directory) is not 0)
            {
                CollectDirectory(resolved, entries);
                entries.Add(new WorkflowCheckpointFileSystemEntry(
                    resolved,
                    0,
                    IsDirectory: true));
                continue;
            }

            using var handle = File.OpenHandle(
                resolved,
                FileMode.Open,
                FileAccess.Read,
                ReadShare,
                FileOptions.RandomAccess);
            entries.Add(new WorkflowCheckpointFileSystemEntry(
                resolved,
                RandomAccess.GetLength(handle),
                IsDirectory: false));
        }
    }

    private void EnsureDirectory(string path)
    {
        var resolved = Resolve(path);
        var relative = Path.GetRelativePath(_root, resolved);
        var current = _root;
        if (relative is ".")
            return;
        foreach (var component in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            Directory.CreateDirectory(current);
            RejectReparsePoint(current);
        }
    }

    private void ValidateExistingComponents(string path, bool includeLeaf)
    {
        var relative = Path.GetRelativePath(_root, path);
        var components = relative is "."
            ? []
            : relative.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);
        var count = includeLeaf ? components.Length : Math.Max(0, components.Length - 1);
        var current = _root;
        RejectReparsePoint(current);
        for (var index = 0; index < count; index++)
        {
            current = Path.Combine(current, components[index]);
            RejectReparsePoint(current);
        }
    }

    private string Resolve(string path)
    {
        var candidate = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(_root, candidate);
        if (relative == ".." ||
            relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Workflow checkpoint path escapes its storage directory.");
        }
        if (relative is ".")
            return _root;
        foreach (var component in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (component is "." or ".." ||
                component.Contains(Path.AltDirectorySeparatorChar))
            {
                throw new InvalidDataException(
                    "Workflow checkpoint path contains an invalid component.");
            }
        }
        return candidate;
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) is not 0)
        {
            throw new InvalidDataException(
                "Workflow checkpoint path contains a reparse point.");
        }
    }
}
