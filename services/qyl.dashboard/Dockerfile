# qyl.dashboard - React Frontend
# Multi-stage build with nginx for production
#
# Build from repository root:
#   docker build -f services/qyl.dashboard/Dockerfile -t qyl-dashboard .

# ============================================================================
# STAGE 1: Build React App
# ============================================================================
FROM node:24-alpine@sha256:d1b3b4da11eefd5941e7f0b9cf17783fc99d9c6fc34884a665f40a06dbdfc94f AS build

WORKDIR /app

# Copy package files and install dependencies
COPY services/qyl.dashboard/package.json services/qyl.dashboard/package-lock.json ./
RUN npm ci

# Copy source and build
COPY services/qyl.dashboard/ ./
RUN npm run build

# ============================================================================
# STAGE 2: Production nginx
# ============================================================================
FROM nginx:1.30-alpine@sha256:9f00a7a92624975cdd964ec71fbb86bf91dc117e86436a9a91fa7b5cdb9615d1

# Labels for container registries
LABEL org.opencontainers.image.title="qyl.dashboard"
LABEL org.opencontainers.image.description="AI Observability Dashboard - React + Tailwind"
LABEL org.opencontainers.image.source="https://github.com/your-org/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# Copy built assets
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx template — Railway / PaaS hosts set $PORT at runtime; nginx:alpine's default
# entrypoint runs envsubst over /etc/nginx/templates/*.conf.template before starting, so
# the rendered /etc/nginx/conf.d/default.conf picks up $PORT live. Plain `docker run`
# falls back to the 80 default below.
COPY services/qyl.dashboard/nginx.conf.template /etc/nginx/templates/default.conf.template
ENV PORT=80

EXPOSE 80
