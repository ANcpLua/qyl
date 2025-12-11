# Feature: Trace Detail with Gantt Timeline

> **Status:** Ready
> **Effort:** ~2h
> **Backend:** No (endpoint exists)
> **Priority:** P1

---

## Problem

TracesPage shows trace list but clicking a trace has no detail view. Users cannot visualize span timing as a Gantt-style timeline or inspect span attributes.

## Solution

Add trace detail view with horizontal timeline visualization, span inspection panel, and attribute display. Navigate via clicking a trace row.

---

## Context

### Dashboard Location
```
/Users/ancplua/qyl/src/qyl.dashboard/
```

### Existing Endpoint
```
GET /api/v1/traces/{traceId}
Response: {
  trace_id: string,
  root_span_name: string,
  service_name: string,
  duration_ms: number,
  spans: Span[]
}
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
| Radix UI | Latest | Dialog, ScrollArea |

### Existing Types
```typescript
interface Span {
  trace_id: string;
  span_id: string;
  parent_span_id?: string;
  name: string;
  kind: string;
  start_time: string;
  end_time: string;
  duration_ms: number;
  status_code: string;
  attributes: Record<string, unknown>;
  events?: SpanEvent[];
}
```

---

## Files

| File | Action | What |
|------|--------|------|
| `src/components/traces/TraceTimeline.tsx` | Create | Gantt-style timeline |
| `src/components/traces/SpanDetail.tsx` | Create | Span inspector panel |
| `src/components/traces/index.ts` | Create | Barrel exports |
| `src/pages/TraceDetailPage.tsx` | Create | Full trace detail page |
| `src/pages/index.ts` | Modify | Add TraceDetailPage export |
| `src/App.tsx` | Modify | Add /traces/:traceId route |

---

## Implementation

### Step 1: Create Trace Timeline Component

**File:** `src/components/traces/TraceTimeline.tsx`

```tsx
import * as React from "react";
import { AlertCircle, CheckCircle2, Clock } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { Span } from "@/types";

interface TraceTimelineProps {
  spans: Span[];
  selectedSpanId?: string;
  onSelectSpan: (span: Span) => void;
  className?: string;
}

interface SpanWithDepth extends Span {
  depth: number;
}

function buildSpanTree(spans: Span[]): SpanWithDepth[] {
  // Build parent map
  const childrenMap = new Map<string | undefined, Span[]>();
  spans.forEach((span) => {
    const parentId = span.parent_span_id || undefined;
    if (!childrenMap.has(parentId)) {
      childrenMap.set(parentId, []);
    }
    childrenMap.get(parentId)!.push(span);
  });

  // DFS to assign depth
  const result: SpanWithDepth[] = [];
  function traverse(parentId: string | undefined, depth: number) {
    const children = childrenMap.get(parentId) || [];
    children
      .sort((a, b) => new Date(a.start_time).getTime() - new Date(b.start_time).getTime())
      .forEach((span) => {
        result.push({ ...span, depth });
        traverse(span.span_id, depth + 1);
      });
  }
  traverse(undefined, 0);

  return result;
}

function getStatusIcon(status: string) {
  switch (status.toLowerCase()) {
    case "error":
      return <AlertCircle className="h-3 w-3 text-red-500" />;
    case "ok":
      return <CheckCircle2 className="h-3 w-3 text-green-500" />;
    default:
      return <Clock className="h-3 w-3 text-muted-foreground" />;
  }
}

