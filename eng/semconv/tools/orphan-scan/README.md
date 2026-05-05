# orphan-scan

Finds attribute-shaped string literals in qyl C# code that do not match any
registered OTel semantic-convention id (upstream or qyl-custom).

## Usage

From the repo root:

```bash
python3 -m eng.semconv.tools.orphan-scan --repo-root . --report orphans.json
```

or, because the package lives outside a Python import root, the more robust:

```bash
python3 eng/semconv/tools/orphan-scan/__main__.py --repo-root . --report orphans.json
```

Flags:

| Flag              | Default                                                         |
|-------------------|-----------------------------------------------------------------|
| `--repo-root`     | `.`                                                             |
| `--registry-dir`  | repeatable; defaults to `.tools/semconv-upstream/model` and `eng/semconv/model/qyl` |
| `--scan-dir`      | repeatable; defaults to `services` and `packages`               |
| `--report`        | JSON output file (stdout if omitted)                            |
| `--limit`         | cap reported orphans                                            |

## Output

```json
{
  "repo_root": "/abs/path",
  "registry_sources": [".../registry/attributes.yaml", "..."],
  "registry_id_count": 1534,
  "orphan_count": 12,
  "orphans": [
    {
      "file": "services/qyl.mcp/Tools/Example.cs",
      "line": 42,
      "literal": "http.reqeust.method",
      "suggestions": [
        { "candidate": "http.request.method", "distance": 1 }
      ]
    }
  ]
}
```

## Rules

- Registry sources are the upstream OTel model YAMLs (`.tools/semconv-upstream`)
  plus any qyl-custom registry under `eng/semconv/model/qyl`. Missing dirs are
  silently skipped.
- Scanning only reports literals passed as the first argument to known
  tag-setter methods (`SetTag`, `AddTag`, `SetAttribute`, `AddAttribute`,
  `SetCustomProperty`, `SetBaggage`) that look like dotted attribute ids.
- Suggestions use a 4-distance Levenshtein window and share the literal's
  top-level prefix.
