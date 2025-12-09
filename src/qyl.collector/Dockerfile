# qyl.collector - Multi-stage Docker build
# Native AOT for minimal image size (~25MB)

# ============================================================================
# STAGE 1: Build dashboard (SolidJS/React)
# ============================================================================
FROM node:22-alpine AS dashboard-build

WORKDIR /dashboard
COPY ../qyl.dashboard/package*.json ./
RUN npm ci

COPY ../qyl.dashboard/ ./
RUN npm run build

# ============================================================================
# STAGE 2: Build .NET Native AOT
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS dotnet-build

# Install native AOT prerequisites
RUN apk add --no-cache clang build-base zlib-dev

WORKDIR /src

# Copy csproj and restore
COPY qyl.collector.csproj ./
RUN dotnet restore

# Copy source and build
COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

# ============================================================================
# STAGE 3: Runtime image
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime

# Labels for Docker Hub
LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="AI Observability Backend - Embedded DuckDB, Native AOT"
LABEL org.opencontainers.image.source="https://github.com/your-org/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# Create non-root user
RUN addgroup -S qyl && adduser -S qyl -G qyl

# Working directory
WORKDIR /app

# Copy published app
COPY --from=dotnet-build /app .

# Copy dashboard build to wwwroot
COPY --from=dashboard-build /dashboard/dist ./wwwroot

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
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5100/health || exit 1

# Entry point
ENTRYPOINT ["./qyl.collector"]
