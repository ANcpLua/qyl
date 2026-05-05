#nullable enable

namespace Qyl.Domains.Transport.Rpc;

public sealed class RpcAttributes
{
    public required Qyl.Domains.Transport.Rpc.RpcSystem System { get; init; }
    public string? Service { get; init; }
    public string? Method { get; init; }
    public string? ServerAddress { get; init; }
    public int? ServerPort { get; init; }
    public Qyl.Domains.Transport.Rpc.GrpcStatusCode? GrpcStatusCode { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? GrpcRequestMetadata { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? GrpcResponseMetadata { get; init; }
    public Qyl.Domains.Transport.Rpc.RpcMessageType? MessageType { get; init; }
    public long? MessageId { get; init; }
    public long? MessageCompressedSize { get; init; }
    public long? MessageUncompressedSize { get; init; }
    public Qyl.Domains.Transport.Rpc.ConnectRpcErrorCode? ConnectRpcErrorCode { get; init; }
    public int? JsonrpcErrorCode { get; init; }
    public string? JsonrpcErrorMessage { get; init; }
    public string? JsonrpcRequestId { get; init; }
    public string? JsonrpcVersion { get; init; }
}

public sealed class RpcServerDurationMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Transport.Rpc.RpcSystem System { get; init; }
    public required string Service { get; init; }
    public required string Method { get; init; }
    public Qyl.Domains.Transport.Rpc.GrpcStatusCode? GrpcStatusCode { get; init; }
}

public sealed class RpcClientDurationMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Transport.Rpc.RpcSystem System { get; init; }
    public required string Service { get; init; }
    public required string Method { get; init; }
    public required string ServerAddress { get; init; }
    public required int ServerPort { get; init; }
    public Qyl.Domains.Transport.Rpc.GrpcStatusCode? GrpcStatusCode { get; init; }
}

public enum RpcSystem
{
    Grpc,
    ApacheThrift,
    ConnectRpc,
    Jsonrpc,
    ApacheDubbo,
    Wcf,
    JavaRmi,
    DotnetRemoting
}

public enum GrpcStatusCode
{
    Ok = 0,
    Cancelled = 1,
    Unknown = 2,
    InvalidArgument = 3,
    DeadlineExceeded = 4,
    NotFound = 5,
    AlreadyExists = 6,
    PermissionDenied = 7,
    ResourceExhausted = 8,
    FailedPrecondition = 9,
    Aborted = 10,
    OutOfRange = 11,
    Unimplemented = 12,
    Internal = 13,
    Unavailable = 14,
    DataLoss = 15,
    Unauthenticated = 16
}

public enum RpcMessageType
{
    Sent,
    Received
}

public enum ConnectRpcErrorCode
{
    Cancelled,
    Unknown,
    InvalidArgument,
    DeadlineExceeded,
    NotFound,
    AlreadyExists,
    PermissionDenied,
    ResourceExhausted,
    FailedPrecondition,
    Aborted,
    OutOfRange,
    Unimplemented,
    Internal,
    Unavailable,
    DataLoss,
    Unauthenticated
}
