using StatusCode = Grpc.Core.StatusCode;

namespace Qyl.Collector.Grpc;

internal static class GrpcExport
{
    // Single source of truth for the OTLP gRPC export error contract: cancellation -> Cancelled,
    // shutdown -> Unavailable, malformed payload (bad id lengths) -> InvalidArgument,
    // anything else -> Internal.
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> export, string dataKind)
    {
        try
        {
            return await export().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled"));
        }
        catch (ObjectDisposedException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Service is shutting down"));
        }
        catch (InvalidDataException)
        {
            // Deliberately no exception detail: collector responses never carry exception messages
            // (VerifyNoRemovedBuildSurface tombstones the pattern).
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Malformed {dataKind} payload."));
        }
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to process {dataKind} data."));
        }
    }
}
