# qyl.collector - Telemetry API Backend
# Build from repository root:
#   docker build -f src/qyl.collector/Dockerfile -t qyl-collector .

# ============================================================================
# STAGE 1: Build React Dashboard
# ============================================================================
FROM node:22-slim AS dashboard

WORKDIR /dashboard

# Copy package files and install dependencies
COPY src/qyl.dashboard/package.json src/qyl.dashboard/package-lock.json ./
RUN npm ci

# Copy source and build
COPY src/qyl.dashboard/ ./
RUN npm run build && \
    test -f ./dist/index.html || (echo "ERROR: Dashboard build failed - no index.html" && exit 1)

# ============================================================================
# STAGE 2: Build .NET
# ============================================================================
# Using Debian (not Alpine) because DuckDB native libs require glibc
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Cache buster - change this value to force full rebuild
ARG CACHE_BUST=2026-01-28-v2

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore
COPY src/qyl.protocol/*.csproj src/qyl.protocol/
COPY src/qyl.collector/*.csproj src/qyl.collector/
COPY src/qyl.servicedefaults/*.csproj src/qyl.servicedefaults/
COPY src/qyl.servicedefaults.generator/*.csproj src/qyl.servicedefaults.generator/
COPY src/qyl.instrumentation.generators/*.csproj src/qyl.instrumentation.generators/

# Restore dependencies
WORKDIR /src/src/qyl.collector
RUN dotnet restore

# Copy source files
WORKDIR /src
COPY src/qyl.protocol/ src/qyl.protocol/
COPY src/qyl.collector/ src/qyl.collector/
COPY src/qyl.servicedefaults/ src/qyl.servicedefaults/
COPY src/qyl.servicedefaults.generator/ src/qyl.servicedefaults.generator/
COPY src/qyl.instrumentation.generators/ src/qyl.instrumentation.generators/

# Debug: show canary file and Program.cs header to verify correct files
RUN echo "=== CANARY ===" && cat src/qyl.collector/CANARY.txt || echo "NO CANARY" && \
    echo "=== PROGRAM.CS HEADER ===" && head -5 src/qyl.collector/Program.cs

# Publish
WORKDIR /src/src/qyl.collector
# Publish with reduced analysis for CI stability
# Note: Full analysis runs locally via Directory.Build.props
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
# Using Debian (not Alpine) because DuckDB native libs require glibc
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="qyl Telemetry Collector API"

# Create non-root user (Debian syntax)
RUN groupadd --system qyl && useradd --system --gid qyl qyl

WORKDIR /app

# Copy published .NET app
COPY --from=build /app .

# Copy dashboard build to wwwroot and verify
COPY --from=dashboard /dashboard/dist ./wwwroot/
RUN test -f ./wwwroot/index.html || (echo "ERROR: Dashboard not copied to wwwroot" && exit 1)

# Set ownership
RUN chown -R qyl:qyl /app

USER qyl

# Railway sets PORT env var at runtime - app must listen on it
EXPOSE 5100

# Set QYL_PORT from Railway's PORT (Program.cs uses QYL_PORT for Kestrel config)
# Set QYL_GRPC_PORT=0 by default to disable gRPC on Railway (single-port platform)
# Override with QYL_GRPC_PORT=4317 to enable gRPC on platforms that support multiple ports
# ASPNETCORE_URLS is ignored when explicit ConfigureKestrel is used
ENTRYPOINT ["/bin/sh", "-c", "QYL_PORT=${PORT:-5100} QYL_GRPC_PORT=${QYL_GRPC_PORT:-0} exec dotnet qyl.collector.dll"]
