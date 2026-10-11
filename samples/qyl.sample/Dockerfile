# Native AOT container build, after dotnet/dotnet-docker samples/releasesapi:
# an SDK image that carries clang for the ILCompiler, a chiseled runtime-deps image with no .NET runtime at all.
# Build context is the repository root (see eng/compose.yaml) so packages/Qyl.Api.Sdk/ and the
# Directory.*.props, Version.props and global.json are available.
# No BuildKit-only instructions (--mount, --link) on purpose: the file builds with any Docker builder.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot@sha256:88446bc2708f037046d48ef2bd77c437e981ca01229d7005ac2016ec42e05347 AS build
WORKDIR /source

COPY . .
# The NuGet caches are removed in the same layer: they are dead weight, and the legacy builder cannot export the http-cache
# (file names containing '$' break its diff stream).
RUN dotnet publish samples/qyl.sample/qyl.sample.csproj --configuration Release --output /app \
    && rm -f /app/*.dbg /app/*.Development.json \
    && rm -rf /root/.nuget /root/.local/share/NuGet

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:dc5cd0c7d0a39b825312b4f0986d99b9adf9b6a3af69bc23cd345c27d9ed916a
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["./qyl.sample"]
