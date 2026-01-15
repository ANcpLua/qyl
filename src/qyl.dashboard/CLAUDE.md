# qyl.dashboard

React 19 SPA. Build artifact embedded in collector.

## identity

```yaml
name: qyl.dashboard
type: react-spa
runtime: node-22
build: vite-7
role: build-artifact
standalone: never (embedded at build-time)
```

## build-contract

```yaml
command: npm run build
input: src/
output: dist/

structure:
  dist/:
    - index.html
    - assets/:
        - index-[hash].js
        - index-[hash].css
        
destination: ../qyl.collector/wwwroot/
copier: nuke DashboardEmbed
```

## development

```yaml
command: npm run dev
port: 5173
features:
  - hot-reload
  - api-proxy

vite-config: |
  export default defineConfig({
    server: {
      port: 5173,
      proxy: {
        '/api': 'http://localhost:5100',
        '/v1': 'http://localhost:5100',
      }
    },
    build: {
      outDir: 'dist',
      sourcemap: false
    }
  });

requires: collector running on :5100
```

## type-generation

```yaml
source: ../../core/openapi/openapi.yaml
output: src/types/api.ts
package: openapi-typescript@7.10.1
command: npm run generate:types

script: |
  "generate:types": "openapi-typescript ../core/openapi/openapi.yaml -o src/types/api.ts"

rule: never edit api.ts manually
```

## tech-stack

```yaml
dependencies:
  react: "19"
  react-dom: "19"
  "@tanstack/react-query": "5"
  "@radix-ui/react-*": latest
  recharts: latest
  lucide-react: latest
  tailwindcss: "4"
  clsx: latest
  
dev-dependencies:
  vite: "7"
  "@vitejs/plugin-react": latest
  typescript: "5.9"
  openapi-typescript: "7.10.1"
  eslint: latest
  postcss: latest
  autoprefixer: latest
```

## patterns

```yaml
data-fetching:
  library: "@tanstack/react-query"
  
  hook: |
    import { useQuery } from '@tanstack/react-query';
    import { api } from '@/lib/api';
    
    export function useSession(id: string) {
      return useQuery({
        queryKey: ['session', id],
        queryFn: () => api.getSession(id),
      });
    }
    
  mutation: |
    import { useMutation, useQueryClient } from '@tanstack/react-query';
    
    export function useDeleteSession() {
      const queryClient = useQueryClient();
      return useMutation({
        mutationFn: (id: string) => api.deleteSession(id),
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: ['sessions'] });
        },
      });
    }

sse-streaming:
  pattern: |
    import { useEffect } from 'react';
    import { useQueryClient } from '@tanstack/react-query';
    import type { SpanRecord } from '@/types/api';
    
    export function useLiveSpans() {
      const queryClient = useQueryClient();
      
      useEffect(() => {
        const es = new EventSource('/api/v1/live');
        
        es.onmessage = (event) => {
          const span: SpanRecord = JSON.parse(event.data);
          queryClient.setQueryData<SpanRecord[]>(
            ['spans', 'live'], 
            (old = []) => [...old.slice(-999), span]
          );
        };
        
        es.onerror = () => es.close();
        
        return () => es.close();
      }, [queryClient]);
    }

api-client:
  pattern: |
    import type { paths } from '@/types/api';
    
    const BASE_URL = '';
    
    export const api = {
      async getSession(id: string) {
        const res = await fetch(`${BASE_URL}/api/v1/sessions/${id}`);
        if (!res.ok) throw new Error('Failed to fetch session');
        return res.json() as Promise<paths['/api/v1/sessions/{id}']['get']['responses']['200']['content']['application/json']>;
      },
      // ...
    };
```

## project-structure

```yaml
src/:
  components/:
    ui/:           # Radix primitives, shadcn-style
    layout/:       # Shell, Sidebar, Header
    spans/:        # SpanList, SpanDetail, SpanWaterfall
    sessions/:     # SessionList, SessionDetail
    charts/:       # TokenUsage, LatencyChart
  hooks/:          # TanStack Query hooks
  lib/:
    api.ts         # API client
    utils.ts       # cn(), formatters
  pages/:          # Route components
  types/:
    api.ts         # Generated from OpenAPI
  App.tsx
  main.tsx
  index.css        # Tailwind
```

## scripts

```yaml
dev: vite (HMR)
build: vite build → dist/
preview: vite preview
generate:types: openapi-typescript
lint: eslint src/
typecheck: tsc --noEmit
```

## forbidden

```yaml
actions:
  - edit src/types/api.ts
  - add qyl.collector as dependency
  - run standalone in production
  - use raw fetch() (use TanStack Query)
  - import from .NET projects
```
