using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class WorkflowCheckpointWindowsFileSystemTests
{
    [Fact]
    public void Rooted_backend_preserves_atomic_checkpoint_file_contract()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"qyl-windows-checkpoints-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var files = new WorkflowCheckpointWindowsFileSystem(root);
            var source = Path.Combine(root, "run", "checkpoint.tmp");
            var collision = Path.Combine(root, "run", "collision.tmp");
            var destination = Path.Combine(root, "run", "checkpoint.json");

            Write(files.CreateFile(source), [1, 2, 3]);
            Assert.True(files.MoveFileNoReplace(source, destination));
            Assert.False(files.Exists(source));
            Assert.Equal(3, files.Length(destination));

            Write(files.CreateFile(collision), [4]);
            Assert.False(files.MoveFileNoReplace(collision, destination));
            Assert.True(files.Exists(collision));
            Assert.Equal(3, files.Length(destination));

            var entry = Assert.Single(
                files.EnumerateTree(root),
                entry => entry.Path == destination);
            Assert.Equal(destination, entry.Path);
            Assert.Equal(3, entry.Length);
            Assert.Throws<InvalidDataException>(() => files.CreateFile(
                Path.Combine(root, "..", "escape.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void Write(Microsoft.Win32.SafeHandles.SafeFileHandle handle, byte[] bytes)
    {
        using var stream = new FileStream(
            handle,
            FileAccess.Write,
            bufferSize: 1,
            isAsync: false);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }
}