function SpanBar({
  span,
  traceStart,
  traceDuration,
  isSelected,
  onClick,
}: {
  span: SpanWithDepth;
  traceStart: number;
  traceDuration: number;
  isSelected: boolean;
  onClick: () => void;
}) {
  const spanStart = new Date(span.start_time).getTime();
  const leftPercent = ((spanStart - traceStart) / traceDuration) * 100;
  const widthPercent = Math.max((span.duration_ms / traceDuration) * 100, 0.5);

  const colors: Record<string, string> = {
    server: "bg-blue-500",
    client: "bg-green-500",
    producer: "bg-purple-500",
    consumer: "bg-orange-500",
    internal: "bg-slate-500",
  };

  const barColor = colors[span.kind.toLowerCase()] || "bg-primary";

  return (
    <div
      className={cn(
        "group flex items-center gap-2 py-1.5 px-2 cursor-pointer rounded hover:bg-muted/50",
        isSelected && "bg-muted"
      )}
      style={{ paddingLeft: `${span.depth * 16 + 8}px` }}
      onClick={onClick}
    >
      {/* Span Info */}
      <div className="flex items-center gap-1.5 w-[200px] shrink-0">
        {getStatusIcon(span.status_code)}
        <span className="text-xs font-mono truncate" title={span.name}>
          {span.name}
        </span>
      </div>

      {/* Duration */}
      <div className="w-[70px] shrink-0 text-right text-xs text-muted-foreground">
        {span.duration_ms.toFixed(1)}ms
      </div>

      {/* Timeline Bar */}
      <div className="flex-1 h-5 relative bg-muted/30 rounded">
        <div
          className={cn(
            "absolute h-full rounded transition-all",
            barColor,
            isSelected ? "opacity-100" : "opacity-70 group-hover:opacity-100"
          )}
          style={{
            left: `${leftPercent}%`,
            width: `${widthPercent}%`,
            minWidth: "4px",
          }}
        />
      </div>
    </div>
  );
}

export function TraceTimeline({
  spans,
  selectedSpanId,
  onSelectSpan,
  className,
}: TraceTimelineProps) {
  const sortedSpans = React.useMemo(() => buildSpanTree(spans), [spans]);

  const { traceStart, traceDuration } = React.useMemo(() => {
    if (spans.length === 0) return { traceStart: 0, traceDuration: 1 };

    const starts = spans.map((s) => new Date(s.start_time).getTime());
    const ends = spans.map((s) => new Date(s.end_time).getTime());
    const start = Math.min(...starts);
    const end = Math.max(...ends);

    return {
      traceStart: start,
      traceDuration: Math.max(end - start, 1),
    };
  }, [spans]);

  return (
    <div className={cn("space-y-0.5", className)}>
      {/* Header */}
      <div className="flex items-center gap-2 px-2 py-1 text-xs font-medium text-muted-foreground border-b">
        <div className="w-[200px] shrink-0">Span</div>
        <div className="w-[70px] shrink-0 text-right">Duration</div>
        <div className="flex-1">Timeline</div>
      </div>

      {/* Spans */}
      {sortedSpans.map((span) => (
        <SpanBar
          key={span.span_id}
          span={span}
          traceStart={traceStart}
          traceDuration={traceDuration}
          isSelected={span.span_id === selectedSpanId}
          onClick={() => onSelectSpan(span)}
        />
      ))}
    </div>
  );
}
```

### Step 2: Create Span Detail Component

**File:** `src/components/traces/SpanDetail.tsx`

```tsx
import * as React from "react";
import { X, Copy, Check, Clock, Tag, Activity } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import type { Span } from "@/types";

interface SpanDetailProps {
  span: Span;
  onClose: () => void;
  className?: string;
}

function CopyValue({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = React.useState(false);

  const copy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    toast.success(`${label} copied`);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <button
      onClick={copy}
      className="inline-flex items-center gap-1 text-xs font-mono bg-muted px-1.5 py-0.5 rounded hover:bg-muted/80"
    >
      {value.slice(0, 16)}...
      {copied ? (
        <Check className="h-3 w-3 text-green-500" />
      ) : (
        <Copy className="h-3 w-3 text-muted-foreground" />
      )}
    </button>
  );
}

function AttributeRow({ name, value }: { name: string; value: unknown }) {
  const stringValue = typeof value === "object"
    ? JSON.stringify(value)
    : String(value);

  return (
    <div className="flex gap-2 py-1 text-xs">
      <span className="text-muted-foreground shrink-0 w-[140px] truncate" title={name}>
        {name}
      </span>
      <span className="font-mono break-all">{stringValue}</span>
    </div>
  );
}

