# docs.json Reference

## Minimal Configuration

```json
{
  "$schema": "https://mintlify.com/docs.json",
  "name": "My Docs",
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

## Full Configuration

```json
{
  "$schema": "https://mintlify.com/docs.json",
  "theme": "mint",
  "name": "Site Name",
  "colors": {
    "primary": "#0891B2",
    "light": "#06B6D4",
    "dark": "#0E7490"
  },
  "favicon": "/favicon.svg",
  "logo": {
    "light": "/logo/light.svg",
    "dark": "/logo/dark.svg"
  },
  "navigation": {
    "tabs": [
      {
        "tab": "Documentation",
        "groups": [
          {
            "group": "Getting Started",
            "pages": ["index", "quickstart"]
          },
          {
            "group": "Features",
            "pages": [
              "features/overview",
              {
                "group": "Nested Group",
                "pages": [
                  "features/nested/page1",
                  "features/nested/page2"
                ]
              }
            ]
          }
        ]
      },
      {
        "tab": "API Reference",
        "groups": [
          {
            "group": "Overview",
            "pages": ["api-reference/introduction"]
          }
        ],
        "openapi": "/api-reference/openapi.yaml"
      }
    ],
    "global": {
      "anchors": [
        {
          "anchor": "GitHub",
          "href": "https://github.com/org/repo",
          "icon": "github"
        }
      ]
    }
  },
  "navbar": {
    "links": [
      {
        "label": "GitHub",
        "href": "https://github.com/org/repo"
      }
    ]
  },
  "footer": {
    "socials": {
      "github": "https://github.com/org"
    }
  },
  "contextual": {
    "options": ["copy", "view", "claude", "cursor"]
  }
}
```

## OpenAPI Integration

For interactive API playground:

```json
{
  "navigation": {
    "tabs": [
      {
        "tab": "API Reference",
        "groups": [
          {
            "group": "Endpoints",
            "pages": ["api-reference/introduction"]
          }
        ],
        "openapi": "/api-reference/openapi.yaml"
      }
    ]
  }
}
```

Mintlify will auto-generate pages for each endpoint in the OpenAPI spec.

### OpenAPI Requirements

- OpenAPI 3.0 or 3.1 format
- YAML or JSON
- Place in `api-reference/` directory
- Reference with absolute path `/api-reference/openapi.yaml`

## Icons

Common icons for navigation groups:

| Icon | Usage |
|------|-------|
| `cube` | Packages, SDKs |
| `shield-check` | Security, Analyzers |
| `code` | Code, APIs |
| `book` | Documentation |
| `rocket` | Getting Started |
| `gear` | Configuration |
| `wrench` | Tools |
| `puzzle-piece` | Extensions |

## Page References

Pages are referenced by their path without extension:

```
docs/
├── index.mdx          → "index"
├── quickstart.mdx     → "quickstart"
└── sdk/
    ├── overview.mdx   → "sdk/overview"
    └── config.mdx     → "sdk/config"
```
