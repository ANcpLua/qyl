# Feature Proposal: [FEATURE_NAME]

> **Status:** Draft | Approved | Implemented
> **Date:** [YYYY-MM-DD]
> **Effort:** ~10min | ~30min | ~1h (with Claude)
> **Backend Required:** Yes | No

---

## Overview

**Problem:** <!-- One sentence -->

**Solution:** <!-- One sentence -->

**User Story:**
```
As a [user], I want [action] so that [benefit].
```

---

## Codebase Context

### qyl.dashboard Location
```
/Users/ancplua/qyl/src/qyl.dashboard/
```

### Tech Stack (DO NOT CHANGE VERSIONS)

| Tech | Version | Notes |
|------|---------|-------|
| React | 19.2.0 | No forwardRef needed, hooks only |
| TypeScript | 5.9.3 | Strict mode |
| Tailwind | 4.1.17 | `@theme` in index.css, `cn()` helper |
| Radix UI | Latest | TooltipProvider in App.tsx |
| TanStack Query | 5.90.11 | `telemetryKeys` factory |
| Lucide | 0.555.0 | Icon components |
| Sonner | 2.0.7 | `toast.success()`, `toast.error()` |
| Vite | 7.2.6 | Port 5173 |

### File Structure

```
src/
├── components/
│   ├── layout/           # DashboardLayout, Sidebar, TopBar
│   ├── ui/               # button, badge, card, input, select, tabs, tooltip, etc.
│   └── index.ts          # Barrel exports
├── pages/                # ResourcesPage, TracesPage, LogsPage, MetricsPage, GenAIPage, SettingsPage
├── hooks/                # use-telemetry, use-keyboard-shortcuts, use-theme
├── types/telemetry.ts    # All interfaces
├── lib/utils.ts          # cn() helper
└── App.tsx               # Router + QueryClientProvider + TooltipProvider
```

### Patterns to Follow

**New Component:**
```tsx
import * as React from "react";
import { cn } from "@/lib/utils";

interface MyComponentProps { /* props */ }

export function MyComponent({ className, ...props }: MyComponentProps) {
  return <div className={cn("base-classes", className)} {...props} />;
}
```

**Export from barrel:** Add to `src/components/ui/index.ts`

**Data fetching:** Use `useQuery` with `telemetryKeys`

**Toasts:** `import { toast } from "sonner"`

---

## Files to Change

| File | Action | What |
|------|--------|------|
| `src/components/ui/[name].tsx` | Create | <!-- describe --> |
| `src/components/ui/index.ts` | Modify | Add export |
| `src/pages/[Page].tsx` | Modify | <!-- describe --> |

---

## Implementation

### Step 1: [Title]

**File:** `src/[path]`

```tsx
// Full code here - Claude will implement this
```

### Step 2: [Title]

<!-- Continue steps... -->

---

## Gotchas

<!-- Feature-specific pitfalls -->

- **[Issue]:** [What can go wrong]
  - **Fix:** [How to avoid]

### Standard Reminders
- Use `e.stopPropagation()` in nested click handlers
- Parent needs `group` class for `group-hover:` to work
- `TooltipProvider` already wraps app
- Use `cn()` for className merging
- No console errors/TS errors allowed

---

## Test

```bash
cd /Users/ancplua/qyl/src/qyl.dashboard && npm run dev
open http://localhost:5173
```

**Verify:**
- [ ] <!-- Test case 1 -->
- [ ] <!-- Test case 2 -->
- [ ] <!-- Test case 3 -->
- [ ] No console errors

---

## Done When

- [ ] Feature works as described
- [ ] No TypeScript errors
- [ ] No console errors
- [ ] Tested manually in browser

---

## Future Ideas (out of scope)

- <!-- Enhancement 1 -->
- <!-- Enhancement 2 -->

---

*Template v2.0 - Optimized for Claude-assisted development*
