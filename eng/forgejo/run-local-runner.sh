#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUNNER_ROOT="${ROOT_DIR}/artifacts/forgejo-runner"
DATA_DIR="${RUNNER_ROOT}/data"
SECRETS_DIR="${DATA_DIR}/secrets"
TOKEN_FILE="${SECRETS_DIR}/token"
CONFIG_FILE="${DATA_DIR}/runner-config.yml"
COMPOSE_FILE="${RUNNER_ROOT}/docker-compose.yml"
FORGEJO_URL="${FORGEJO_URL:-https://v15.next.forgejo.org/}"
FORGEJO_URL="${FORGEJO_URL%/}/"
RUNNER_CAPACITY="${QYL_FORGEJO_RUNNER_CAPACITY:-4}"
if ! [[ "${RUNNER_CAPACITY}" =~ ^[1-9][0-9]*$ ]]; then
  echo "QYL_FORGEJO_RUNNER_CAPACITY must be a positive integer." >&2
  exit 2
fi

if ! [[ "${FORGEJO_URL}" =~ ^https?://[^/]+/$ ]]; then
  echo "FORGEJO_URL must be an http(s) URL with a non-empty host and a trailing slash." >&2
  exit 2
fi

if [[ -z "${FORGEJO_RUNNER_UUID:-}" ]]; then
  echo "FORGEJO_RUNNER_UUID is required. Create a repository-scoped runner in Forgejo and export its UUID." >&2
  exit 2
fi

if ! [[ "${FORGEJO_RUNNER_UUID}" =~ ^[A-Za-z0-9_-]+$ ]]; then
  echo "FORGEJO_RUNNER_UUID may only contain alphanumerics, underscores, and hyphens." >&2
  exit 2
fi

mkdir -p "${SECRETS_DIR}"
chmod 700 "${SECRETS_DIR}"

if [[ -n "${FORGEJO_RUNNER_TOKEN:-}" ]]; then
  umask 077
  printf '%s' "${FORGEJO_RUNNER_TOKEN}" > "${TOKEN_FILE}"
fi

if [[ ! -s "${TOKEN_FILE}" ]]; then
  echo "Runner token is missing. Put it in ${TOKEN_FILE} or export FORGEJO_RUNNER_TOKEN for this command." >&2
  exit 2
fi
chmod 600 "${TOKEN_FILE}"

cat > "${CONFIG_FILE}" <<YAML
log:
  level: info
  job_level: info

runner:
  file: /data/.runner
  capacity: ${RUNNER_CAPACITY}
  timeout: 3h
  shutdown_timeout: 30m
  labels:
    - docker:docker://docker.io/library/node:lts
    - ubuntu-latest:docker://docker.io/library/node:lts

cache:
  enabled: true

container:
  docker_host: "-"
  valid_volumes: []

server:
  connections:
    qyl:
      url: "${FORGEJO_URL}"
      uuid: "${FORGEJO_RUNNER_UUID}"
      token_url: file:/data/secrets/token
YAML

cat > "${COMPOSE_FILE}" <<'YAML'
services:
  docker-in-docker:
    image: docker.io/library/docker:28-dind@sha256:2a232a42256f70d78e3cc5d2b5d6b3276710a0de0596c145f627ecfae90282ac
    privileged: true
    command: ['dockerd', '-H', 'tcp://0.0.0.0:2375', '--tls=false']
    restart: unless-stopped

  runner:
    image: data.forgejo.org/forgejo/runner:12@sha256:3d49075f9115054ae2485d8cea2819296a904dfd4f00017285168028615d8533
    depends_on:
      docker-in-docker:
        condition: service_started
    environment:
      DOCKER_HOST: tcp://docker-in-docker:2375
    volumes:
      - ./data:/data
    restart: unless-stopped
    command: forgejo-runner daemon --config /data/runner-config.yml
YAML

docker compose -f "${COMPOSE_FILE}" up -d
docker compose -f "${COMPOSE_FILE}" ps
