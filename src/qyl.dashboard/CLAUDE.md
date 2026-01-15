# qyl.dashboard

React 19 SPA for observability visualization. Build artifact embedded in collector.

## identity

```yaml
name: qyl.dashboard
type: react-spa
runtime: node-22
build-tool: vite-6
role: build-artifact
standalone: never
```

## build-contract

```yaml
input: src/
output: dist/
command: npm run build

output-structure:
  - dist/index.html
  - dist/assets/index-[hash].js
  - dist/assets/index-[hash].css

destination: ../qyl.collector/wwwroot/
copy-by: nuke (DashboardEmbed target)
```

## development

```yaml
command: npm run dev
port: 5173
hot-reload: true

proxy:
  /api: http://localhost:5100
  /v1: http://localhost:5100
  
requires: collector running on :5100
```

## type-generation

```yaml
source: ../../core/openapi/openapi.yaml
output: src/types/api.ts
command: npm run generate:types
tool: openapi-typescript

rule: never-edit-api.ts-manually
```

## tech-stack

```yaml
framework:
  react: "19"
  typescript: "5.7"
  
build:
  vite: "6"
  
styling:
  tailwind: "4"
  
state:
  tanstack-query: "5"
  
components:
  radix-ui: latest
  lucide-react: icons
  recharts: charts
```

## patterns

```yaml
data-fetching:
  library: tanstack-query
  pattern: |
    export function useSession(id: string) {
      return useQuery({
        queryKey: ['session', id],
        queryFn: () => api.getSession(id),
      });
    }

sse-streaming:
  pattern: |
    useEffect(() => {
      const es = new EventSource('/api/v1/live');
      es.onmessage = (e) => {
        const span = JSON.parse(e.data);
        queryClient.setQueryData(['spans'], old => [...old, span]);
      };
      return () => es.close();
    }, []);
```

## project-structure

```yaml
directories:
  - src/components/ui/        # Radix primitives
  - src/components/spans/     # Span visualization
  - src/components/sessions/  # Session views
  - src/components/layout/    # Shell, sidebar
  - src/hooks/                # TanStack Query hooks
  - src/lib/                  # Utilities, API client
  - src/pages/                # Route components
  - src/types/                # Generated types (api.ts)
```

## scripts

```yaml
dev: vite dev server with HMR
build: production build → dist/
generate:types: openapi → typescript
lint: eslint
typecheck: tsc --noEmit
```

## dependencies

```yaml
runtime:
  - react@19
  - react-dom@19
  - @tanstack/react-query@5
  - @radix-ui/*
  - recharts
  - tailwindcss@4
  - lucide-react

dev:
  - vite@6
  - typescript@5.7
  - openapi-typescript
  - eslint
  - @vitejs/plugin-react
```

## forbidden

```yaml
actions:
  - edit src/types/api.ts manually
  - add qyl.collector as npm dependency
  - run standalone in production
  - use fetch() directly (use tanstack-query)
  - import anything from .NET projects
```
