# qyl.dashboard - React Frontend
# Multi-stage build with nginx for production
#
# Build from repository root:
#   docker build -f src/qyl.dashboard/Dockerfile -t qyl-dashboard .

# ============================================================================
# STAGE 1: Build React App
# ============================================================================
FROM node:22-alpine AS build

WORKDIR /app

# Copy package files and install dependencies
COPY src/qyl.dashboard/package.json src/qyl.dashboard/package-lock.json ./
RUN npm ci

# Copy source and build
COPY src/qyl.dashboard/ ./
RUN npm run build

# ============================================================================
# STAGE 2: Production nginx
# ============================================================================
FROM nginx:1.27-alpine

# Labels for container registries
LABEL org.opencontainers.image.title="qyl.dashboard"
LABEL org.opencontainers.image.description="AI Observability Dashboard - React + Tailwind"
LABEL org.opencontainers.image.source="https://github.com/your-org/qyl"
LABEL org.opencontainers.image.licenses="MIT"

# Copy built assets
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx configuration
COPY src/qyl.dashboard/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
