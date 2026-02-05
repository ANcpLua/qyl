# Codex Skills Index

> IMPORTANT: Prefer skill-led reasoning over ad-hoc reasoning for any task matching a skill below.
> Fetch skill details from: .codex/skills/{skill-name}/SKILL.md
> Read the SKILL.md BEFORE executing any workflow. Each contains exact agent configs, phases, and output formats.

## Critical Workflow

1. Match task to a skill using the Decision Tree below
2. Read `.codex/skills/{skill-name}/SKILL.md` for full instructions
3. Follow the SKILL.md exactly - phases, agent types, output formats
4. For multi-agent skills: launch all agents for a phase in ONE message
5. Run verification (build + test) as final step

## Decision Tree

```
Bug/Fix?
├── P0 critical ─────────────────→ turbo-fix (16 agents)
├── P1/P2 standard ─────────────→ fix (8 agents, configurable)
└── From audit findings ────────→ fix-pipeline (7 agents)

Design decision?
├── Multiple valid approaches ──→ tournament (N competitors + judge)
├── Complex trade-offs ─────────→ deep-think (5 agents)
└── Architecture question ──────→ deep-think mode=architecture

Audit/Review?
├── Full codebase health ───────→ mega-swarm mode=full (12 agents)
├── Quick check ────────────────→ mega-swarm quick=true (6 agents)
├── Security-focused ───────────→ red-blue-review scope=security
├── Pre-release gate ───────────→ red-blue-review scope=full
├── Code review ────────────────→ code-review OR competitive-review
└── PR review ──────────────────→ review-pr OR pr-test-analyzer

Batch work?
└── Multiple similar items ─────→ batch-implement (1 per item)

New feature?
├── Need exploration first ─────→ brainstorming → feature-dev
├── Have a plan ────────────────→ executing-plans OR subagent-driven-development
├── Independent tasks ──────────→ dispatching-parallel-agents
└── Need isolation ─────────────→ using-git-worktrees

Writing code?
├── New feature/bugfix ─────────→ test-driven-development
├── Debugging ──────────────────→ systematic-debugging
├── Simplify/refactor ─────────→ code-simplifier OR codebase-cohesion
└── Cleanup ────────────────────→ cleanup-specialist

Finishing work?
├── Claiming done ──────────────→ verification-before-completion
├── Creating commit ────────────→ commit
├── Creating PR ────────────────→ commit-push-pr
└── Merging branch ─────────────→ finishing-a-development-branch

qyl platform?
├── Backend/collector ──────────→ qyl-collector
├── Frontend/dashboard ─────────→ qyl-dashboard
├── Build/infra ────────────────→ qyl-build
├── Cross-cutting ──────────────→ qyl-observability-specialist
└── Agent coordination ─────────→ qyl-coordination-docs

OpenTelemetry?
├── Semantic conventions ───────→ otel-expert
├── GenAI instrumentation ──────→ otel-genai-architect
├── Docs lookup ────────────────→ otel-guide OR docs-lookup
└── Sync upstream docs ─────────→ otel-librarian (via /otelwiki:sync)

.NET/MSBuild?
├── Build system ───────────────→ msbuild-expert
├── Test platform issues ───────→ dotnet-mtp-advisor
├── Architecture lint ──────────→ lint-dotnet
├── Type ownership ─────────────→ type-ownership
└── Slice validation ───────────→ slice-validate

ANcpLua ecosystem?
├── SDK (props/targets) ────────→ ancplua-sdk-specialist
├── Analyzers (AL00XX) ─────────→ ancplua-analyzers-specialist
├── ErrorOrX generator ─────────→ erroror-generator-specialist
├── ServiceDefaults ────────────→ servicedefaults-specialist
├── Documentation site ─────────→ ancplua-docs-generator
└── Cross-repo context ─────────→ ancplua-ecosystem
```

## Compressed Skills Index

