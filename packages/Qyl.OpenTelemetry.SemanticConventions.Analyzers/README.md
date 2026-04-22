# Qyl.OpenTelemetry.SemanticConventions.Analyzers

Roslyn analyzers and code fixes that enforce OpenTelemetry semantic
conventions v1.40.0 in C# code.

## Diagnostics

| Range                  | Kind        | What it flags                                                                 |
|------------------------|-------------|-------------------------------------------------------------------------------|
| `QYLSC0001`–`QYLSC0245` | Warning     | Each deprecated OTel attribute / metric / event / entity / enum member gets its own rule id so severity can be tuned per entry via `.editorconfig`. |
| `QYLSC002`             | Info        | Magic-string attribute id — suggests replacing with a typed constant.         |
| `QYLSC003A`            | Warning     | String literal that looks OTel-namespaced but isn't in the registry; offers a Levenshtein suggestion. |
| `QYLSC003B`            | Warning     | Unregistered `qyl.*` attribute — must be registered under `eng/semconv/qyl/model`. |

## Code fixes

The deprecated-attribute fixer dispatches on `replacement_mode` from the
upstream OTel registry:

- **Direct / FieldMapping / Integrate** — 1:1 literal replacement with the
  registered target.
- **Alternative** — one code action per candidate replacement.
- **Removed** — strips the enclosing statement with a TODO note.
- **Composite / Conditional / Contextual / Example / NoteOnly** — no auto-fix;
  diagnostic fires and the engineer resolves manually.

## Tuning severity

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.QYLSC0001.severity = error    # fail the build on android.state
dotnet_diagnostic.QYLSC0047.severity = none     # tolerate db.jdbc.driver_classname usage
```

## Regenerating

```bash
python3 eng/semconv/tools/gen-deprecated-diagnostics/gen.py \
    --yaml eng/semconv/deprecated-lookup/master-programmatic.yaml \
    --out  packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/Model/DeprecatedDiagnostics.g.cs
```
