using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Qyl.Api.Contracts.Workflow;

namespace Qyl.Collector.Storage;

internal static class WorkflowRunCursorCodec
{
    private const string Version = "qylwr1";

    internal static string Encode(
        string projectId,
        WorkflowRunStatus? status,
        int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, offset);
        return string.Join(
            '.',
            Version,
            Hash(projectId),
            StatusToken(status),
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
    }

    internal static bool TryDecode(
        string? encoded,
        string projectId,
        WorkflowRunStatus? status,
        out int offset)
    {
        offset = 0;
        if (encoded is null)
            return true;
        var parts = encoded.Split('.');
        if (parts.Length is not 4 ||
            parts[0] != Version ||
            parts[1] != Hash(projectId) ||
            parts[2] != StatusToken(status))
        {
            return false;
        }

        try
        {
            var canonical = parts[3].Replace('-', '+').Replace('_', '/');
            canonical += (canonical.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                0 => "",
                _ => throw new FormatException()
            };
            var bytes = Convert.FromBase64String(canonical);
            if (bytes.Length is not sizeof(int) ||
                Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != parts[3])
            {
                return false;
            }
            offset = BinaryPrimitives.ReadInt32BigEndian(bytes);
            return offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string StatusToken(WorkflowRunStatus? status) => status switch
    {
        null => "all",
        WorkflowRunStatus.Active => "active",
        WorkflowRunStatus.Completed => "completed",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Interrupted => "interrupted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
