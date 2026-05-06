# qyl.dashboard - React Frontend
# Multi-stage build with nginx for production
#
# Build from repository root:
#   docker build -f services/qyl.dashboard/Dockerfile -t qyl-dashboard .

# ============================================================================
# STAGE 1: Build React App
# ============================================================================
FROM node:22-alpine@sha256:8ea2348b068a9544dae7317b4f3aafcdc032df1647bb7d768a05a5cad1a7683f AS build

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
FROM nginx:1.30-alpine@sha256:0272e4604ed93c1792f03695a033a6e8546840f86e0de20a884bb17d2c924883

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
