# qyl.mcp - MCP Server for AI Agent Queries
# Native AOT build for minimal footprint
#
# Build from repository root:
#   docker build -f src/qyl.mcp/Dockerfile -t qyl-mcp .

# ============================================================================
# STAGE 1: Build .NET Native AOT
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# Install native AOT prerequisites
RUN apk add --no-cache clang build-base zlib-dev

WORKDIR /src

# Copy solution files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props Version.props global.json nuget.config ./
COPY eng/ eng/

# Copy protocol (ProjectReference) and mcp projects
COPY src/qyl.protocol/*.csproj src/qyl.protocol/
COPY src/qyl.mcp/*.csproj src/qyl.mcp/
WORKDIR /src/src/qyl.mcp
RUN dotnet restore

# Copy source and publish
WORKDIR /src
COPY src/qyl.protocol/ src/qyl.protocol/
COPY src/qyl.mcp/ src/qyl.mcp/
WORKDIR /src/src/qyl.mcp
RUN dotnet publish -c Release -o /app --no-restore

# ============================================================================
# STAGE 2: Runtime image
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime

# Labels for container registries
LABEL org.opencontainers.image.title="qyl.mcp"
LABEL org.opencontainers.image.description="MCP Server for AI Agent Telemetry Queries"
LABEL org.opencontainers.image.source="https://github.com/your-org/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# Create non-root user
RUN addgroup -S qyl && adduser -S qyl -G qyl

WORKDIR /app

# Copy published app
COPY --from=build /app .

# Set ownership
RUN chown -R qyl:qyl /app

# Switch to non-root user
USER qyl

# Environment variables
ENV QYL_COLLECTOR_URL=http://localhost:5100

ENTRYPOINT ["./qyl.mcp"]
