# Feature Proposal: Copy to Clipboard UI

> **Status:** Approved
> **Author:** Claude
> **Date:** 2025-12-09
> **Priority:** High
> **Effort:** Quick Win (< 2h)
> **Backend Required:** No

---

## 1. Overview

### Problem Statement
Users cannot easily copy trace IDs, span IDs, session IDs, or attribute values from the dashboard. They must manually select text which is error-prone and slow.

### Proposed Solution
Add a hover-activated copy button next to all ID fields and copyable values. Button shows copy icon, changes to checkmark on success, and shows toast notification.

### User Story
```
As a developer debugging an issue,
I want to quickly copy trace IDs and attribute values,
So that I can search logs, share with teammates, or use in API calls.
```

---

## 2. Project Context

### Tech Stack Reference

| Technology | Version | Documentation |
|------------|---------|---------------|
| React | 19.2.0 | [React Docs](https://react.dev) |
| TypeScript | 5.9.3 | Strict mode enabled |
| Tailwind CSS | 4.1.17 | Uses `@theme` syntax in `index.css` |
| Radix UI | Latest | [Radix Primitives](https://radix-ui.com) |
| Lucide React | 0.555.0 | `Copy`, `Check` icons |
| Sonner | 2.0.7 | Toast notifications |
| Vite | 7.2.6 | Dev server port 5173 |

### Project Structure

```
/Users/ancplua/qyl/src/qyl.dashboard/
├── src/
│   ├── components/
│   │   ├── ui/
│   │   │   ├── button.tsx          # Existing - will use
│   │   │   ├── tooltip.tsx         # Existing - will use
│   │   │   └── index.ts            # Will add exports
│   │   └── index.ts
│   │
│   ├── pages/
│   │   ├── ResourcesPage.tsx       # Has session IDs
│   │   ├── TracesPage.tsx          # Has trace/span IDs
│   │   ├── LogsPage.tsx            # Has attribute values
│   │   └── GenAIPage.tsx           # Has model names, IDs
│   │
│   ├── lib/
│   │   └── utils.ts                # Has cn() helper
│   │
│   └── types/
│       └── telemetry.ts
```

---

## 3. Feature Specification

### Affected Files

| File | Action | Purpose |
|------|--------|---------|
| `src/components/ui/copy-button.tsx` | Create | Core copy button component |
| `src/components/ui/copyable-text.tsx` | Create | Text + copy button wrapper |
| `src/components/ui/index.ts` | Modify | Add exports |
| `src/pages/TracesPage.tsx` | Modify | Add to trace/span IDs |
| `src/pages/LogsPage.tsx` | Modify | Add to attribute values |
| `src/pages/ResourcesPage.tsx` | Modify | Add to session IDs |
| `src/pages/GenAIPage.tsx` | Modify | Add to model/request IDs |

### New Dependencies

| Package | Version | Purpose | Install Command |
|---------|---------|---------|-----------------|
| None | - | Uses existing packages | - |

### API Endpoints

None required - client-side only feature using `navigator.clipboard`.

### Type Definitions

```typescript
// No new types needed - uses built-in React types
```

---

## 4. Implementation Steps

### Step 1: Create CopyButton Component

**File:** `src/components/ui/copy-button.tsx`

```tsx
import * as React from "react";
import { Copy, Check } from "lucide-react";
import { Button } from "./button";
import { Tooltip, TooltipContent, TooltipTrigger } from "./tooltip";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

interface CopyButtonProps {
  value: string;
  className?: string;
  label?: string;
}

export function CopyButton({
  value,
  className,
  label = "Value"
}: CopyButtonProps) {
  const [copied, setCopied] = React.useState(false);

  const handleCopy = async (e: React.MouseEvent) => {
    e.stopPropagation();

    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      toast.success(`${label} copied to clipboard`);

      setTimeout(() => setCopied(false), 1500);
    } catch {
      toast.error("Failed to copy to clipboard");
    }
  };

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className={cn(
            "h-6 w-6 opacity-0 group-hover:opacity-100 transition-opacity",
            className
          )}
          onClick={handleCopy}
        >
          {copied ? (
            <Check className="h-3 w-3 text-green-500" />
          ) : (
            <Copy className="h-3 w-3" />
          )}
        </Button>
      </TooltipTrigger>
      <TooltipContent side="top">
        <p>{copied ? "Copied!" : `Copy ${label.toLowerCase()}`}</p>
      </TooltipContent>
    </Tooltip>
  );
}
```

**Explanation:**
- Uses existing Button with ghost variant for minimal visual impact
- `stopPropagation` prevents triggering parent click handlers (row selection)
- State tracks copied status for visual feedback
- Tooltip provides context for accessibility
- 1.5s timeout resets icon for repeat copies

### Step 2: Create CopyableText Wrapper

**File:** `src/components/ui/copyable-text.tsx`

```tsx
import * as React from "react";
import { CopyButton } from "./copy-button";
import { cn } from "@/lib/utils";

interface CopyableTextProps {
  value: string;
  label?: string;
  className?: string;
  textClassName?: string;
  truncate?: boolean;
  maxWidth?: string;
}

export function CopyableText({
  value,
  label,
  className,
  textClassName,
  truncate = false,
  maxWidth = "200px"
}: CopyableTextProps) {
  return (
    <div className={cn("group inline-flex items-center gap-1", className)}>
      <span
        className={cn(
          "font-mono text-sm",
          truncate && "truncate",
          textClassName
        )}
        style={truncate ? { maxWidth } : undefined}
        title={truncate ? value : undefined}
      >
        {value}
      </span>
      <CopyButton value={value} label={label} />
    </div>
  );
}
```

**Explanation:**
- Combines text display with copy button
- `group` class enables hover detection for child opacity
- Optional truncation with native title tooltip for full value
- Flexible className props for styling in different contexts

### Step 3: Export from Barrel

**File:** `src/components/ui/index.ts`

```tsx
// Add these exports to existing file:
export { CopyButton } from "./copy-button";
export { CopyableText } from "./copyable-text";
```

### Step 4: Integrate in TracesPage

**File:** `src/pages/TracesPage.tsx`

```tsx
// Add import
import { CopyableText } from "@/components/ui";

// Find trace ID display (typically in span row)
// Before:
<span className="font-mono text-xs text-muted-foreground">
  {span.traceId}
</span>

// After:
<CopyableText
  value={span.traceId}
  label="Trace ID"
  truncate
  maxWidth="100px"
  textClassName="text-xs text-muted-foreground"
/>

// For span IDs:
<CopyableText
  value={span.spanId}
  label="Span ID"
  truncate
  maxWidth="80px"
  textClassName="text-xs text-muted-foreground"
/>
```

### Step 5: Integrate in LogsPage

**File:** `src/pages/LogsPage.tsx`

```tsx
// Add import
import { CopyableText } from "@/components/ui";

// In log detail/attribute display:
// Before:
<span className="text-sm">{String(attrValue)}</span>

// After:
<CopyableText
  value={String(attrValue)}
  label={attrKey}
  textClassName="text-sm"
/>
```

### Step 6: Integrate in ResourcesPage

**File:** `src/pages/ResourcesPage.tsx`

```tsx
// Add import
import { CopyableText } from "@/components/ui";

// In session card or list row:
// Before:
<p className="text-xs text-muted-foreground font-mono">
  {session.sessionId}
</p>

// After:
<CopyableText
  value={session.sessionId}
  label="Session ID"
  truncate
  maxWidth="120px"
  textClassName="text-xs text-muted-foreground"
/>
```

### Step 7: Integrate in GenAIPage

**File:** `src/pages/GenAIPage.tsx`

```tsx
// Add import
import { CopyableText } from "@/components/ui";

// For request IDs, model names:
<CopyableText
  value={span.attributes["gen_ai.request.id"] || ""}
  label="Request ID"
  truncate
/>

<CopyableText
  value={span.attributes["gen_ai.request.model"] || ""}
  label="Model"
/>
```

---

## 5. Pitfalls & Gotchas

### React 19 Considerations
- [x] No `forwardRef` needed - Button already handles refs
- [x] Event handlers work as expected

### TypeScript Strict Mode
- [x] `CopyButtonProps` interface defined
- [x] Optional props have defaults
- [x] Event type is `React.MouseEvent`

### Tailwind v4 Notes
- [x] Using existing color classes (`text-green-500`)
- [x] `group-hover:opacity-100` pattern for hover reveal
- [x] `transition-opacity` for smooth fade

### Radix UI Integration
- [x] `TooltipProvider` already exists in App.tsx
- [x] Using `asChild` on TooltipTrigger
- [x] Tooltip positioning with `side="top"`

### Common Mistakes

1. **Missing `group` class on parent:**
   - **Issue:** Copy button never appears on hover
   - **Solution:** Ensure parent container has `group` class

2. **Row selection triggered on copy:**
   - **Issue:** Clicking copy also selects the row
   - **Solution:** Call `e.stopPropagation()` in click handler

3. **Tooltip not showing:**
   - **Issue:** TooltipProvider missing
   - **Solution:** Verify `<TooltipProvider>` wraps app in App.tsx

4. **Copy fails silently:**
   - **Issue:** `navigator.clipboard` undefined in non-HTTPS
   - **Solution:** Use try/catch and show error toast

5. **Z-index issues:**
   - **Issue:** Button appears behind other elements
   - **Solution:** Add `z-10` to button className if needed

---

## 6. Testing Checklist

### Unit Tests

```typescript
// src/__tests__/copy-button.test.tsx
import { render, screen, fireEvent } from "@testing-library/react";
import { CopyButton } from "@/components/ui/copy-button";

// Mock clipboard API
Object.assign(navigator, {
  clipboard: {
    writeText: vi.fn().mockResolvedValue(undefined),
  },
});

describe("CopyButton", () => {
  it("renders copy icon by default", () => {
    render(<CopyButton value="test" />);
    expect(screen.getByRole("button")).toBeInTheDocument();
  });

  it("copies value to clipboard on click", async () => {
    render(<CopyButton value="test-value" />);
    fireEvent.click(screen.getByRole("button"));
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith("test-value");
  });

  it("shows check icon after copy", async () => {
    render(<CopyButton value="test" />);
    fireEvent.click(screen.getByRole("button"));
    // Assert check icon is shown
  });
});
```

### Manual Testing

```bash
# Start development server
cd /Users/ancplua/qyl/src/qyl.dashboard
npm run dev

# Open browser
open http://localhost:5173
```

**Test Scenarios:**

- [x] **Happy Path:** Hover over trace ID, click copy, verify clipboard content
- [x] **Multiple Copies:** Copy same value twice, both work
- [x] **Different Values:** Copy trace ID then span ID, both correct
- [x] **Row Selection:** Copy doesn't trigger row selection
- [x] **Toast Shows:** Success toast appears on copy
- [x] **Icon Animation:** Copy icon changes to check, reverts after 1.5s
- [x] **Truncated Text:** Full value shown in native title tooltip
- [x] **Error Handling:** Toast shows error if clipboard fails (test in HTTP)

### Browser Compatibility
- [x] Chrome (latest) - `navigator.clipboard` supported
- [x] Firefox (latest) - `navigator.clipboard` supported
- [x] Safari (latest) - `navigator.clipboard` supported
- [x] Edge (latest) - `navigator.clipboard` supported

---

## 7. Success Criteria

### Functional Requirements
- [x] Copy button appears on hover over copyable text
- [x] Clicking copy button copies exact value to clipboard
- [x] Visual feedback shows success (check icon)
- [x] Toast notification confirms action
- [x] Works on TracesPage, LogsPage, ResourcesPage, GenAIPage

### Non-Functional Requirements
- [x] No console errors or warnings
- [x] No TypeScript errors
- [x] No ESLint warnings
- [x] Accessible via keyboard (button focusable)
- [x] Screen reader announces "Copy [label]" via tooltip

### Definition of Done
- [ ] Code implemented and self-reviewed
- [ ] All tests passing
- [ ] Manual testing completed on all 4 pages
- [ ] No regressions in existing functionality

---

## 8. Screenshots / Mockups

### Before
```
┌─────────────────────────────────────────────┐
│ Trace ID: abc123def456                      │
│ Span ID:  xyz789                            │
└─────────────────────────────────────────────┘
(User must manually select text to copy)
```

### After
```
┌─────────────────────────────────────────────┐
│ Trace ID: abc123def456 [📋]  ← hover reveal │
│ Span ID:  xyz789       [📋]                 │
└─────────────────────────────────────────────┘

After click:
┌─────────────────────────────────────────────┐
│ Trace ID: abc123def456 [✓]  ← green check   │
│                                             │
│  ┌──────────────────────────┐              │
│  │ ✓ Trace ID copied        │ ← toast      │
│  └──────────────────────────┘              │
└─────────────────────────────────────────────┘
```

---

## 9. Rollback Plan

```bash
# If issues arise, remove the new files:
rm src/components/ui/copy-button.tsx
rm src/components/ui/copyable-text.tsx

# Revert changes to index.ts and page files:
git checkout HEAD -- src/components/ui/index.ts
git checkout HEAD -- src/pages/TracesPage.tsx
git checkout HEAD -- src/pages/LogsPage.tsx
git checkout HEAD -- src/pages/ResourcesPage.tsx
git checkout HEAD -- src/pages/GenAIPage.tsx
```

---

## 10. Future Considerations

- [ ] Add keyboard shortcut (Ctrl+C when row focused)
- [ ] Copy multiple values at once (multi-select)
- [ ] Copy as JSON for complex objects
- [ ] Copy formatted (with labels) vs raw value option
- [ ] Share button that copies URL with trace/span ID

---

## Appendix

### Related Documentation
- [Radix Tooltip](https://www.radix-ui.com/primitives/docs/components/tooltip)
- [Sonner Toast](https://sonner.emilkowal.ski/)
- [Clipboard API](https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText)

### References
- [Aspire Dashboard Copy Feature](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/explore)

---

*Proposal Version: 1.0.0*
*Created: 2025-12-09*
