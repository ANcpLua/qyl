# Feature: Structured Logs Filtering

> **Status:** Ready
> **Effort:** ~2h
> **Backend:** Yes (new endpoint)
> **Priority:** P1

---

## Problem

LogsPage exists but has no filtering UI. Users cannot filter logs by resource, level, or message content. The backend doesn't have a logs query endpoint.

## Solution

Add backend logs endpoint with filtering, and frontend filter UI with level dropdown, resource selector, and advanced filter dialog.

---

## Context

### Dashboard Location
```
/Users/ancplua/qyl/src/qyl.dashboard/
```

### Collector Location
```
/Users/ancplua/qyl/src/qyl.collector/
```

### Stack (DO NOT CHANGE)
| Tech | Version | Notes |
|------|---------|-------|
| React | 19.2.0 | No forwardRef, hooks only |
| TypeScript | 5.9.3 | Strict mode |
| Tailwind | 4.1.17 | `cn()` helper |
| TanStack Query | 5.90.11 | `telemetryKeys` factory |
| Lucide | 0.555.0 | Icons |
| Sonner | 2.0.7 | Toasts |
| Radix UI | Latest | Select, Dialog primitives |

### Existing Types
```typescript
// From src/types/telemetry.ts - adapt as needed
interface LogRecord {
  timestamp: string;
  severityText: string;
  severityNumber: number;
  body: string;
  attributes: Record<string, unknown>;
  traceId?: string;
  spanId?: string;
  resourceAttributes?: Record<string, unknown>;
}
```

---

## Files

| File | Action | What |
|------|--------|------|
| `src/qyl.collector/Query/LogsEndpoints.cs` | Create | Logs query endpoint |
| `src/qyl.collector/Program.cs` | Modify | Register logs endpoints |
| `src/types/telemetry.ts` | Modify | Add LogRecord, LogListResponse types |
| `src/hooks/use-telemetry.ts` | Modify | Add useLogs hook |
| `src/components/ui/log-filter.tsx` | Create | Filter toolbar component |
| `src/pages/LogsPage.tsx` | Modify | Add filtering UI |

---

## Implementation

### Step 1: Add Log Types

**File:** `src/types/telemetry.ts`

```typescript
// Add to existing types file

export interface LogRecord {
  timestamp: string;
  severity_text: string;
  severity_number: number;
  body: string;
  attributes: Record<string, unknown>;
  trace_id?: string;
  span_id?: string;
  resource_name?: string;
}

export interface LogListResponse {
  logs: LogRecord[];
  total: number;
  has_more: boolean;
}

export interface LogFilter {
  resource?: string;
  level?: string;
  search?: string;
  from?: string;
  to?: string;
}

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';
```

### Step 2: Add Logs Hook

**File:** `src/hooks/use-telemetry.ts`

```typescript
// Add to existing hooks file

import type { LogListResponse, LogFilter } from '@/types';

// Add to telemetryKeys
export const telemetryKeys = {
  // ... existing keys
  logs: (filter?: LogFilter) => ['telemetry', 'logs', filter] as const,
};

// Add hook
export function useLogs(filter?: LogFilter) {
  const params = new URLSearchParams();
  if (filter?.resource) params.set('resource', filter.resource);
  if (filter?.level) params.set('level', filter.level);
  if (filter?.search) params.set('search', filter.search);
  if (filter?.from) params.set('from', filter.from);
  if (filter?.to) params.set('to', filter.to);

  const queryString = params.toString();
  const url = `/api/v1/logs${queryString ? `?${queryString}` : ''}`;

  return useQuery({
    queryKey: telemetryKeys.logs(filter),
    queryFn: () => fetchJson<LogListResponse>(url),
    select: (data) => data.logs,
    refetchInterval: 5000,
  });
}
```

### Step 3: Create Log Filter Component

**File:** `src/components/ui/log-filter.tsx`

