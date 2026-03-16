# Contracts Specification

Shared types consumed by all qyl projects. Generated from TypeSpec. BCL-only — zero NuGet packages.

---

## Table of Contents

1. [Overview](#1-overview)
2. [TypeSpec Models](#2-typespec-models)
3. [Enums and Attributes](#3-enums-and-attributes)
4. [Constraints](#4-constraints)
5. [Definition of Done](#5-definition-of-done)

---

## 1. Overview

`src/qyl.contracts/` — class library containing models, enums, and primitives shared across collector, MCP, agents, workflows, and loom.

Source of truth: `core/specs/*.tsp` → `tsp compile` → C# models in `qyl.contracts`.

## 2. TypeSpec Models

TypeSpec definitions in `core/specs/` generate:

- C# record types (models, DTOs)
- Enums (severity levels, span kinds, status codes)
- Copilot types (`CopilotTypes.cs`)
- Paged result wrapper (`PagedResult.cs`)

Generation flow:

```text
core/specs/main.tsp
  → tsp compile
  → qyl.contracts/Models/
  → qyl.contracts/Copilot/
```

Do not edit generated files directly. Modify the TypeSpec source and regenerate.

## 3. Enums and Attributes

Semconv attribute constants generated from upstream OTel YAML:

```text
eng/semconv/generate-semconv.ts
  → reads upstream semconv YAML
  → generates core/specs/generated/semconv.g.tsp
  → tsp compile
  → qyl.contracts attribute constants
```

## 4. Constraints

- Zero NuGet packages. BCL-only. This is non-negotiable.
- No version field on types currently. Known gap.
- `recommendation` in issue payloads is a plain string, not a structured ID. Known gap.
- Risk fields are untyped strings. Known gap.
- `TimeConversions` in `Primitives/` handles all timestamp normalization.

## 5. Definition of Done

- [ ] All model types generated from TypeSpec, not hand-written
- [ ] Zero PackageReference in qyl.contracts.csproj
- [ ] All downstream projects compile against contracts without additional package dependencies
- [ ] Semconv attributes match OTel Semantic Conventions 1.40
