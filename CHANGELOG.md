# Changelog

## 2026-02

- fix: resolve CI build failures
- refactor: migrate domains/ to models/, wire GenerateSemconv

## 2026-01

- fix: ProtobufReader position not advancing past sub-messages (#22)
- feat: qyl.watchdog — EMA-based process anomaly detection with launchd auto-start
- feat: insights materializer — auto-generated system context from telemetry
- feat: AG-UI backend tool rendering, SDK bump
- feat: W3C Baggage + Schema URL support, TypeSpec cleanup (#25)
- chore: relocate semconv generator, remove obsolete config

## 2025-12

- fix: replace banned time APIs with TimeProvider
- fix(docker): writable directories for non-root user, Railway compatibility
- feat(generator): Gauge metric support
- feat: analyzers — qyl.Analyzers with 15 diagnostic rules (QYL001-015)
- feat: complete P1+P2 roadmap with 22 parallel agents
- chore: upgrade SDKs, fix corrupted code fix provider
- feat: use SDK features for generator, remove manual polyfills
- feat: publish ANcpLua.NET.Sdk 1.0.0 to NuGet
- refactor: build pipeline restructure (eng/build/)

## 2025-11

- refactor: complete TypeSpec god schema migration
- feat: vertical slice ADRs, tests, semconv normalization
- feat: unified qyl.providers with Gemini, OpenAI, Ollama
- fix: CPM configuration, DuckDB API fixes, namespace corrections
- feat: AgentsGateway example with A2A support

## 2025-10

- Initial commit: qyl AI observability platform
