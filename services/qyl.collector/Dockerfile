# syntax=docker/dockerfile:1@sha256:2780b5c3bab67f1f76c781860de469442999ed1a0d7992a5efdf2cffc0e3d769
# Qyl.Collector - Telemetry API Backend
# Build from repository root:
#   docker build -f services/qyl.collector/Dockerfile -t qyl-collector .

# ============================================================================
# STAGE 1: Build React Dashboard
# ============================================================================
FROM node:22-slim@sha256:d415caac2f1f77b98caaf9415c5f807e14bc8d7bdea62561ea2fef4fbd08a73c AS dashboard

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
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc AS build

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props Version.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore (layer caching - only invalidated when .csproj changes)
COPY packages/Qyl.Contracts/*.csproj packages/Qyl.Contracts/
COPY packages/Qyl.SemanticConventions/*.csproj packages/Qyl.SemanticConventions/
COPY packages/Qyl.Telemetry/*.csproj packages/Qyl.Telemetry/
COPY packages/Qyl.OpenTelemetry.SemanticConventions/*.csproj packages/Qyl.OpenTelemetry.SemanticConventions/
COPY packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/*.csproj packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/
COPY packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/*.csproj packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/
COPY services/qyl.collector/*.csproj services/qyl.collector/
COPY internal/qyl.instrumentation/*.csproj internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/*.csproj internal/qyl.instrumentation.generators/
COPY internal/qyl.collector.storage.generators/*.csproj internal/qyl.collector.storage.generators/

# Restore dependencies
WORKDIR /src/services/qyl.collector
RUN dotnet restore

# Copy source files
WORKDIR /src
COPY packages/Qyl.Contracts/ packages/Qyl.Contracts/
COPY packages/Qyl.SemanticConventions/ packages/Qyl.SemanticConventions/
COPY packages/Qyl.Telemetry/ packages/Qyl.Telemetry/
COPY packages/Qyl.OpenTelemetry.SemanticConventions/ packages/Qyl.OpenTelemetry.SemanticConventions/
COPY packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/ packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/
COPY packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/ packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/
COPY services/qyl.collector/ services/qyl.collector/
COPY internal/qyl.instrumentation/ internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/ internal/qyl.instrumentation.generators/
COPY internal/qyl.collector.storage.generators/ internal/qyl.collector.storage.generators/

# Publish
WORKDIR /src/services/qyl.collector
RUN dotnet publish -c Release -o /app --no-restore \
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:55e37c7795bfaf6b9cc5d77c155811d9569f529d86e20647704bc1d7dd9741d4 AS runtime

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

# Copy published .NET app
COPY --from=build --chown=qyl:qyl /app .

# Copy dashboard build to wwwroot
COPY --from=dashboard --chown=qyl:qyl /dashboard/dist ./wwwroot/

USER qyl

EXPOSE 5100 4317 4318

# Environment defaults (can be overridden at runtime)
# QYL_GRPC_PORT=4317 enables gRPC OTLP ingestion by default (ADR-001)
# QYL_DATA_PATH uses /data for volume mount compatibility
ENV QYL_GRPC_PORT=4317 \
    QYL_OTLP_PORT=4318 \
    QYL_DATA_PATH=/data/qyl.duckdb \
    ASPNETCORE_WEBROOT=/app/wwwroot \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD curl -sf "http://localhost:${PORT:-5100}/health" || exit 1

ENTRYPOINT ["dotnet", "qyl.collector.dll"]
