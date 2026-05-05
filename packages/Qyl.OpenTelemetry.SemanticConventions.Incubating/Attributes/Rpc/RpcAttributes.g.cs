

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Rpc;

public static class RpcAttributes
{
    [global::System.Obsolete("Replaced by rpc.response.status_code.", false)]
    public const string ConnectRpcErrorCode = "rpc.connect_rpc.error_code";

    public static class ConnectRpcErrorCodeValues
    {
        public const string Aborted = "aborted";

        public const string AlreadyExists = "already_exists";

        public const string Cancelled = "cancelled";

        public const string DataLoss = "data_loss";

        public const string DeadlineExceeded = "deadline_exceeded";

        public const string FailedPrecondition = "failed_precondition";

        public const string Internal = "internal";

        public const string InvalidArgument = "invalid_argument";

        public const string NotFound = "not_found";

        public const string OutOfRange = "out_of_range";

        public const string PermissionDenied = "permission_denied";

        public const string ResourceExhausted = "resource_exhausted";

        public const string Unauthenticated = "unauthenticated";

        public const string Unavailable = "unavailable";

        public const string Unimplemented = "unimplemented";

        public const string Unknown = "unknown";
    }

    [global::System.Obsolete("Replaced by rpc.request.metadata.", false)]
    public const string ConnectRpcRequestMetadata = "rpc.connect_rpc.request.metadata";

    [global::System.Obsolete("Replaced by rpc.response.metadata.", false)]
    public const string ConnectRpcResponseMetadata = "rpc.connect_rpc.response.metadata";

    [global::System.Obsolete("Replaced by rpc.request.metadata.", false)]
    public const string GrpcRequestMetadata = "rpc.grpc.request.metadata";

    [global::System.Obsolete("Replaced by rpc.response.metadata.", false)]
    public const string GrpcResponseMetadata = "rpc.grpc.response.metadata";

    [global::System.Obsolete("Use string representation of the gRPC status code on the `rpc.response.status_code` attribute.", false)]
    public const string GrpcStatusCode = "rpc.grpc.status_code";

    public static class GrpcStatusCodeValues
    {
        public const string Ok = "0";

        public const string Cancelled = "1";

        public const string Unknown = "2";

        public const string InvalidArgument = "3";

        public const string DeadlineExceeded = "4";

        public const string NotFound = "5";

        public const string AlreadyExists = "6";

        public const string PermissionDenied = "7";

        public const string ResourceExhausted = "8";

        public const string FailedPrecondition = "9";

        public const string Aborted = "10";

        public const string OutOfRange = "11";

        public const string Unimplemented = "12";

        public const string Internal = "13";

        public const string Unavailable = "14";

        public const string DataLoss = "15";

        public const string Unauthenticated = "16";
    }

    [global::System.Obsolete("Use string representation of the error code on the `rpc.response.status_code` attribute.", false)]
    public const string JsonrpcErrorCode = "rpc.jsonrpc.error_code";

    [global::System.Obsolete("Use the span status description when reporting JSON-RPC spans.", false)]
    public const string JsonrpcErrorMessage = "rpc.jsonrpc.error_message";

    [global::System.Obsolete("Replaced by jsonrpc.request.id.", false)]
    public const string JsonrpcRequestId = "rpc.jsonrpc.request_id";

    [global::System.Obsolete("Replaced by jsonrpc.protocol.version.", false)]
    public const string JsonrpcVersion = "rpc.jsonrpc.version";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string MessageCompressedSize = "rpc.message.compressed_size";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string MessageId = "rpc.message.id";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string MessageType = "rpc.message.type";

    public static class MessageTypeValues
    {
        public const string Received = "RECEIVED";

        public const string Sent = "SENT";
    }

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string MessageUncompressedSize = "rpc.message.uncompressed_size";

    public const string Method = "rpc.method";

    public const string MethodOriginal = "rpc.method_original";

    public const string RequestMetadata = "rpc.request.metadata";

    public const string ResponseMetadata = "rpc.response.metadata";

    public const string ResponseStatusCode = "rpc.response.status_code";

    [global::System.Obsolete("Value should be included in `rpc.method` which is expected to be a fully-qualified name.", false)]
    public const string Service = "rpc.service";

    [global::System.Obsolete("Replaced by rpc.system.name.", false)]
    public const string System = "rpc.system";

    public static class SystemValues
    {
        public const string ApacheDubbo = "apache_dubbo";

        public const string ConnectRpc = "connect_rpc";

        public const string DotnetWcf = "dotnet_wcf";

        public const string Grpc = "grpc";

        public const string JavaRmi = "java_rmi";

        public const string Jsonrpc = "jsonrpc";

        public const string OncRpc = "onc_rpc";
    }

    public const string SystemName = "rpc.system.name";

    public static class SystemNameValues
    {
        public const string Connectrpc = "connectrpc";

        public const string Dubbo = "dubbo";

        public const string Grpc = "grpc";

        public const string Jsonrpc = "jsonrpc";
    }
}
