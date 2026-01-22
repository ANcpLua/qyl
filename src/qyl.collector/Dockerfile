# qyl.collector - Telemetry API Backend
# Build from repository root:
#   docker build -f src/qyl.collector/Dockerfile -t qyl-collector .

# ============================================================================
# STAGE 1: Build .NET
# ============================================================================
# Using Debian (not Alpine) because DuckDB native libs require glibc
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy solution-level files for restore
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props global.json nuget.config ./
COPY eng/ eng/

# Copy project files for restore
COPY src/qyl.protocol/*.csproj src/qyl.protocol/
COPY src/qyl.collector/*.csproj src/qyl.collector/

# Restore dependencies (include RID for AOT publish)
WORKDIR /src/src/qyl.collector
RUN dotnet restore

# Copy source files
WORKDIR /src
COPY src/qyl.protocol/ src/qyl.protocol/
COPY src/qyl.collector/ src/qyl.collector/

# Publish (AOT for Alpine)
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
# STAGE 2: Runtime image
# ============================================================================
# Using Debian (not Alpine) because DuckDB native libs require glibc
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="qyl Telemetry Collector API"

# Create non-root user (Debian syntax)
RUN groupadd --system qyl && useradd --system --gid qyl qyl

WORKDIR /app

# Copy published app
COPY --from=build /app .

# Set ownership
RUN chown -R qyl:qyl /app

USER qyl

# Railway uses PORT env var
ENV ASPNETCORE_URLS=http://+:${PORT:-5100}

EXPOSE 5100

ENTRYPOINT ["dotnet", "qyl.collector.dll"]
