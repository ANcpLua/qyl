# syntax=docker/dockerfile:1
# qyl.collector - Telemetry API Backend
# Build from repository root:
#   docker build -f src/qyl.collector/Dockerfile -t qyl-collector .

# ============================================================================
# STAGE 1: Build React Dashboard
# ============================================================================
FROM node:22-slim AS dashboard

WORKDIR /dashboard

# Copy package files and install dependencies with Railway cache mount
COPY src/qyl.dashboard/package.json src/qyl.dashboard/package-lock.json ./
RUN --mount=type=cache,id=s/ff836187-b65c-4645-ab40-67b54fbfa93f-npm,target=/root/.npm \
    npm ci --prefer-offline

# Copy source and build
COPY src/qyl.dashboard/ ./
RUN npm run build && \
    test -f ./dist/index.html || (echo "ERROR: Dashboard build failed - no index.html" && exit 1)

# ============================================================================
# STAGE 2: Build .NET
# ============================================================================
# Using Debian (not Alpine) because DuckDB native libs require glibc
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore (layer caching - only invalidated when .csproj changes)
COPY src/qyl.protocol/*.csproj src/qyl.protocol/
COPY src/qyl.collector/*.csproj src/qyl.collector/
COPY src/qyl.servicedefaults/*.csproj src/qyl.servicedefaults/
COPY src/qyl.servicedefaults.generator/*.csproj src/qyl.servicedefaults.generator/
COPY src/qyl.instrumentation.generators/*.csproj src/qyl.instrumentation.generators/
COPY src/qyl.copilot/*.csproj src/qyl.copilot/

# Restore dependencies with Railway NuGet cache mount
WORKDIR /src/src/qyl.collector
RUN --mount=type=cache,id=s/ff836187-b65c-4645-ab40-67b54fbfa93f-nuget,target=/root/.nuget/packages \
    dotnet restore

# Copy source files
WORKDIR /src
COPY src/qyl.protocol/ src/qyl.protocol/
COPY src/qyl.collector/ src/qyl.collector/
COPY src/qyl.servicedefaults/ src/qyl.servicedefaults/
COPY src/qyl.servicedefaults.generator/ src/qyl.servicedefaults.generator/
COPY src/qyl.instrumentation.generators/ src/qyl.instrumentation.generators/
COPY src/qyl.copilot/ src/qyl.copilot/

# Publish with Railway NuGet cache mount
WORKDIR /src/src/qyl.collector
RUN --mount=type=cache,id=s/ff836187-b65c-4645-ab40-67b54fbfa93f-nuget,target=/root/.nuget/packages \
    dotnet publish -c Release -o /app --no-restore \
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="qyl Telemetry Collector API"
LABEL org.opencontainers.image.source="https://github.com/ANcpLua/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# Create non-root user and writable app directory (Debian syntax)
# IMPORTANT: Create /app with qyl ownership BEFORE WORKDIR so qyl can write files (DuckDB)
RUN groupadd --system qyl && useradd --system --gid qyl qyl && \
    mkdir -p /app /data && chown -R qyl:qyl /app /data

WORKDIR /app

# Copy published .NET app
COPY --from=build --chown=qyl:qyl /app .

# Copy dashboard build to wwwroot
COPY --from=dashboard --chown=qyl:qyl /dashboard/dist ./wwwroot/

USER qyl

EXPOSE 5100 4317

# Environment defaults (can be overridden at runtime)
# Note: QYL_PORT not set - allows Railway's PORT to take effect via fallback in Program.cs
# QYL_DATA_PATH uses /data for volume mount compatibility
ENV QYL_GRPC_PORT=0 \
    QYL_DATA_PATH=/data/qyl.duckdb \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

ENTRYPOINT ["dotnet", "qyl.collector.dll"]