```tsx
import * as React from "react";
import { Filter, X, Search } from "lucide-react";
import { Button } from "./button";
import { Input } from "./input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./select";
import { Badge } from "./badge";
import { cn } from "@/lib/utils";
import type { LogFilter, LogLevel } from "@/types";

const LOG_LEVELS: { value: LogLevel | "all"; label: string; color: string }[] = [
  { value: "all", label: "All Levels", color: "" },
  { value: "trace", label: "Trace", color: "bg-slate-500" },
  { value: "debug", label: "Debug", color: "bg-blue-500" },
  { value: "info", label: "Info", color: "bg-green-500" },
  { value: "warn", label: "Warning", color: "bg-yellow-500" },
  { value: "error", label: "Error", color: "bg-red-500" },
  { value: "fatal", label: "Fatal", color: "bg-purple-500" },
];

interface LogFilterBarProps {
  filter: LogFilter;
  onFilterChange: (filter: LogFilter) => void;
  resources?: string[];
  className?: string;
}

export function LogFilterBar({
  filter,
  onFilterChange,
  resources = [],
  className,
}: LogFilterBarProps) {
  const [search, setSearch] = React.useState(filter.search || "");

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onFilterChange({ ...filter, search: search || undefined });
  };

  const clearFilter = (key: keyof LogFilter) => {
    const newFilter = { ...filter };
    delete newFilter[key];
    if (key === "search") setSearch("");
    onFilterChange(newFilter);
  };

  const activeFilters = Object.entries(filter).filter(
    ([_, v]) => v !== undefined
  );

  return (
    <div className={cn("space-y-2", className)}>
      {/* Filter Row */}
      <div className="flex items-center gap-2 flex-wrap">
        {/* Level Select */}
        <Select
          value={filter.level || "all"}
          onValueChange={(v) =>
            onFilterChange({
              ...filter,
              level: v === "all" ? undefined : v,
            })
          }
        >
          <SelectTrigger className="w-[140px]">
            <SelectValue placeholder="Level" />
          </SelectTrigger>
          <SelectContent>
            {LOG_LEVELS.map((level) => (
              <SelectItem key={level.value} value={level.value}>
                <div className="flex items-center gap-2">
                  {level.color && (
                    <span
                      className={cn("h-2 w-2 rounded-full", level.color)}
                    />
                  )}
                  {level.label}
                </div>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {/* Resource Select */}
        {resources.length > 0 && (
          <Select
            value={filter.resource || "all"}
            onValueChange={(v) =>
              onFilterChange({
                ...filter,
                resource: v === "all" ? undefined : v,
              })
            }
          >
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="Resource" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Resources</SelectItem>
              {resources.map((r) => (
                <SelectItem key={r} value={r}>
                  {r}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}

        {/* Search */}
        <form onSubmit={handleSearchSubmit} className="flex-1 min-w-[200px]">
          <div className="relative">
            <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search logs..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-8"
            />
          </div>
        </form>

        {/* Filter Button (for advanced - future) */}
        <Button variant="outline" size="icon" disabled title="Advanced filters">
          <Filter className="h-4 w-4" />
        </Button>
      </div>

      {/* Active Filters */}
      {activeFilters.length > 0 && (
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs text-muted-foreground">Active:</span>
          {activeFilters.map(([key, value]) => (
            <Badge
              key={key}
              variant="secondary"
              className="gap-1 pr-1"
            >
              {key}: {String(value)}
              <button
                onClick={() => clearFilter(key as keyof LogFilter)}
                className="ml-1 rounded-full hover:bg-muted p-0.5"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          <Button
            variant="ghost"
            size="sm"
            className="h-6 text-xs"
            onClick={() => {
              setSearch("");
              onFilterChange({});
            }}
          >
            Clear all
          </Button>
        </div>
      )}
    </div>
  );
}
```

### Step 4: Export Component

**File:** `src/components/ui/index.ts`

```typescript
// Add export
export { LogFilterBar } from "./log-filter";
```

### Step 5: Update LogsPage

**File:** `src/pages/LogsPage.tsx`

```tsx
import * as React from "react";
import { formatDistanceToNow } from "date-fns";
import { AlertCircle, AlertTriangle, Info, Bug, Skull } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { LogFilterBar } from "@/components/ui/log-filter";
import { useLogs, useSessions } from "@/hooks/use-telemetry";
import { cn } from "@/lib/utils";
import type { LogFilter, LogRecord } from "@/types";

const LEVEL_CONFIG: Record<string, { icon: typeof Info; color: string }> = {
  trace: { icon: Bug, color: "text-slate-500" },
  debug: { icon: Bug, color: "text-blue-500" },
  info: { icon: Info, color: "text-green-500" },
  warn: { icon: AlertTriangle, color: "text-yellow-500" },
  warning: { icon: AlertTriangle, color: "text-yellow-500" },
  error: { icon: AlertCircle, color: "text-red-500" },
  fatal: { icon: Skull, color: "text-purple-500" },
};

function LogLevelIcon({ level }: { level: string }) {
  const config = LEVEL_CONFIG[level.toLowerCase()] || LEVEL_CONFIG.info;
  const Icon = config.icon;
  return <Icon className={cn("h-4 w-4", config.color)} />;
}

function LogRow({ log }: { log: LogRecord }) {
  const [expanded, setExpanded] = React.useState(false);

  return (
    <div
      className="border-b last:border-b-0 py-2 px-3 hover:bg-muted/50 cursor-pointer"
      onClick={() => setExpanded(!expanded)}
    >
      <div className="flex items-start gap-3">
        <LogLevelIcon level={log.severity_text} />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 text-xs text-muted-foreground mb-1">
            <span>{new Date(log.timestamp).toLocaleTimeString()}</span>
            {log.resource_name && (
              <Badge variant="outline" className="text-xs">
                {log.resource_name}
              </Badge>
            )}
            <Badge
              variant="secondary"
              className={cn(
                "text-xs",
                LEVEL_CONFIG[log.severity_text.toLowerCase()]?.color
              )}
            >
              {log.severity_text}
            </Badge>
          </div>
          <p className="text-sm font-mono truncate">{log.body}</p>
        </div>
      </div>

      {expanded && (
        <div className="mt-2 ml-7 p-2 bg-muted/30 rounded text-xs font-mono">
          <div className="grid gap-1">
            {log.trace_id && (
              <div>
                <span className="text-muted-foreground">trace_id:</span>{" "}
                {log.trace_id}
              </div>
            )}
            {log.span_id && (
              <div>
                <span className="text-muted-foreground">span_id:</span>{" "}
                {log.span_id}
              </div>
            )}
            {Object.entries(log.attributes || {}).map(([k, v]) => (
              <div key={k}>
                <span className="text-muted-foreground">{k}:</span>{" "}
                {JSON.stringify(v)}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export function LogsPage() {
  const [filter, setFilter] = React.useState<LogFilter>({});
  const { data: logs, isLoading, error } = useLogs(filter);
  const { data: sessions } = useSessions();

  // Extract unique resources from sessions
  const resources = React.useMemo(() => {
    if (!sessions) return [];
    const names = new Set<string>();
    sessions.forEach((s) => s.services?.forEach((svc) => names.add(svc)));
    return Array.from(names).sort();
  }, [sessions]);

  return (
    <div className="flex flex-col h-full gap-4 p-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Structured Logs</h1>
      </div>

      <LogFilterBar
        filter={filter}
        onFilterChange={setFilter}
        resources={resources}
      />

      <Card className="flex-1 overflow-hidden">
        {isLoading ? (
          <div className="flex items-center justify-center h-full text-muted-foreground">
            Loading logs...
          </div>
        ) : error ? (
          <div className="flex items-center justify-center h-full text-red-500">
            Error loading logs
          </div>
        ) : !logs?.length ? (
          <div className="flex items-center justify-center h-full text-muted-foreground">
            No logs found
          </div>
        ) : (
          <ScrollArea className="h-full">
            {logs.map((log, i) => (
              <LogRow key={`${log.timestamp}-${i}`} log={log} />
            ))}
          </ScrollArea>
        )}
      </Card>
    </div>
  );
}
```

