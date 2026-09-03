# syntax=docker/dockerfile:1@sha256:ecfaec9ed6d810b56388c508f4121597bfbba70d41a6dfeee4d8cad5f295fc32

FROM node:24-slim@sha256:ba849c60be29959425b8734d57b8b4b7d56f98edd9504c9af091d5281095a71e AS dashboard

WORKDIR /dashboard

COPY services/qyl.dashboard/package.json services/qyl.dashboard/package-lock.json ./
RUN npm ci --prefer-offline

COPY services/qyl.dashboard/ ./
RUN npm run build && \
    test -f ./dist/index.html || (echo "ERROR: Dashboard build failed - no index.html" && exit 1)

# DuckDB.NET ships glibc native assets; the AOT SDK supplies clang and zlib for ILC.
FROM mcr.microsoft.com/dotnet/sdk:10.0.400-noble-aot@sha256:74edbceabfc6d1a7e4a5764be034fd744a07f60d26703232e74f6a5edc04e8ba AS build

WORKDIR /src

COPY . .

# The dashboard build lands where the csproj embed gate expects it: a Release publish embeds
# dist/** into the assembly (QylEmbedDashboard), and the publish fails loudly if dist is missing.
COPY --from=dashboard /dashboard/dist services/qyl.dashboard/dist/

RUN eng/scripts/publish-collector-aot.sh /app

RUN test -x /app/qyl.collector \
    && test ! -e /app/qyl.collector.dll \
    && test -f /app/libduckdb.so \
    || (echo "ERROR: Native AOT publish did not produce the expected collector artifacts" && exit 1)

RUN set -e; \
    command -v strings >/dev/null; \
    if strings /app/qyl.collector | grep -Eq 'hostfxr|DOTNET_ROOT_'; then \
        echo "ERROR: Collector binary references the .NET host and is not Native AOT"; \
        exit 1; \
    fi

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0@sha256:851e6e04fff95d33a02c725373dc0ac624734986f2dc9028887077347ecffa96 AS runtime

LABEL org.opencontainers.image.title="qyl.collector"
LABEL org.opencontainers.image.description="qyl Telemetry Collector API"
LABEL org.opencontainers.image.source="https://github.com/ANcpLua/qyl"
LABEL org.opencontainers.image.licenses="MIT"

RUN apt-get update && apt-get install -y --no-install-recommends curl util-linux && \
    rm -rf /var/lib/apt/lists/* && \
    groupadd --system qyl && useradd --system --gid qyl qyl && \
    mkdir -p /app /data && chown qyl:qyl /app /data && \
    command -v setpriv >/dev/null

WORKDIR /app

COPY --from=build --chown=qyl:qyl /app .
COPY --chmod=0555 services/qyl.collector/container-entrypoint.sh /usr/local/bin/qyl-entrypoint

EXPOSE 5100 4317 4318

ENV QYL_BIND_ADDRESS=0.0.0.0 \
    QYL_GRPC_PORT=4317 \
    QYL_OTLP_PORT=4318 \
    QYL_DATA_PATH=/data/qyl.duckdb \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD curl -sf "http://localhost:${PORT:-5100}/health" || exit 1

ENTRYPOINT ["/usr/local/bin/qyl-entrypoint"]
