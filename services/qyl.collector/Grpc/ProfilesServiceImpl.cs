using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace Qyl.Collector.Grpc;

internal sealed class ProfilesServiceImpl(IQylStore store)
    : ProfilesService.ProfilesServiceBase
{
    public override Task<ExportProfilesServiceResponse> Export(
        ExportProfilesServiceRequest request,
        ServerCallContext context) =>
        GrpcExport.ExecuteAsync(async () =>
        {
            var profileBatch = OtlpConverter.ConvertProfiles(request);
            var profiles = IngestionStorageMapper.ToProfileStorageRows(profileBatch);

            if (profiles.Count <= 0) return new ExportProfilesServiceResponse();

            await store.InsertProfilesAsync(profiles, context.CancellationToken).ConfigureAwait(false);
            return new ExportProfilesServiceResponse();
        }, "profile");
}