### Step 6: Backend - Logs Endpoint

**File:** `src/qyl.collector/Query/LogsEndpoints.cs`

```csharp
using System.Text.Json;

namespace qyl.collector.Query;

public static class LogsEndpoints
{
    public static IEndpointRouteBuilder MapLogsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/logs", async (
            DuckDbStore store,
            string? resource,
            string? level,
            string? search,
            int? limit) =>
        {
            var logs = await store.GetLogsAsync(
                resource: resource,
                level: level,
                search: search,
                limit: limit ?? 500
            ).ConfigureAwait(false);

            return Results.Ok(new
            {
                logs,
                total = logs.Count,
                has_more = logs.Count >= (limit ?? 500)
            });
        });

        return endpoints;
    }
}
```

### Step 7: Backend - Add to DuckDbStore

**File:** `src/qyl.collector/Storage/DuckDbStore.cs`

Add method:

```csharp
public async Task<List<LogRecordDto>> GetLogsAsync(
    string? resource = null,
    string? level = null,
    string? search = null,
    int limit = 500)
{
    var sql = new StringBuilder("SELECT * FROM logs WHERE 1=1");
    var parameters = new List<object>();

    if (!string.IsNullOrEmpty(resource))
    {
        sql.Append(" AND resource_name = ?");
        parameters.Add(resource);
    }

    if (!string.IsNullOrEmpty(level))
    {
        sql.Append(" AND LOWER(severity_text) = LOWER(?)");
        parameters.Add(level);
    }

    if (!string.IsNullOrEmpty(search))
    {
        sql.Append(" AND body LIKE ?");
        parameters.Add($"%{search}%");
    }

    sql.Append(" ORDER BY timestamp DESC LIMIT ?");
    parameters.Add(limit);

    // Execute query and map to DTOs
    // Implementation depends on your DuckDB wrapper
    return await ExecuteQueryAsync<LogRecordDto>(sql.ToString(), parameters.ToArray())
        .ConfigureAwait(false);
}
```

### Step 8: Register Endpoint in Program.cs

**File:** `src/qyl.collector/Program.cs`

Add after existing endpoint registrations:

```csharp
app.MapLogsEndpoints();
```

---

## Gotchas

- Level filtering is case-insensitive
- Search uses SQL LIKE with wildcards
- Log expansion uses local state, not React Query
- Resources come from sessions, not a separate endpoint
- `severity_text` vs `severityText` - use snake_case for API

---

## Test

```bash
cd /Users/ancplua/qyl/src/qyl.dashboard && npm run dev
```

- [ ] LogsPage renders with filter bar
- [ ] Level dropdown shows all levels
- [ ] Selecting level filters logs
- [ ] Resource dropdown populated from sessions
- [ ] Search filters by body text
- [ ] Active filters show as badges
- [ ] Clear all removes filters
- [ ] Clicking log row expands details
- [ ] No TS errors
- [ ] No console errors

---

*Template v3 - One prompt, one agent, done.*
