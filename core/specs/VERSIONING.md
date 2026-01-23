# TypeSpec versioning in qyl

qyl uses `@typespec/versioning` to record when schema elements were added or removed across
OpenTelemetry semantic convention versions. The annotations live in the TypeSpec "God Schema"
and act as the canonical history for schema evolution.

## Where it is used

- `core/specs/domains/ai/genai.tsp` for GenAI semantic conventions
- `core/specs/domains/data/db.tsp` for DB semantic conventions
- `core/specs/domains/transport/http.tsp` for HTTP semantic conventions
- `core/specs/api/routes.tsp` for API versioning

## Pattern: version registry + annotations

```tsp
import "@typespec/versioning";
using TypeSpec.Versioning;

@versioned(GenAiVersions)
namespace Qyl.Domains.AI.GenAi;

enum GenAiVersions {
  v1_27: "1.27.0",
  v1_28: "1.28.0",
  v1_29: "1.29.0",
  v1_37: "1.37.0",
  v1_38: "1.38.0",
  v1_39: "1.39.0",
}

model GenAiSpanAttributes {
  @encodedName("application/json", "gen_ai.system")
  @removed(GenAiVersions.v1_37)
  system?: string;

  @encodedName("application/json", "gen_ai.usage.prompt_tokens")
  @removed(GenAiVersions.v1_28)
  usagePromptTokens?: TokenCount;

  @encodedName("application/json", "gen_ai.usage.input_tokens")
  usageInputTokens?: TokenCount;

  @encodedName("application/json", "gen_ai.usage.input_tokens.cached")
  @added(GenAiVersions.v1_38)
  usageInputTokensCached?: TokenCount;
}
```

## Ingestion mapping (deprecated -> current)

qyl keeps ingestion backward-compatible by normalizing deprecated attribute keys. This mapping
is maintained in code and aligned with the TypeSpec history.

```csharp
public static readonly FrozenDictionary<string, string> DeprecatedMappings =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["gen_ai.system"] = "gen_ai.provider.name",
        ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
        ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",
        ["agents.tool.call_id"] = "gen_ai.tool.call.id",
        ["db.system"] = "db.system.name"
    }.ToFrozenDictionary(StringComparer.Ordinal);
```

Tests for the mappings live in `tests/qyl.collector.tests/Ingestion/SchemaNormalizerTests.cs`.

## Workflow for schema evolution

- Add a new version entry to the enum in the owning namespace.
- Mark additions and removals with `@added` and `@removed`.
- Update `SchemaNormalizer.DeprecatedMappings` to keep ingestion compatibility.
- Extend `SchemaNormalizerTests` to cover the new mapping.
- Run `nuke Generate` after TypeSpec changes.
