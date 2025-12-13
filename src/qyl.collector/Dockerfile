# qyl.collector - AI Observability Backend
# Multi-stage build with Native AOT for minimal image size (~25MB)
#
# Build from repository root:
#   docker build -f src/qyl.collector/Dockerfile -t qyl-collector .

# ============================================================================
# STAGE 1: Build .NET Native AOT
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS dotnet-build

# Install native AOT prerequisites
RUN apk add --no-cache clang build-base zlib-dev

WORKDIR /src

# Copy solution files for restore
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ src/Shared/

# Copy collector project and restore
COPY src/qyl.collector/*.csproj src/qyl.collector/
WORKDIR /src/src/qyl.collector
RUN dotnet restore

# Copy source and publish
WORKDIR /src
COPY src/qyl.collector/ src/qyl.collector/
WORKDIR /src/src/qyl.collector
RUN dotnet publish -c Release -o /app --no-restore

# ============================================================================
# STAGE 2: Runtime image
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime

# Labels for container registries
LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="AI Observability Backend - DuckDB + OTLP + SSE"
LABEL org.opencontainers.image.source="https://github.com/your-org/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# Create non-root user
RUN addgroup -S qyl && adduser -S qyl -G qyl

WORKDIR /app

# Copy published app
COPY --from=dotnet-build /app .

# Create data directory for DuckDB
RUN mkdir -p /data && chown -R qyl:qyl /data /app

# Switch to non-root user
USER qyl

# Environment variables
ENV QYL_PORT=5100
ENV QYL_DATA_PATH=/data/qyl.duckdb
ENV ASPNETCORE_URLS=http://+:5100

# Expose ports
EXPOSE 5100
EXPOSE 4317
EXPOSE 4318

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5100/health || exit 1

# Entry point
ENTRYPOINT ["./qyl.collector"]
