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
RUN npm run build

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
COPY src/qyl.instrumentation.generators/*.csproj src/qyl.instrumentation.generators/

# Restore dependencies
WORKDIR /src/src/qyl.collector
RUN dotnet restore

# Copy source files
WORKDIR /src
COPY src/qyl.protocol/ src/qyl.protocol/
COPY src/qyl.collector/ src/qyl.collector/
COPY src/qyl.instrumentation.generators/ src/qyl.instrumentation.generators/

# Publish
WORKDIR /src/src/qyl.collector
RUN dotnet publish -c Release -o /app --no-restore \
    -p:TreatWarningsAsErrors=false \
    -p:PublishAot=false \
    -p:PublishTrimmed=false \
    -p:EnableTrimAnalyzer=false \
    -p:RunAnalyzers=false \
    -p:RunAnalyzersDuringBuild=false \
    -p:WarningLevel=0 \
    || true && test -f /app/qyl.collector.dll

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

# Copy dashboard build to wwwroot
COPY --from=dashboard /dashboard/dist ./wwwroot/

# Set ownership
RUN chown -R qyl:qyl /app

USER qyl

# Railway sets PORT env var at runtime - app must listen on it
EXPOSE 5100

# Set QYL_PORT from Railway's PORT (Program.cs uses QYL_PORT for Kestrel config)
# ASPNETCORE_URLS is ignored when explicit ConfigureKestrel is used
ENTRYPOINT ["/bin/sh", "-c", "QYL_PORT=${PORT:-5100} exec dotnet qyl.collector.dll"]
