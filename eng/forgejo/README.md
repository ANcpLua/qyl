# Forgejo Research Harness

This folder keeps the Forgejo-facing research and runner glue out of qyl runtime code.

## Research Corpus

Run:

```sh
node eng/forgejo/research-forgejo-docs.mjs
```

The script writes ignored local output under `artifacts/forgejo-research/`:

- `repos/` shallow clones of the current qyl Forgejo repository plus the canonical Forgejo docs, website, runner, and `code.forgejo.org/forgejo/forgejo` server repositories.
- `source-metadata/forgejo-v15-swagger.json` from `https://v15.next.forgejo.org/swagger.v1.json`.
- `corpus.ndjson`, `manifest.json`, `route-index.json`, and `summary-api-notes.md`.

The script uses `git pull --ff-only` for existing clones. It does not use `git reset`.

Useful live Forgejo v15 references:

- Repository API: `https://v15.next.forgejo.org/api/swagger#/repository`
- Miscellaneous API: `https://v15.next.forgejo.org/api/swagger#/miscellaneous`
- Admin and self-hosted runner API: `https://v15.next.forgejo.org/api/swagger#/admin`
- Machine-readable Swagger used by the script: `https://v15.next.forgejo.org/swagger.v1.json`

## Local Runner

Create one repository-scoped runner in Forgejo and keep the displayed token out of logs.

```sh
export FORGEJO_RUNNER_UUID="..."
export FORGEJO_RUNNER_TOKEN="..."
QYL_FORGEJO_RUNNER_CAPACITY=4 ./eng/forgejo/run-local-runner.sh
```

The script stores the token in `artifacts/forgejo-runner/data/secrets/token` with restrictive permissions and starts
the official `data.forgejo.org/forgejo/runner:12` container with Docker-in-Docker. It starts one runner process with
configurable capacity. Do not reuse one UUID/token across many runner containers; create separate Forgejo runner
registrations for separate runner identities.

The generated `runner-config.yml` sets `valid_volumes: []`, which forbids workflow-controlled bind mounts into job
containers. Job containers can still talk to the DinD daemon via the `DOCKER_HOST` env var declared in the compose
file. Loosen this only if you have a specific reason to expose host paths to untrusted workflow code.

## Workflow

`.forgejo/workflows/forgejo-doc-research.yml` runs the same corpus extraction daily (06:00 UTC) and on manual
dispatch. It expects a Forgejo runner with the `docker` label, matching the runner configuration above.
