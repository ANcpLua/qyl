# qyl

[![Railway Deploy](https://img.shields.io/badge/railway-deployed-success)](https://qyl-api-production.up.railway.app/)
[![Docker Image](https://img.shields.io/badge/docker-ghcr.io%2Fancplua%2Fqyl-blue)](https://github.com/ancplua/qyl/pkgs/container/qyl)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Question Your Logs** — AI Observability Platform für OpenTelemetry GenAI-Daten

Sammelt Telemetrie von AI Agent-Systemen: Token-Nutzung, Latenz, Errors und Kosten über alle AI-Workloads hinweg.

## 🚀 Quick Start

```bash
# Docker (empfohlen)
docker run -d -p 5100:5100 -p 4317:4317 -v ~/.qyl:/data ghcr.io/ancplua/qyl:latest

# .NET Global Tool
dotnet tool install -g qyl && qyl start
```

Dashboard öffnen: http://localhost:5100

## 📚 Dokumentation

Vollständige Dokumentation, API-Referenz, Beispiele und Architektur-Details:

**[→ ancplua.mintlify.app](https://ancplua.mintlify.app/)**

## 🌐 Live Demo

Produktions-Deployment auf Railway:

**[→ qyl-api-production.up.railway.app](https://qyl-api-production.up.railway.app/)**

## 📦 Installation & Konfiguration

Alle Details zu Installation, OTLP-Konfiguration, MCP-Integration und Entwicklung finden Sie in der [Dokumentation](https://ancplua.mintlify.app/).

## License

MIT
