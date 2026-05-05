

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Message;

public static class MessageAttributes
{
    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string CompressedSize = "message.compressed_size";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string Id = "message.id";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string Type = "message.type";

    public static class TypeValues
    {
        public const string Received = "RECEIVED";

        public const string Sent = "SENT";
    }

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string UncompressedSize = "message.uncompressed_size";
}
