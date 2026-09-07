# Native AOT container build, after dotnet/dotnet-docker samples/releasesapi:
# an SDK image that carries clang for the ILCompiler, a chiseled runtime-deps image with no .NET runtime at all.
# Build context is the repository root (see eng/compose.yaml) so packages/Qyl.Api.Sdk/ and the
# Directory.*.props, Version.props and global.json are available.
# No BuildKit-only instructions (--mount, --link) on purpose: the file builds with any Docker builder.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
WORKDIR /source

COPY . .
# The NuGet caches are removed in the same layer: they are dead weight, and the legacy builder cannot export the http-cache
# (file names containing '$' break its diff stream).
RUN dotnet publish samples/qyl.sample/qyl.sample.csproj --configuration Release --output /app \
    && rm -f /app/*.dbg /app/*.Development.json \
    && rm -rf /root/.nuget /root/.local/share/NuGet

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["./qyl.sample"]
