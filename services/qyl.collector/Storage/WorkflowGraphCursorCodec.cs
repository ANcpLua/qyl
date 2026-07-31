using System.Security.Cryptography;
using System.Text;
using Qyl.Api.Contracts.Workflow;

namespace Qyl.Collector.Storage;

internal sealed class WorkflowCursorRejectedException(
    WorkflowCursorKind kind,
    WorkflowCursorFailureReason reason,
    string currentGeneration) : Exception("Workflow graph cursor was rejected.")
{
    public WorkflowCursorKind Kind { get; } = kind;
    public WorkflowCursorFailureReason Reason { get; } = reason;
    public string CurrentGeneration { get; } = currentGeneration;
}

internal static class WorkflowGraphCursorCodec
{
    private const string Version = "qylwg1";
    internal const int MaximumEncodedLength = 1536;
    private const int MaximumAnchorScalars = 192;
    private static readonly UTF8Encoding s_strictUtf8 = new(false, true);

    internal static string EncodeNode(
        string projectId,
        string runId,
        string generation,
        string anchor) =>
        Encode('n', projectId, runId, generation, anchor);

    internal static string EncodeEdge(
        string projectId,
        string runId,
        string generation,
        string anchor) =>
        Encode('e', projectId, runId, generation, anchor);

    internal static string DecodeNode(
        string encoded,
        string projectId,
        string runId,
        string generation) =>
        Decode(encoded, 'n', WorkflowCursorKind.Node, projectId, runId, generation);

    internal static string DecodeEdge(
        string encoded,
        string projectId,
        string runId,
        string generation) =>
        Decode(encoded, 'e', WorkflowCursorKind.Edge, projectId, runId, generation);

    private static string Encode(
        char kind,
        string projectId,
        string runId,
        string generation,
        string anchor)
    {
        var encoded = string.Join(
            '.',
            Version,
            kind,
            Hash(projectId),
            Hash(runId),
            generation,
            Base64UrlEncode(anchor));
        if (encoded.Length > MaximumEncodedLength)
            throw new InvalidDataException("Workflow graph cursor exceeds its encoded limit.");
        return encoded;
    }

    private static string Decode(
        string encoded,
        char expectedKind,
        WorkflowCursorKind publicKind,
        string projectId,
        string runId,
        string generation)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaximumEncodedLength)
            throw Rejected(publicKind, WorkflowCursorFailureReason.Invalid, generation);
        var parts = encoded.Split('.');
        if (parts.Length is not 6 ||
            parts[0] != Version ||
            parts[1].Length is not 1 ||
            parts[1][0] != expectedKind ||
            !IsLowerHexDigest(parts[2]) ||
            !IsLowerHexDigest(parts[3]) ||
            !WorkflowCheckpointStore.IsCanonicalGeneration(parts[4]))
        {
            throw Rejected(publicKind, WorkflowCursorFailureReason.Invalid, generation);
        }
        if (parts[2] != Hash(projectId) || parts[3] != Hash(runId))
            throw Rejected(publicKind, WorkflowCursorFailureReason.WrongRun, generation);
        if (parts[4] != generation)
            throw Rejected(publicKind, WorkflowCursorFailureReason.WrongGeneration, generation);

        string anchor;
        try
        {
            anchor = Base64UrlDecode(parts[5]);
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            throw Rejected(publicKind, WorkflowCursorFailureReason.Invalid, generation);
        }
        if (string.IsNullOrEmpty(anchor) ||
            anchor.EnumerateRunes().Count() > MaximumAnchorScalars ||
            Base64UrlEncode(anchor) != parts[5])
        {
            throw Rejected(publicKind, WorkflowCursorFailureReason.Invalid, generation);
        }
        return anchor;
    }

    private static WorkflowCursorRejectedException Rejected(
        WorkflowCursorKind kind,
        WorkflowCursorFailureReason reason,
        string generation) =>
        new(kind, reason, generation);

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => "",
            _ => throw new FormatException("Workflow graph cursor is not valid base64url.")
        };
        return s_strictUtf8.GetString(Convert.FromBase64String(normalized));
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsLowerHexDigest(string value)
    {
        if (value.Length is not 64)
            return false;
        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}
