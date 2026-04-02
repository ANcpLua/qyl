# syntax=docker/dockerfile:1
# qyl.mcp - MCP Server for AI Agent Queries
# Default container mode is remote Streamable HTTP for OpenAI / Anthropic connectors.
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

# Copy contracts (ProjectReference) and mcp projects
COPY src/qyl.contracts/*.csproj src/qyl.contracts/
COPY src/qyl.mcp/*.csproj src/qyl.mcp/
WORKDIR /src/src/qyl.mcp
RUN dotnet restore

# Copy source and publish
WORKDIR /src
COPY src/qyl.contracts/ src/qyl.contracts/
COPY src/qyl.mcp/ src/qyl.mcp/
WORKDIR /src/src/qyl.mcp
RUN dotnet publish -c Release -o /app --no-restore \
    -p:TreatWarningsAsErrors=false \
    -p:PublishTrimmed=false \
    -p:EnableTrimAnalyzer=false \
    -p:RunAnalyzers=false \
    -p:RunAnalyzersDuringBuild=false \
    -p:WarningLevel=0 \
    && test -f /app/qyl.mcp || (echo "ERROR: qyl.mcp publish failed" && exit 1)

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

EXPOSE 8080

# Copy published app
COPY --from=build /app .

# Set ownership
RUN chown -R qyl:qyl /app

# Switch to non-root user
USER qyl

# Environment variables
ENV QYL_COLLECTOR_URL=http://localhost:5100
ENV QYL_MCP_TRANSPORT=http
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["./qyl.mcp"]
