# Feature: [FEATURE_NAME]

> **Status:** Draft | Ready | Done
> **Effort:** ~30min | ~1h | ~2h
> **Backend:** Yes | No
> **Priority:** P0 | P1 | P2

---

## Problem

<!-- 1-2 sentences: What's broken or missing? -->

## Solution

<!-- 1-2 sentences: What will you build? -->

---

## Context

### Dashboard Location

```
/Users/ancplua/qyl/src/qyl.dashboard/
```

### Stack (DO NOT CHANGE)

| Tech           | Version | Notes                     |
|----------------|---------|---------------------------|
| React          | 19.2.0  | No forwardRef, hooks only |
| TypeScript     | 5.9.3   | Strict mode               |
| Tailwind       | 4.1.17  | `cn()` helper             |
| TanStack Query | 5.90.11 | `telemetryKeys` factory   |
| Lucide         | 0.555.0 | Icons                     |
| Sonner         | 2.0.7   | Toasts                    |
| Radix UI       | Latest  | Primitives                |

### Patterns

```tsx
// Component pattern
import { cn } from "@/lib/utils";
interface Props { className?: string; }
export function Component({ className }: Props) {
  return <div className={cn("base", className)} />;
}

// Data fetching
import { useQuery } from "@tanstack/react-query";
import { telemetryKeys } from "@/hooks/use-telemetry";

// Toasts
import { toast } from "sonner";
toast.success("Done"); toast.error("Failed");
```

---

## Files

| File          | Action        | What          |
|---------------|---------------|---------------|
| <!-- path --> | Create/Modify | <!-- what --> |

---

## Implementation

### Step 1: [Title]

**File:** `src/path/file.tsx`

```tsx
// COMPLETE CODE - agent implements exactly this
```

### Step 2: [Title]

<!-- Continue... -->

---

## Gotchas

- `e.stopPropagation()` in nested click handlers
- Parent needs `group` class for `group-hover:`
- `TooltipProvider` already in App.tsx
- Use `cn()` for className merging

---

## Test

```bash
cd /Users/ancplua/qyl/src/qyl.dashboard && npm run dev
```

- [ ] Feature works
- [ ] No TS errors
- [ ] No console errors

---

## Backend (if needed)

### Endpoint

```
METHOD /api/v1/path
```

### Request/Response

```json
// Request
{}
// Response
{}
```

### File

**Path:** `src/qyl.collector/[path].cs`

```csharp
// COMPLETE CODE
```

---

*Template v3 - One prompt, one agent, done.*
