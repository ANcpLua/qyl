# Frontend

> Dashboard (React) + MCP Server

## Dashboard

### Structure

```
src/qyl.dashboard/
├── src/
│   ├── main.tsx              # Entry
│   ├── App.tsx               # Router
│   ├── components/           # UI components
│   ├── hooks/
│   │   └── useSse.ts         # SSE hook
│   ├── pages/                # Route pages
│   ├── lib/                  # Utilities
│   └── types/
│       ├── api.ts            # Manual types
│       ├── telemetry.ts
│       └── generated/        # Kiota output (DO NOT EDIT)
├── package.json
├── vite.config.ts
├── tailwind.config.ts
└── tsconfig.json
```

### Tech Stack

| Library | Version | Purpose |
|---------|---------|---------|
| React | 19 | UI framework |
| Vite | 6 | Build tool |
| TanStack Query | 5 | Data fetching + cache |
| TanStack Virtual | 3 | List virtualization |
| Tailwind CSS | 4 | Styling |
| Recharts | 2 | Charts |

### Data Fetching Pattern

```typescript
// REST via TanStack Query
export function useSessions(limit = 50) {
  return useQuery({
    queryKey: ['sessions', limit],
    queryFn: () => fetch(`/api/v1/sessions?limit=${limit}`)
      .then(r => r.json()),
    refetchInterval: 5000,
  });
}

// Real-time via SSE
export function useSpanStream() {
  const queryClient = useQueryClient();

  useEffect(() => {
    const es = new EventSource('/api/v1/events/spans');

    es.onmessage = () => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['spans'] });
    };

    es.onerror = () => {
      es.close();
      setTimeout(() => {/* reconnect */}, 3000);
    };

    return () => es.close();
  }, [queryClient]);
}
```

### Commands

```bash
npm install                    # Install deps
npm run dev                    # Dev server (:5173, proxies to collector)
npm run build                  # Production build
npm run test                   # Vitest
npm run lint                   # ESLint
```

---

## MCP Server

### Structure

```
src/qyl.mcp/
├── Program.cs                # Entry, MCP host
├── Client.cs                 # HTTP client to collector
└── Tools/
    ├── TelemetryTools.cs     # MCP tool definitions
    └── TelemetryJsonContext.cs  # AOT JSON
```

### Tools

| Tool | Description |
|------|-------------|
| `query_spans` | Query spans with filters |
| `get_session` | Get session by ID |
| `get_trace` | Get trace tree |
| `list_services` | List all services |
| `analyze_genai` | Analyze gen_ai usage |

### Communication

```
qyl.mcp ──HTTP──► qyl.collector
    │
    │ stdio (JSON-RPC)
    ▼
Claude / AI Agent
```

**Important**: MCP server talks to collector via HTTP only. No direct DB access.

### Configuration

```json
// Claude Desktop config
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/qyl.mcp"],
      "env": {
        "QYL_COLLECTOR_URL": "http://localhost:5100"
      }
    }
  }
}
```

### Run

```bash
# Development
dotnet run --project src/qyl.mcp

# As MCP server (stdio)
dotnet run --project src/qyl.mcp -- --stdio
```
