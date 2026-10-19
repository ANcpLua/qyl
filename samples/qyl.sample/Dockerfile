# Native AOT container build, after dotnet/dotnet-docker samples/releasesapi:
# an SDK image that carries clang for the ILCompiler, a chiseled runtime-deps image with no .NET runtime at all.
# Build context is the repository root (ApiSdkContainerScenario builds it from there) so packages/Qyl.Api.Sdk/ and the
# Directory.*.props, Version.props and global.json are available.
# No BuildKit-only instructions (--mount, --link) on purpose: the file builds with any Docker builder.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot@sha256:62a3ebde694544ae0790b5ca2ffd56db04298af5652b7295c88fd61097752664 AS build
WORKDIR /source

COPY . .
# The NuGet caches are removed in the same layer: they are dead weight, and the legacy builder cannot export the http-cache
# (file names containing '$' break its diff stream).
RUN DOTNET_PROCESSOR_COUNT=4 dotnet publish samples/qyl.sample/qyl.sample.csproj --configuration Release --output /app \
    -maxcpucount:4 -p:UseSharedCompilation=false \
    && rm -f /app/*.dbg /app/*.Development.json \
    && rm -rf /root/.nuget /root/.local/share/NuGet

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:dc5cd0c7d0a39b825312b4f0986d99b9adf9b6a3af69bc23cd345c27d9ed916a
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["./qyl.sample"]
