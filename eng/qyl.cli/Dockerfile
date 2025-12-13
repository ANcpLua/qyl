# qyl.cli - Development CLI Tool
#
# Build from eng/ directory:
#   docker build -f qyl.cli/Dockerfile -t qyl-cli .

# ============================================================================
# STAGE 1: Build
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copy project file and restore
COPY qyl.cli/qyl.cli.csproj qyl.cli/
RUN dotnet restore qyl.cli/qyl.cli.csproj

# Copy source and build
COPY qyl.cli/ qyl.cli/
WORKDIR /src/qyl.cli
RUN dotnet publish -c Release -o /app --no-restore

# ============================================================================
# STAGE 2: Runtime
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime

# Create non-root user
RUN addgroup -S qyl && adduser -S qyl -G qyl

WORKDIR /app
COPY --from=build /app .
RUN chown -R qyl:qyl /app

USER qyl

ENTRYPOINT ["dotnet", "qyl.cli.dll"]
