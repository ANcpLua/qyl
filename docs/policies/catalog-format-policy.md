# Catalog Format Policy v1

<policy id="@catalog-format-v1" version="1.0" owner="ancplua" status="active"></policy>

## Trigger

Apply when request contains: catalog, registry, matrix, taxonomy, inventory, feature map, abstraction map.

## Required Artifact Set

1. `<slug>-catalog.csv`
2. `<slug>-abstractions.csv`
3. `<slug>-catalog.json`

Default output directory:
`/Users/ancplua/qyl/docs/catalogs`

## Filename Rules

- `slug` is lowercase kebab-case (example: `sentry`, `aspire13`, `openclaw`).
- Do not change filename pattern unless user asks.

## CSV Schema: catalog

File: `<slug>-catalog.csv`  
Header (exact order):
`id,feature_name,category,docs_url,source_url,abstraction_tags`

Rules:

- `id`: stable ID (example `SENTRY-001`)
- `abstraction_tags`: semicolon-separated (example `DetectionLoop;AlertLoop`)
- one row per atomic feature
- no merged/bucket rows

## CSV Schema: abstractions

File: `<slug>-abstractions.csv`  
Header (exact order):
`id,abstraction_name,intent,depends_on`

Rules:

- `depends_on`: semicolon-separated feature IDs
- one row per abstraction loop/model

## JSON Schema

File: `<slug>-catalog.json`  
Top-level keys (exact):

- `catalog_meta`
- `features`
- `abstractions`

`catalog_meta` keys:

- `name`
- `generated_on` (YYYY-MM-DD)
- `pinned_commit`
- `pinned_commit_url`

`features[]` keys:

- `id`
- `feature_name`
- `category`
- `docs_url`
- `source_url`
- `abstraction_tags` (array of strings)

`abstractions[]` keys:

- `id`
- `name`
- `intent`
- `depends_on` (array of feature IDs)

## Source-Link Rules

- If source links are requested, pin to one commit hash.
- Use file+line links in `source_url`.
- Use docs page links in `docs_url`.
- If source cannot be resolved, mark explicitly as inference in response text.

## Validation Checklist

- IDs contiguous and stable
- headers exact and ordered
- URLs valid
- JSON parseable
- no markdown-table-only output when artifact files are requested

## How to Refer to This Policy

Use this exact phrase:
`Use @catalog-format-v1.`

Optional stronger phrase:
`Use @catalog-format-v1 and produce all 3 artifacts (catalog.csv, abstractions.csv, catalog.json).`
