# syntax=docker/dockerfile:1@sha256:2780b5c3bab67f1f76c781860de469442999ed1a0d7992a5efdf2cffc0e3d769
# Qyl.Loom - AI Investigation Microservice
# Build from repository root:
#   docker build -f services/qyl.loom/Dockerfile -t qyl-loom .

# ============================================================================
# STAGE 1: Build .NET
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:dc8430e6024d454edadad1e160e1973be3cabbb7125998ef190d9e5c6adf7dbb AS build

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props Version.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore (layer caching). qyl.loom references qyl.instrumentation
# for WithQylTelemetry, which itself pulls in qyl.instrumentation.generators.
COPY packages/Qyl.Contracts/*.csproj packages/Qyl.Contracts/
COPY packages/Qyl.SemanticConventions/*.csproj packages/Qyl.SemanticConventions/
COPY internal/qyl.instrumentation/*.csproj internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/*.csproj internal/qyl.instrumentation.generators/
COPY services/qyl.loom/*.csproj services/qyl.loom/

# Restore dependencies
WORKDIR /src/services/qyl.loom
RUN dotnet restore

# Copy source files
WORKDIR /src
COPY packages/Qyl.Contracts/ packages/Qyl.Contracts/
COPY packages/Qyl.SemanticConventions/ packages/Qyl.SemanticConventions/
COPY internal/qyl.instrumentation/ internal/qyl.instrumentation/
COPY internal/qyl.instrumentation.generators/ internal/qyl.instrumentation.generators/
COPY services/qyl.loom/ services/qyl.loom/

# Publish
WORKDIR /src/services/qyl.loom
RUN dotnet publish -c Release -o /app --no-restore \
    -p:TreatWarningsAsErrors=false \
    -p:PublishAot=false \
    -p:PublishTrimmed=false \
    -p:EnableTrimAnalyzer=false \
    -p:RunAnalyzers=false \
    -p:RunAnalyzersDuringBuild=false \
    -p:WarningLevel=0 \
    && test -f /app/qyl.loom.dll || (echo "ERROR: .NET publish failed" && exit 1)

# ============================================================================
# STAGE 2: Runtime image
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:9b5222b0ff8e9eb991a7c1a64b25f0f771d21ccc05dfa1c834f5668ffd9cd73f AS runtime

LABEL org.opencontainers.image.title="qyl.loom"
LABEL org.opencontainers.image.description="qyl AI Investigation Microservice"
LABEL org.opencontainers.image.source="https://github.com/ANcpLua/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# curl for container healthchecks; non-root user
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/* && \
    groupadd --system qyl && useradd --system --gid qyl qyl && \
    mkdir -p /app && chown -R qyl:qyl /app

WORKDIR /app
COPY --from=build --chown=qyl:qyl /app .

USER qyl

EXPOSE 5200

ENV ASPNETCORE_URLS=http://+:5200 \
    QYL_COLLECTOR_URL=http://collector:5100 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD curl -sf http://localhost:5200/health || exit 1

ENTRYPOINT ["dotnet", "qyl.loom.dll"]
