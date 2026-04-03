# syntax=docker/dockerfile:1
# Qyl.Loom - AI Investigation Microservice
# Build from repository root:
#   docker build -f src/qyl.loom/Dockerfile -t qyl-loom .

# ============================================================================
# STAGE 1: Build .NET
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props Version.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore (layer caching)
COPY src/qyl.contracts/*.csproj src/qyl.contracts/
COPY src/qyl.loom/*.csproj src/qyl.loom/

# Restore dependencies
WORKDIR /src/src/qyl.loom
RUN dotnet restore

# Copy source files
WORKDIR /src
COPY src/qyl.contracts/ src/qyl.contracts/
COPY src/qyl.loom/ src/qyl.loom/

# Publish
WORKDIR /src/src/qyl.loom
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

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
