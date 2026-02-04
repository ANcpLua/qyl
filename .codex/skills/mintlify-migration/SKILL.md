---
name: mintlify-migration
description: |
  Migrate documentation from DocFX, MkDocs, or plain Markdown to Mintlify format. Use when user mentions Mintlify, docs migration, or converting documentation sites.
---

## Source Metadata

```yaml
frontmatter:
  license: MIT
  metadata:
    author: ancplua
    version: "1.0"
  compatibility: Requires filesystem access and Mintlify CLI (npx mintlify)
```


# Mintlify Migration Skill

Migrate documentation from various formats to Mintlify's MDX-based documentation platform.

## When to Use

Activate this skill when the user wants to:
- Migrate from DocFX, MkDocs, Docusaurus, or plain Markdown to Mintlify
- Set up a new Mintlify documentation site
- Convert documentation syntax (callouts, tabs, accordions)
- Configure `docs.json` navigation

## Migration Workflow

### Phase 1: Analysis

1. **Identify source format** by checking for:
   - `docfx.json` → DocFX
   - `mkdocs.yml` → MkDocs
   - `docusaurus.config.js` → Docusaurus
   - Plain `.md` files → Generic Markdown

2. **Inventory content**:
   ```bash
   find <source-dir> -name "*.md" -type f | wc -l
   ```

3. **Identify conversion patterns** (see [references/patterns.md](references/patterns.md))

### Phase 2: Setup Target

1. **Create Mintlify structure**:
   ```
   docs/
   ├── docs.json          # Navigation config
   ├── index.mdx          # Landing page
   ├── favicon.svg
   └── logo/
       ├── light.svg
       └── dark.svg
   ```

2. **Initialize docs.json** (see [references/docs-json.md](references/docs-json.md))

### Phase 3: Convert Content

1. **Rename files**: `.md` → `.mdx`
2. **Convert callouts**:
   - DocFX `> [!NOTE]` → `<Note>`
   - DocFX `> [!WARNING]` → `<Warning>`
   - DocFX `> [!TIP]` → `<Tip>`
3. **Convert collapsibles**:
   - `<details>/<summary>` → `<Accordion>`
4. **Fix links**: Remove `.md` extensions from internal links
5. **Add frontmatter**: `title`, `description`, `icon`

### Phase 4: Validate

```bash
npx mintlify broken-links
```

## Quick Reference

### Mintlify Components

| Component | Usage |
|-----------|-------|
| `<Note>` | Info callout |
| `<Warning>` | Warning callout |
| `<Tip>` | Tip callout |
| `<Card>` | Clickable card |
| `<CardGroup>` | Grid of cards |
| `<Tabs>/<Tab>` | Tabbed content |
| `<Accordion>` | Collapsible section |
| `<CodeGroup>` | Multi-language code |

### Frontmatter Template

```yaml
---
title: Page Title
description: Brief description for SEO
icon: cube
sidebarTitle: Short Title
---
```

### docs.json Structure

```json
{
  "$schema": "https://mintlify.com/docs.json",
  "name": "Site Name",
  "navigation": {
    "tabs": [
      {
        "tab": "Documentation",
        "groups": [
          {
            "group": "Getting Started",
            "pages": ["index", "quickstart"]
          }
        ]
      }
    ]
  }
}
```

## Common Issues

| Issue | Solution |
|-------|----------|
| Broken links | Use absolute paths `/section/page` not relative `page.mdx` |
| Missing pages in nav | Add to `docs.json` navigation |
| OpenAPI not generating | Check `openapi` field in docs.json |
| Node version error | Mintlify requires Node < 25 |

## Files in This Skill

- [references/patterns.md](references/patterns.md) - Detailed conversion patterns
- [references/docs-json.md](references/docs-json.md) - Complete docs.json reference
- [scripts/convert.sh](scripts/convert.sh) - Batch conversion script
