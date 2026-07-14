# syntax=docker/dockerfile:1@sha256:87999aa3d42bdc6bea60565083ee17e86d1f3339802f543c0d03998580f9cb89

FROM node:24-slim@sha256:6f7b03f7c2c8e2e784dcf9295400527b9b1270fd37b7e9a7285cf83b6951452d AS dashboard

WORKDIR /dashboard

COPY services/qyl.dashboard/package.json services/qyl.dashboard/package-lock.json ./
RUN npm ci --prefer-offline

COPY services/qyl.dashboard/ ./
RUN npm run build && \
    test -f ./dist/index.html || (echo "ERROR: Dashboard build failed - no index.html" && exit 1)

# DuckDB.NET ships glibc native assets; the AOT SDK supplies clang and zlib for ILC.
FROM mcr.microsoft.com/dotnet/sdk:10.0-aot@sha256:977c117b98abe301eeb4c2ed3f8deebf8b8f11e65549ee7886581d2b41641adb AS build

WORKDIR /src

COPY Directory.Build.props Directory.Build.targets Directory.Packages.props Version.props global.json nuget.config ./
COPY eng/ eng/

COPY services/qyl.collector/*.csproj services/qyl.collector/
COPY internal/qyl.instrumentation/*.csproj internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/*.csproj internal/qyl.instrumentation.generators/
COPY internal/qyl.collector.storage.generators/*.csproj internal/qyl.collector.storage.generators/

WORKDIR /src/services/qyl.collector
RUN dotnet restore -p:QylAot=true -p:TreatWarningsAsErrors=false

WORKDIR /src
COPY services/qyl.collector/ services/qyl.collector/
COPY internal/qyl.instrumentation/ internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/ internal/qyl.instrumentation.generators/
COPY internal/qyl.collector.storage.generators/ internal/qyl.collector.storage.generators/

# The dashboard build lands where the csproj embed gate expects it: a Release publish embeds
# dist/** into the assembly (QylEmbedDashboard), and the publish fails loudly if dist is missing.
COPY --from=dashboard /dashboard/dist services/qyl.dashboard/dist/

WORKDIR /src/services/qyl.collector
RUN dotnet publish -c Release -o /app --no-restore \
    -p:QylAot=true \
    -p:QylEmbedDashboard=true \
    -p:TreatWarningsAsErrors=false \
    -p:RunAnalyzers=false \
    -p:RunAnalyzersDuringBuild=false \
    && test -x /app/qyl.collector \
    && test ! -e /app/qyl.collector.dll \
    && test -f /app/libduckdb.so \
    || (echo "ERROR: Native AOT publish did not produce the expected collector artifacts" && exit 1)

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0@sha256:894098eafc82e5fa02ba9f2b71d426dc78252876b9e914caae77ed95cfce185a AS runtime

LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="qyl Telemetry Collector API"
LABEL org.opencontainers.image.source="https://github.com/ANcpLua/qyl"
LABEL org.opencontainers.image.licenses="MIT"

RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/* && \
    groupadd --system qyl && useradd --system --gid qyl qyl && \
    mkdir -p /app /data && chown -R qyl:qyl /app /data

WORKDIR /app

COPY --from=build --chown=qyl:qyl /app .

USER qyl

EXPOSE 5100 4317 4318

ENV QYL_BIND_ADDRESS=0.0.0.0 \
    QYL_GRPC_PORT=4317 \
    QYL_OTLP_PORT=4318 \
    QYL_DATA_PATH=/data/qyl.duckdb \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD curl -sf "http://localhost:${PORT:-5100}/health" || exit 1

ENTRYPOINT ["/app/qyl.collector"]
