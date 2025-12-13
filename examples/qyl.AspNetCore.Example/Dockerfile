# qyl.AspNetCore.Example - Sample instrumented ASP.NET Core app
# Shows OpenTelemetry integration with qyl.collector
#
# Build from repository root:
#   docker build -f examples/qyl.AspNetCore.Example/Dockerfile -t qyl-example .

# ============================================================================
# STAGE 1: Build
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copy solution files for restore
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ src/Shared/

# Copy example project and restore
COPY examples/qyl.AspNetCore.Example/*.csproj examples/qyl.AspNetCore.Example/
WORKDIR /src/examples/qyl.AspNetCore.Example
RUN dotnet restore

# Copy source and publish
WORKDIR /src
COPY examples/qyl.AspNetCore.Example/ examples/qyl.AspNetCore.Example/
WORKDIR /src/examples/qyl.AspNetCore.Example
RUN dotnet publish -c Release -o /app --no-restore

# ============================================================================
# STAGE 2: Runtime
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app
COPY --from=build /app .

# Create non-root user
RUN addgroup -S qyl && adduser -S qyl -G qyl && chown -R qyl:qyl /app
USER qyl

ENV ASPNETCORE_URLS=http://+:5050

EXPOSE 5050

ENTRYPOINT ["dotnet", "qyl.AspNetCore.Example.dll"]
