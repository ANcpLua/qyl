# syntax=docker/dockerfile:1@sha256:87999aa3d42bdc6bea60565083ee17e86d1f3339802f543c0d03998580f9cb89
# Qyl.Collector - Telemetry API Backend
# Build from repository root:
#   docker build -f services/qyl.collector/Dockerfile -t qyl-collector .

# ============================================================================
# STAGE 1: Build React Dashboard
# ============================================================================
FROM node:24-slim@sha256:cb4e8f7c443347358b7875e717c29e27bf9befc8f5a26cf18af3c3dec80e58c5 AS dashboard

WORKDIR /dashboard

# Copy package files and install dependencies
COPY services/qyl.dashboard/package.json services/qyl.dashboard/package-lock.json ./
RUN npm ci --prefer-offline

# Copy source and build
COPY services/qyl.dashboard/ ./
RUN npm run build && \
    test -f ./dist/index.html || (echo "ERROR: Dashboard build failed - no index.html" && exit 1)

# ============================================================================
# STAGE 2: Build .NET
# ============================================================================
# Using Debian (not Alpine) because DuckDB native libs require glibc
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5 AS build

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props Version.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore (layer caching - only invalidated when .csproj changes)
COPY services/qyl.collector/*.csproj services/qyl.collector/
COPY internal/qyl.instrumentation/*.csproj internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/*.csproj internal/qyl.instrumentation.generators/
COPY internal/qyl.collector.storage.generators/*.csproj internal/qyl.collector.storage.generators/

# Restore dependencies
WORKDIR /src/services/qyl.collector
RUN dotnet restore

# Copy source files
WORKDIR /src
COPY services/qyl.collector/ services/qyl.collector/
COPY internal/qyl.instrumentation/ internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/ internal/qyl.instrumentation.generators/
COPY internal/qyl.collector.storage.generators/ internal/qyl.collector.storage.generators/

# The dashboard build lands where the csproj embed gate expects it: a Release publish embeds
# dist/** into the assembly (QylEmbedDashboard), and the publish fails loudly if dist is missing.
COPY --from=dashboard /dashboard/dist services/qyl.dashboard/dist/

# Publish (single-origin: dashboard is an embedded resource, no separate wwwroot)
WORKDIR /src/services/qyl.collector
RUN dotnet publish -c Release -o /app --no-restore \
    -p:QylEmbedDashboard=true \
    -p:TreatWarningsAsErrors=false \
    -p:PublishAot=false \
    -p:PublishTrimmed=false \
    -p:EnableTrimAnalyzer=false \
    -p:RunAnalyzers=false \
    -p:RunAnalyzersDuringBuild=false \
    -p:WarningLevel=0 \
    && test -f /app/qyl.collector.dll || (echo "ERROR: .NET publish failed" && exit 1)

# ============================================================================
# STAGE 3: Runtime image
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:7644f992230d35cf230017189d4038c0ae0f7388b13f4f7ae1900a155bafb597 AS runtime

LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="qyl Telemetry Collector API"
LABEL org.opencontainers.image.source="https://github.com/ANcpLua/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# curl for container healthchecks; non-root user with writable dirs
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/* && \
    groupadd --system qyl && useradd --system --gid qyl qyl && \
    mkdir -p /app /data && chown -R qyl:qyl /app /data

WORKDIR /app

# Copy published .NET app (the dashboard rides inside the assembly as embedded resources)
COPY --from=build --chown=qyl:qyl /app .

USER qyl

EXPOSE 5100 4317 4318

# Environment defaults (can be overridden at runtime)
# QYL_GRPC_PORT=4317 enables gRPC OTLP ingestion by default (ADR-001)
# QYL_DATA_PATH uses /data for volume mount compatibility
ENV QYL_BIND_ADDRESS=0.0.0.0 \
    QYL_GRPC_PORT=4317 \
    QYL_OTLP_PORT=4318 \
    QYL_DATA_PATH=/data/qyl.duckdb \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD curl -sf "http://localhost:${PORT:-5100}/health" || exit 1

ENTRYPOINT ["dotnet", "qyl.collector.dll"]
