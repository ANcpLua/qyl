# qyl.dashboard - React Frontend
# Multi-stage build with nginx for production
#
# Build from repository root:
#   docker build -f services/qyl.dashboard/Dockerfile -t qyl-dashboard .

# ============================================================================
# STAGE 1: Build React App
# ============================================================================
FROM node:24-alpine@sha256:a0b9bf06e4e6193cf7a0f58816cc935ff8c2a908f81e6f1a95432d679c54fbfd AS build

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
FROM nginx:1.31-alpine@sha256:54f2a904c251d5a34adf545a72d32515a15e08418dae0266e23be2e18c66fefa

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