export function SpanDetail({ span, onClose, className }: SpanDetailProps) {
  const attributes = Object.entries(span.attributes || {});
  const events = span.events || [];

  return (
    <div className={cn("flex flex-col h-full border-l bg-background", className)}>
      {/* Header */}
      <div className="flex items-center justify-between p-3 border-b">
        <div className="flex items-center gap-2">
          <Activity className="h-4 w-4" />
          <span className="font-medium text-sm truncate max-w-[200px]" title={span.name}>
            {span.name}
          </span>
        </div>
        <Button variant="ghost" size="icon" className="h-6 w-6" onClick={onClose}>
          <X className="h-4 w-4" />
        </Button>
      </div>

      <ScrollArea className="flex-1">
        <div className="p-3 space-y-4">
          {/* IDs */}
          <div className="space-y-2">
            <h4 className="text-xs font-medium text-muted-foreground uppercase">IDs</h4>
            <div className="grid gap-1.5">
              <div className="flex items-center gap-2 text-xs">
                <span className="text-muted-foreground w-16">Trace</span>
                <CopyValue value={span.trace_id} label="Trace ID" />
              </div>
              <div className="flex items-center gap-2 text-xs">
                <span className="text-muted-foreground w-16">Span</span>
                <CopyValue value={span.span_id} label="Span ID" />
              </div>
              {span.parent_span_id && (
                <div className="flex items-center gap-2 text-xs">
                  <span className="text-muted-foreground w-16">Parent</span>
                  <CopyValue value={span.parent_span_id} label="Parent ID" />
                </div>
              )}
            </div>
          </div>

          <Separator />

          {/* Timing */}
          <div className="space-y-2">
            <h4 className="text-xs font-medium text-muted-foreground uppercase flex items-center gap-1">
              <Clock className="h-3 w-3" />
              Timing
            </h4>
            <div className="grid gap-1 text-xs">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Duration</span>
                <span className="font-mono">{span.duration_ms.toFixed(2)} ms</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Start</span>
                <span className="font-mono">
                  {new Date(span.start_time).toISOString()}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">End</span>
                <span className="font-mono">
                  {new Date(span.end_time).toISOString()}
                </span>
              </div>
            </div>
          </div>

          <Separator />

          {/* Status */}
          <div className="space-y-2">
            <h4 className="text-xs font-medium text-muted-foreground uppercase">Status</h4>
            <div className="flex gap-2">
              <Badge
                variant={span.status_code === "ERROR" ? "destructive" : "secondary"}
              >
                {span.status_code}
              </Badge>
              <Badge variant="outline">{span.kind}</Badge>
            </div>
          </div>

          {/* Attributes */}
          {attributes.length > 0 && (
            <>
              <Separator />
              <div className="space-y-2">
                <h4 className="text-xs font-medium text-muted-foreground uppercase flex items-center gap-1">
                  <Tag className="h-3 w-3" />
                  Attributes ({attributes.length})
                </h4>
                <div className="space-y-0.5">
                  {attributes.map(([key, value]) => (
                    <AttributeRow key={key} name={key} value={value} />
                  ))}
                </div>
              </div>
            </>
          )}

          {/* Events */}
          {events.length > 0 && (
            <>
              <Separator />
              <div className="space-y-2">
                <h4 className="text-xs font-medium text-muted-foreground uppercase">
                  Events ({events.length})
                </h4>
                <div className="space-y-2">
                  {events.map((event, i) => (
                    <div key={i} className="text-xs p-2 bg-muted/50 rounded">
                      <div className="font-medium">{event.name}</div>
                      <div className="text-muted-foreground">
                        {new Date(event.timestamp).toISOString()}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </>
          )}
        </div>
      </ScrollArea>
    </div>
  );
}
```

### Step 3: Create Barrel Export

**File:** `src/components/traces/index.ts`

```tsx
export { TraceTimeline } from "./TraceTimeline";
export { SpanDetail } from "./SpanDetail";
```

### Step 4: Create Trace Detail Page

**File:** `src/pages/TraceDetailPage.tsx`

```tsx
import * as React from "react";
import { useParams, useNavigate } from "react-router-dom";
import { ArrowLeft, Activity, Clock, Layers } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { TraceTimeline, SpanDetail } from "@/components/traces";
import { useTrace } from "@/hooks/use-telemetry";
import type { Span } from "@/types";

export function TraceDetailPage() {
  const { traceId } = useParams<{ traceId: string }>();
  const navigate = useNavigate();
  const { data: spans, isLoading, error } = useTrace(traceId || "");
  const [selectedSpan, setSelectedSpan] = React.useState<Span | null>(null);

  if (!traceId) {
    return <div>Invalid trace ID</div>;
  }

  const rootSpan = spans?.find((s) => !s.parent_span_id);
  const totalDuration = rootSpan?.duration_ms || 0;

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center gap-4 p-4 border-b">
        <Button variant="ghost" size="icon" onClick={() => navigate("/traces")}>
          <ArrowLeft className="h-4 w-4" />
        </Button>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <Activity className="h-5 w-5 text-primary" />
            <h1 className="text-lg font-semibold truncate">
              {rootSpan?.name || "Trace Details"}
            </h1>
          </div>
          <div className="flex items-center gap-3 text-sm text-muted-foreground">
            <span className="font-mono text-xs">{traceId}</span>
          </div>
        </div>

        {/* Stats */}
        <div className="flex items-center gap-4 text-sm">
          <div className="flex items-center gap-1.5">
            <Clock className="h-4 w-4 text-muted-foreground" />
            <span>{totalDuration.toFixed(1)} ms</span>
          </div>
          <div className="flex items-center gap-1.5">
            <Layers className="h-4 w-4 text-muted-foreground" />
            <span>{spans?.length || 0} spans</span>
          </div>
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 flex overflow-hidden">
        {/* Timeline */}
        <div className="flex-1 overflow-hidden">
          {isLoading ? (
            <div className="flex items-center justify-center h-full text-muted-foreground">
              Loading trace...
            </div>
          ) : error ? (
            <div className="flex items-center justify-center h-full text-red-500">
              Error loading trace
            </div>
          ) : !spans?.length ? (
            <div className="flex items-center justify-center h-full text-muted-foreground">
              No spans found
            </div>
          ) : (
            <ScrollArea className="h-full">
              <TraceTimeline
                spans={spans}
                selectedSpanId={selectedSpan?.span_id}
                onSelectSpan={setSelectedSpan}
                className="p-4"
              />
            </ScrollArea>
          )}
        </div>

        {/* Span Detail Panel */}
        {selectedSpan && (
          <SpanDetail
            span={selectedSpan}
            onClose={() => setSelectedSpan(null)}
            className="w-[350px]"
          />
        )}
      </div>
    </div>
  );
}
```

### Step 5: Export from Pages

**File:** `src/pages/index.ts`

```tsx
// Add export
export { TraceDetailPage } from "./TraceDetailPage";
```

### Step 6: Update App Router

**File:** `src/App.tsx`

```tsx
// Add import
import { TraceDetailPage } from "@/pages";

// Add route inside DashboardLayout routes
<Route path="/traces/:traceId" element={<TraceDetailPage />} />
```

### Step 7: Update TracesPage Navigation

**File:** `src/pages/TracesPage.tsx`

Add navigation to trace rows:

```tsx
import { useNavigate } from "react-router-dom";

// Inside component:
const navigate = useNavigate();

// On trace row click:
onClick={() => navigate(`/traces/${trace.trace_id}`)}
```

---

## Gotchas

- Span tree building uses DFS for correct depth ordering
- Timeline bar minimum width (4px) prevents invisible spans
- Selected span state is local to page
- Span detail panel slides in from right
- Copy buttons use individual useState for feedback
- Parent span IDs may be null (root spans)

---

## Test

```bash
cd /Users/ancplua/qyl/src/qyl.dashboard && npm run dev
```

1. Go to `/traces`
2. Click a trace row → navigates to `/traces/{traceId}`
3. Timeline shows all spans with hierarchy
4. Click a span → detail panel opens on right
5. Click X → panel closes
6. Copy buttons work for IDs
7. Back button returns to trace list

- [ ] TraceDetailPage renders
- [ ] Timeline shows Gantt bars
- [ ] Span selection works
- [ ] Detail panel shows attributes
- [ ] Copy buttons work
- [ ] Navigation works both ways
- [ ] No TS errors
- [ ] No console errors

---

*Template v3 - One prompt, one agent, done.*
