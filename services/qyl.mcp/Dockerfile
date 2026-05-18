# syntax=docker/dockerfile:1@sha256:2780b5c3bab67f1f76c781860de469442999ed1a0d7992a5efdf2cffc0e3d769
# qyl.mcp - MCP Server for AI Agent Queries
# Default container mode is remote Streamable HTTP for OpenAI / Anthropic connectors.
#
# Build from repository root:
#   docker build -f services/qyl.mcp/Dockerfile -t qyl-mcp .

# ============================================================================
# STAGE 1: Build .NET Native AOT
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:5c559aa5d99337e400d39ab4fa1f6979d126c29b20939d53658ed38300571e74 AS build

# Install native AOT prerequisites
RUN apk add --no-cache clang build-base zlib-dev

WORKDIR /src

# Copy solution files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props Version.props global.json nuget.config ./
COPY eng/ eng/

# Copy contracts (ProjectReference), generators (Analyzer ProjectReference), instrumentation
# (ProjectReference for WithQylTelemetry middleware pipeline), and mcp projects
COPY packages/Qyl.Contracts/*.csproj packages/Qyl.Contracts/
COPY packages/Qyl.SemanticConventions/*.csproj packages/Qyl.SemanticConventions/
COPY packages/Qyl.OpenTelemetry.SemanticConventions/*.csproj packages/Qyl.OpenTelemetry.SemanticConventions/
COPY packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/*.csproj packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/
COPY internal/qyl.instrumentation/*.csproj internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/*.csproj internal/qyl.instrumentation.generators/
COPY internal/qyl.mcp.generators/*.csproj internal/qyl.mcp.generators/
COPY services/qyl.mcp/*.csproj services/qyl.mcp/
WORKDIR /src/services/qyl.mcp
RUN dotnet restore

# Copy source and publish
WORKDIR /src
COPY packages/Qyl.Contracts/ packages/Qyl.Contracts/
COPY packages/Qyl.SemanticConventions/ packages/Qyl.SemanticConventions/
COPY packages/Qyl.OpenTelemetry.SemanticConventions/ packages/Qyl.OpenTelemetry.SemanticConventions/
COPY packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/ packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/
COPY internal/qyl.instrumentation/ internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/ internal/qyl.instrumentation.generators/
COPY internal/qyl.mcp.generators/ internal/qyl.mcp.generators/
COPY services/qyl.mcp/ services/qyl.mcp/
WORKDIR /src/services/qyl.mcp
# RID triggers PublishSelfContained=true + PublishSingleFile=true in qyl.mcp.csproj
# (the runtime image is runtime-deps, which does NOT include the .NET runtime).
RUN dotnet publish -c Release -o /app -r linux-musl-x64 --no-restore \
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
# Stage 1 cross-compiles to linux-musl-x64 (see `-r linux-musl-x64` above),
# so the runtime stage must also be amd64 — otherwise multi-arch hosts
# (e.g. macOS arm64 dev machines) resolve runtime-deps to arm64 and the
# musl-x64 binary fails to load (`ld-musl-x86_64.so.1 not found`).
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine@sha256:f276c0256ffca8fe816d48ba261962b54fea1b0e6f870b6a60b3b705c89e78ac AS runtime

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
