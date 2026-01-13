// =============================================================================
// qyl.collector - Semantic Convention Normalizer
// Normalizes OTel attribute names from any schema version to v1.38.0
// Owner: qyl.collector | Based on: ADR-003 Semantic Convention Normalization
// =============================================================================

#pragma warning disable AL0012 // Intentional deprecated attribute references for migration mapping

namespace qyl.collector.Ingestion;

/// <summary>
///     Normalizes OpenTelemetry semantic convention attribute names to v1.38.0.
///     Handles telemetry from apps using different SDK versions with different attribute names.
/// </summary>
/// <remarks>
///     <para>Example renames:</para>
///     <list type="bullet">
///         <item>http.method → http.request.method (v1.21.0)</item>
///         <item>db.statement → db.query.text (v1.24.0)</item>
///         <item>gen_ai.system → gen_ai.provider.name (v1.27.0)</item>
///     </list>
/// </remarks>
public static class SemconvNormalizer
{
    /// <summary>
    ///     Attribute renames: old name → new name.
    ///     Frozen dictionary for O(1) lookup on hot path.
    /// </summary>
    private static readonly FrozenDictionary<string, string> AttributeRenames = new Dictionary<string, string>
    {
        // =================================================================
        // HTTP (v1.21.0)
        // =================================================================
        ["http.method"] = "http.request.method",
        ["http.url"] = "url.full",
        ["http.target"] = "url.path",
        ["http.host"] = "server.address",
        ["http.scheme"] = "url.scheme",
        ["http.status_code"] = "http.response.status_code",
        ["http.flavor"] = "network.protocol.version",
        ["http.user_agent"] = "user_agent.original",
        ["http.request_content_length"] = "http.request.body.size",
        ["http.response_content_length"] = "http.response.body.size",
        ["http.server_name"] = "server.address",
        ["http.route"] = "http.route", // unchanged but explicit
        ["http.client_ip"] = "client.address",

        // =================================================================
        // Database (v1.24.0)
        // =================================================================
        ["db.statement"] = "db.query.text",
        ["db.operation"] = "db.operation.name",
        ["db.user"] = "db.client.connection.pool.name", // context-dependent
        ["db.name"] = "db.namespace",
        ["db.connection_string"] = "db.client.connection.string",
        ["db.jdbc.driver_classname"] = "db.client.connection.pool.name",

        // =================================================================
        // Network (v1.21.0)
        // =================================================================
        ["net.peer.name"] = "server.address",
        ["net.peer.port"] = "server.port",
        ["net.host.name"] = "server.address",
        ["net.host.port"] = "server.port",
        ["net.transport"] = "network.transport",
        ["net.protocol.name"] = "network.protocol.name",
        ["net.protocol.version"] = "network.protocol.version",
        ["net.sock.peer.addr"] = "network.peer.address",
        ["net.sock.peer.port"] = "network.peer.port",
        ["net.sock.host.addr"] = "network.local.address",
        ["net.sock.host.port"] = "network.local.port",

        // =================================================================
        // Messaging (v1.21.0)
        // =================================================================
        ["messaging.destination"] = "messaging.destination.name",
        ["messaging.destination_kind"] = "messaging.destination.kind",
        ["messaging.temp_destination"] = "messaging.destination.temporary",
        ["messaging.protocol"] = "network.protocol.name",
        ["messaging.protocol_version"] = "network.protocol.version",
        ["messaging.url"] = "url.full",
        ["messaging.message_id"] = "messaging.message.id",
        ["messaging.conversation_id"] = "messaging.message.conversation_id",
        ["messaging.message_payload_size_bytes"] = "messaging.message.body.size",
        ["messaging.message_payload_compressed_size_bytes"] = "messaging.message.envelope.size",

        // =================================================================
        // RPC (v1.21.0)
        // =================================================================
        ["rpc.method"] = "rpc.method",
        ["rpc.service"] = "rpc.service",
        ["rpc.grpc.status_code"] = "rpc.grpc.status_code",

        // =================================================================
        // GenAI (v1.27.0) - Primary focus for qyl
        // =================================================================
        ["gen_ai.system"] = "gen_ai.provider.name",
        ["gen_ai.request.model"] = "gen_ai.request.model",
        ["gen_ai.response.model"] = "gen_ai.response.model",
        ["gen_ai.request.max_tokens"] = "gen_ai.request.max_output_tokens",
        ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
        ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",

        // =================================================================
        // Exception (v1.21.0)
        // =================================================================
        ["exception.type"] = "exception.type",
        ["exception.message"] = "exception.message",
        ["exception.stacktrace"] = "exception.stacktrace",

        // =================================================================
        // Thread (v1.21.0)
        // =================================================================
        ["thread.id"] = "thread.id",
        ["thread.name"] = "thread.name",

        // =================================================================
        // Code (v1.21.0)
        // =================================================================
        ["code.function"] = "code.function.name",
        ["code.namespace"] = "code.namespace",
        ["code.filepath"] = "code.file.path",
        ["code.lineno"] = "code.line.number",

        // =================================================================
        // Peer (v1.21.0) - deprecated, map to server.*
        // =================================================================
        ["peer.service"] = "peer.service",

        // =================================================================
        // Enduser (v1.21.0)
        // =================================================================
        ["enduser.id"] = "enduser.id",
        ["enduser.role"] = "enduser.role",
        ["enduser.scope"] = "enduser.scope"
    }.ToFrozenDictionary();

    /// <summary>Target schema version for normalization.</summary>
    public static SemconvVersion TargetVersion => SemconvVersion.V1_38_0;

    /// <summary>
    ///     Normalizes an attribute name to v1.38.0.
    ///     Returns the original name if no rename is needed.
    /// </summary>
    /// <param name="attributeName">The attribute name to normalize.</param>
    /// <returns>The normalized attribute name.</returns>
    public static string Normalize(string attributeName) =>
        AttributeRenames.GetValueOrDefault(attributeName, attributeName);

    /// <summary>
    ///     Normalizes an attribute name to v1.38.0.
    ///     Returns the original name if no rename is needed.
    /// </summary>
    /// <param name="attributeName">The attribute name to normalize.</param>
    /// <returns>The normalized attribute name.</returns>
    public static ReadOnlySpan<char> Normalize(ReadOnlySpan<char> attributeName)
    {
        // Fast path: check if it's in our rename table
        var key = attributeName.ToString();
        return AttributeRenames.TryGetValue(key, out var newName) ? newName.AsSpan() : attributeName;
    }

    /// <summary>
    ///     Checks if an attribute name needs normalization.
    /// </summary>
    public static bool NeedsNormalization(string attributeName) =>
        AttributeRenames.ContainsKey(attributeName);

    /// <summary>
    ///     Gets all known attribute renames.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetAllRenames() => AttributeRenames;

    /// <summary>
    ///     Normalizes a dictionary of attributes in place.
    /// </summary>
    public static void NormalizeAttributes(Dictionary<string, object?> attributes)
    {
        // Collect keys that need renaming (can't modify during enumeration)
        List<(string oldKey, string newKey)>? renames = null;

        foreach (var key in attributes.Keys)
        {
            if (AttributeRenames.TryGetValue(key, out var newKey) && newKey != key)
            {
                renames ??= [];
                renames.Add((key, newKey));
            }
        }

        if (renames is null) return;

        foreach (var (oldKey, newKey) in renames)
        {
            // Only rename if new key doesn't already exist (preserve newer convention)
            if (!attributes.ContainsKey(newKey) && attributes.TryGetValue(oldKey, out var value))
            {
                attributes[newKey] = value;
                attributes.Remove(oldKey);
            }
        }
    }
}
