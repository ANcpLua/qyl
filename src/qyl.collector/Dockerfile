# qyl.collector - Telemetry API Backend
# Build from repository root:
#   docker build -f src/qyl.collector/Dockerfile -t qyl-collector .

# ============================================================================
# STAGE 1: Build .NET
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# Install native AOT prerequisites (clang, build-base for native compilation)
RUN apk add --no-cache clang build-base zlib-dev

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore
COPY src/qyl.protocol/*.csproj src/qyl.protocol/
COPY src/qyl.collector/*.csproj src/qyl.collector/

# Restore dependencies (include RID for AOT publish)
WORKDIR /src/src/qyl.collector
RUN dotnet restore -r linux-musl-x64

# Copy source files
WORKDIR /src
COPY src/qyl.protocol/ src/qyl.protocol/
COPY src/qyl.collector/ src/qyl.collector/

# Publish (AOT for Alpine)
WORKDIR /src/src/qyl.collector
RUN dotnet publish -c Release -r linux-musl-x64 -o /app --no-restore

# ============================================================================
# STAGE 2: Runtime image
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime

LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="qyl Telemetry Collector API"

# Create non-root user
RUN addgroup -S qyl && adduser -S qyl -G qyl

WORKDIR /app

# Copy published app
COPY --from=build /app .

# Set ownership
RUN chown -R qyl:qyl /app

USER qyl

# Railway uses PORT env var
ENV ASPNETCORE_URLS=http://+:${PORT:-5100}

EXPOSE 5100

ENTRYPOINT ["./qyl.collector"]
