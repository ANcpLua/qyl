using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using StatusCode = Grpc.Core.StatusCode;

namespace Qyl.Collector.Grpc;

internal sealed class ProfilesServiceImpl(IQylStore store)
    : ProfilesService.ProfilesServiceBase
{
    private readonly IQylStore _store = Guard.NotNull(store);

    public override async Task<ExportProfilesServiceResponse> Export(
        ExportProfilesServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var profileBatch = OtlpConverter.ConvertProfiles(request);
            var profiles = IngestionStorageMapper.ToProfileStorageRows(profileBatch);

            if (profiles.Count <= 0) return new ExportProfilesServiceResponse();

            await _store.InsertProfilesAsync(profiles, context.CancellationToken).ConfigureAwait(false);
            return new ExportProfilesServiceResponse();
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled"));
        }
        catch (ObjectDisposedException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Service is shutting down"));
        }
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Failed to process profile data."));
        }
    }
}
