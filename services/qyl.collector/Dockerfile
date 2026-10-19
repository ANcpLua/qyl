FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble-aot@sha256:62a3ebde694544ae0790b5ca2ffd56db04298af5652b7295c88fd61097752664 AS build

WORKDIR /src

COPY . .

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

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0@sha256:12dd273c196e92aa91542749b8df0996f497f812811abba9e6613b484f681dd0 AS runtime

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