```
WORKFLOW-ORCHESTRATION (multi-agent):
|turbo-fix: 16-agent fix, phases: analysis(6)->solutions(4)->implement(3)->verify
|fix: configurable fix, parallelism: standard(8)|maximum(16), modes: aggressive|balanced|conservative
|fix-pipeline: systematic fix, phases: analysis(3)->design(2)->implement(1)->verify
|tournament: competitive N agents, scoring: correctness(40)+elegance(25)+performance(20)+completeness(15)-penalties
|mega-swarm: audit, modes: full(12)|quick(6)|focused(8), 12 categories
|deep-think: reasoning, phases: understand(3)->synthesize(2)->recommend
|batch-implement: parallel impl per item, phases: pattern->implement(N)->review->verify
|red-blue-review: adversarial, phases: red-attack(3)->blue-defend(N)->red-reattack(N)
|agent-tribe-orchestrator: spawns parallel subagents for optimization
|agent-tribe-workflow: multi-agent optimization with parallel patterns
|swarm-audit: full|protocol|collector|mcp|dashboard|otel|tournament
|tournament-review: staged|collector|mcp|dashboard|protocol
|parallel-explore: multiple explore agents for broad questions
|dispatching-parallel-agents: 2+ independent tasks without shared state
|subagent-driven-development: execute plans with independent tasks

CODE-QUALITY:
|code-review: comprehensive security+style+performance+practices
|review: comprehensive code review on specified files/changes
|review-pr: PR review using specialized agents
|code-reviewer: bugs, logic errors, security, quality, conventions
|arch-reviewer: structural problems, dependency violations, SOLID
|impl-reviewer: code-level issues, banned APIs, version mismatches
|competitive-review: dispatch arch-reviewer + impl-reviewer in competition
|pr-test-analyzer: test coverage quality and completeness for PRs
|silent-failure-hunter: silent failures, inadequate error handling
|comment-analyzer: code comment accuracy and maintainability
|completion-integrity: blocks warning suppressions, commented tests
|verification-before-completion: run build/test before claiming done
|verification-subagent: blackbox full test suite validator
|autonomous-ci: local tests AND remote CI before completion

DEVELOPMENT:
|feature-dev: guided feature dev with codebase understanding
|code-architect: designs architectures from existing patterns
|code-explorer: traces execution paths, maps architecture layers
|code-simplifier: simplifies code for clarity and maintainability
|brainstorming: explore intent, requirements, design before impl
|writing-plans: spec/requirements for multi-step tasks
|write-plan: detailed implementation plan with bite-sized tasks
|execute-plan: execute in batches with review checkpoints
|executing-plans: written plan in separate session with checkpoints
|test-driven-development: TDD before writing implementation
|systematic-debugging: any bug/failure/unexpected behavior
|deep-debugger: race conditions, intermittent failures, perf regressions
|implement: tasks from track's plan following TDD
|improve: deep analysis for library adoption and refactoring
|codebase-improver: library adoption, cohesive refactoring
|codebase-cohesion: dead code, duplicates, unifying patterns
|cleanup-specialist: ruthless cleanup, zero suppressions/dead-code/duplication
|create-component: guided component creation with proper patterns
|finishing-a-development-branch: merge/PR/cleanup decision after tests pass

GIT-CI:
|commit: create git commit
|commit-push-pr: commit, push, open PR
|clean-gone: cleanup branches marked [gone]
|using-git-worktrees: isolated worktrees for feature work

QYL-PLATFORM:
|qyl-observability-specialist: OTLP ingestion, DuckDB, GenAI telemetry, MCP, TypeSpec
|qyl-collector: backend OTLP ingestion, DuckDB storage, REST API
|qyl-dashboard: React 19 SPA, real-time visualization
|qyl-build: NUKE, TypeSpec, Docker
|qyl-coordination-docs: agent collaboration guide

ANCPLUA:
|ancplua-sdk-specialist: MSBuild SDK props/targets, packages, polyfills, EditorConfig
|ancplua-analyzers-specialist: Roslyn diagnostics AL00XX, code fixes, perf
|ancplua-docs-generator: ANcpLua.io docs site, Mintlify content
|ancplua-ecosystem: cross-repo context across all ANcpLua repos
|erroror-generator-specialist: endpoint generation, parameter binding, Results<>, AOT
|servicedefaults-specialist: OTel instrumentation, GenAI telemetry, source generators

OPENTELEMETRY:
|otel-expert: semconv, instrumentation, collector config
|otel-genai-architect: AI/GenAI semantic conventions, traces, metrics, logs
|otel-guide: semconv questions, collector config, attribute lookup
|otel-librarian: sync upstream OTel docs, validate
|docs-lookup: OTel conventions + ANcpLua SDK docs in parallel
|observability-engineer: monitoring, logging, tracing systems, SLI/SLO
|performance-engineer: observability, optimization, scalable performance

DOTNET:
|msbuild-expert: project files, NuGet packaging, .targets/.props
|dotnet-mtp-advisor: xUnit v3, exit codes, filter syntax, VSTest migration
|lint-dotnet: architecture lint for MSBuild/CPM violations
|type-ownership: shared types in protocol, internal in owning project
|type-design-analyzer: type design analysis for new/modified types
|slice-validate: vertical slice completeness end-to-end
|xml-doc-generator: XML documentation comments for C# code

FRONTEND:
|frontend-design: distinctive production-grade interfaces
|frontend-developer: React 19, Next.js, modern frontend
|ui-designer: component creation, layout systems, visual design
|responsive-design: container queries, fluid typography, CSS Grid, mobile-first
|interaction-design: microinteractions, motion, transitions, feedback
|visual-design-foundations: typography, color theory, spacing, iconography
|web-component-design: React/Vue/Svelte component patterns
|design-system-architect: tokens, libraries, theming, scalable operations
|design-system-patterns: tokens, theming infrastructure, component architecture
|design-system-setup: initialize design system with tokens
|mobile-ios-design: HIG, SwiftUI patterns
|mobile-android-design: Material Design 3, Jetpack Compose
|react-native-design: cross-platform styling, navigation, Reanimated
|playground: interactive HTML playgrounds, single-file explorers

AI-ML:
|ai-engineer: LLM apps, RAG, agents, vector search, multimodal
|ai-assistant: NLU, dialog management, integrations
|prompt-engineer: CoT, few-shot, constitutional AI
|prompt-optimize: optimize prompts for production
|prompt-engineering-patterns: advanced prompting techniques
|langchain-agent: LangGraph-based agents
|langchain-architecture: LangChain 1.x, LangGraph, agents, memory
|llm-evaluation: eval strategies, metrics, benchmarking
|rag-implementation: vector databases, semantic search
|hybrid-search-implementation: vector + keyword search
|embedding-strategies: model selection, chunking strategies
|vector-database-engineer: Pinecone, Weaviate, Qdrant, Milvus, pgvector
|vector-index-tuning: HNSW parameters, quantization, recall optimization
|similarity-search-patterns: nearest neighbor, semantic search

BUSINESS:
|startup-analyst: market sizing, financial modeling, strategy
|startup-financial-modeling: 3-5yr financial projections
|startup-metrics-framework: SaaS metrics, CAC, LTV, unit economics
|business-case: investor-ready business case document
|financial-projections: revenue, costs, cash flow, scenarios
|competitive-landscape: competitor analysis, differentiation
|market-opportunity: TAM/SAM/SOM analysis
|market-sizing-analysis: market size calculations
|team-composition-analysis: team structure, hiring, org chart

DOCS-WRITING:
|mintlify-migration: DocFX/MkDocs/Markdown to Mintlify
|writing-clearly-and-concisely: Strunk's rules for clear prose
|writing-skills: creating/editing/verifying skills

META:
|metacognitive-guard: struggle signal monitoring, escalation
|epistemic-checkpoint: verify versions/dates/status before answering
|deep-think-partner: complex logical problems, collaborative reasoning
|using-superpowers: skill discovery and invocation protocol

ACCESSIBILITY:
|accessibility-audit: WCAG compliance audit
|accessibility-compliance: WCAG 2.2, inclusive design, assistive tech
|accessibility-expert: screen reader compatibility, inclusive design

BROWSER:
|browsing: Chrome DevTools Protocol, multi-tab, form automation
|browser-user: inspect cached content, analyze DOM

STRIPE:
|stripe-best-practices: payments, checkout, subscriptions, webhooks
|test-cards: Stripe test card numbers
|explain-error: Stripe error codes and solutions

MISC:
|cancel-ralph: cancel active Ralph Loop
|status: project status, active tracks, next actions
|setup: initialize project with Conductor artifacts
|new-track: new track with spec and phased plan
|sync: force sync OTel docs
```
