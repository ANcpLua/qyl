# qyl.dashboard - React Frontend
# Multi-stage build with nginx for production
#
# Build from repository root:
#   docker build -f services/qyl.dashboard/Dockerfile -t qyl-dashboard .

# ============================================================================
# STAGE 1: Build React App
# ============================================================================
FROM node:24-alpine@sha256:f1f2c6ee5140c8a7e94d379a40a9467395be641716126f65aea28aff35dbb196 AS build

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
FROM nginx:1.30-alpine@sha256:0d3b80406a13a767339fbe2f41406d6c7da727ab89cf8fae399e81f780f814d1

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
