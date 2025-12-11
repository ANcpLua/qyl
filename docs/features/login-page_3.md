# Feature: Login Page

> **Status:** Ready
> **Effort:** ~1h
> **Backend:** No (endpoints exist)
> **Priority:** P0

---

## Problem

Dashboard has no login UI. Users must manually append `?t=<token>` to URL. The `/api/login` endpoint exists but there's no page to use it.

## Solution

Create a login page at `/login` with token input, instructions on where to find the token, and redirect to dashboard on success.

---

## Context

### Dashboard Location
```
/Users/ancplua/qyl/src/qyl.dashboard/
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
| Radix UI | Latest | Primitives |

### Existing Auth Endpoints
```
POST /api/login        - Body: { token: string } → Sets cookie, returns { success: true }
POST /api/logout       - Clears cookie
GET  /api/auth/check   - Returns { authenticated: boolean }
```

### Patterns
```tsx
// Component pattern
import { cn } from "@/lib/utils";
interface Props { className?: string; }
export function Component({ className }: Props) {
  return <div className={cn("base", className)} />;
}

// Data fetching
import { useQuery, useMutation } from "@tanstack/react-query";

// Toasts
import { toast } from "sonner";
toast.success("Done"); toast.error("Failed");
```

---

## Files

| File | Action | What |
|------|--------|------|
| `src/pages/LoginPage.tsx` | Create | Login form with token input |
| `src/pages/index.ts` | Modify | Add LoginPage export |
| `src/hooks/use-auth.ts` | Create | Auth hooks (login, logout, check) |
| `src/App.tsx` | Modify | Add /login route outside DashboardLayout |

---

## Implementation

### Step 1: Create Auth Hooks

**File:** `src/hooks/use-auth.ts`

```tsx
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";

interface LoginResponse {
  success: boolean;
  error?: string;
}

interface AuthStatus {
  authenticated: boolean;
}

export const authKeys = {
  status: ["auth", "status"] as const,
};

async function checkAuth(): Promise<AuthStatus> {
  const res = await fetch("/api/auth/check", { credentials: "include" });
  if (!res.ok) throw new Error("Auth check failed");
  return res.json();
}

async function login(token: string): Promise<LoginResponse> {
  const res = await fetch("/api/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify({ token }),
  });
  return res.json();
}

async function logout(): Promise<void> {
  await fetch("/api/logout", {
    method: "POST",
    credentials: "include",
  });
}

export function useAuthStatus() {
  return useQuery({
    queryKey: authKeys.status,
    queryFn: checkAuth,
    retry: false,
    staleTime: 1000 * 60, // 1 minute
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return useMutation({
    mutationFn: login,
    onSuccess: (data) => {
      if (data.success) {
        queryClient.invalidateQueries({ queryKey: authKeys.status });
        toast.success("Logged in successfully");
        navigate("/");
      } else {
        toast.error(data.error || "Invalid token");
      }
    },
    onError: () => {
      toast.error("Login failed");
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: authKeys.status });
      toast.success("Logged out");
      navigate("/login");
    },
  });
}
```

### Step 2: Create Login Page

**File:** `src/pages/LoginPage.tsx`

```tsx
import * as React from "react";
import { useSearchParams } from "react-router-dom";
import { KeyRound, Terminal, ArrowRight, Eye, EyeOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card } from "@/components/ui/card";
import { useLogin } from "@/hooks/use-auth";
import { cn } from "@/lib/utils";

export function LoginPage() {
  const [searchParams] = useSearchParams();
  const [token, setToken] = React.useState(searchParams.get("t") || "");
  const [showToken, setShowToken] = React.useState(false);
  const loginMutation = useLogin();

  // Auto-login if token in URL
  React.useEffect(() => {
    const urlToken = searchParams.get("t");
    if (urlToken) {
      loginMutation.mutate(urlToken);
    }
  }, []);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (token.trim()) {
      loginMutation.mutate(token.trim());
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-background p-4">
      <Card className="w-full max-w-md p-8 space-y-6">
        {/* Logo */}
        <div className="flex flex-col items-center gap-2">
          <div className="h-12 w-12 rounded-xl bg-primary/10 flex items-center justify-center">
            <KeyRound className="h-6 w-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold">qyl Dashboard</h1>
          <p className="text-muted-foreground text-sm">
            Enter your authentication token to continue
          </p>
        </div>

        {/* Login Form */}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="relative">
            <Input
              type={showToken ? "text" : "password"}
              placeholder="Paste token here..."
              value={token}
              onChange={(e) => setToken(e.target.value)}
              className="pr-10 font-mono"
              autoFocus
            />
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="absolute right-0 top-0 h-full px-3"
              onClick={() => setShowToken(!showToken)}
            >
              {showToken ? (
                <EyeOff className="h-4 w-4 text-muted-foreground" />
              ) : (
                <Eye className="h-4 w-4 text-muted-foreground" />
              )}
            </Button>
          </div>

          <Button
            type="submit"
            className="w-full"
            disabled={!token.trim() || loginMutation.isPending}
          >
            {loginMutation.isPending ? (
              "Logging in..."
            ) : (
              <>
                Log in
                <ArrowRight className="ml-2 h-4 w-4" />
              </>
            )}
          </Button>
        </form>

        {/* Instructions */}
        <div className="rounded-lg bg-muted/50 p-4 space-y-3">
          <div className="flex items-center gap-2 text-sm font-medium">
            <Terminal className="h-4 w-4" />
            Where to find your token
          </div>
          <div className="text-xs text-muted-foreground space-y-2">
            <p>Look for this line in your terminal when starting the collector:</p>
            <code className="block bg-background rounded px-2 py-1 font-mono text-xs">
              Dashboard: http://localhost:5100/login?t=<span className="text-primary">YOUR_TOKEN</span>
            </code>
            <p>Copy the token value and paste it above, or click the link directly.</p>
          </div>
        </div>

        {/* Footer */}
        <p className="text-center text-xs text-muted-foreground">
          Token expires after 3 days. A new token is generated each time the collector starts.
        </p>
      </Card>
    </div>
  );
}
```

### Step 3: Export from Pages

**File:** `src/pages/index.ts`

```tsx
// Add this export to existing file
export { LoginPage } from "./LoginPage";
```

### Step 4: Update App Router

**File:** `src/App.tsx`

```tsx
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { Toaster } from "@/components/ui/sonner";
import { DashboardLayout } from "@/components/layout";
import {
  GenAIPage,
  LogsPage,
  LoginPage,
  MetricsPage,
  ResourcesPage,
  SettingsPage,
  TracesPage,
} from "@/pages";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 30,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          {/* Login route - outside layout */}
          <Route path="/login" element={<LoginPage />} />

          {/* Protected routes */}
          <Route element={<DashboardLayout />}>
            <Route path="/" element={<ResourcesPage />} />
            <Route path="/traces" element={<TracesPage />} />
            <Route path="/logs" element={<LogsPage />} />
            <Route path="/metrics" element={<MetricsPage />} />
            <Route path="/genai" element={<GenAIPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
      <Toaster richColors position="bottom-right" />
    </QueryClientProvider>
  );
}
```

---

## Gotchas

- Auto-login from URL token triggers on mount via `useEffect`
- Token input uses `type="password"` by default for security
- `credentials: "include"` required for cookie handling
- Login mutation handles both success=true and success=false responses
- Font-mono on input for token readability

---

## Test

```bash
cd /Users/ancplua/qyl/src/qyl.dashboard && npm run dev
```

1. Navigate to `http://localhost:5173/login`
2. Page shows login form with instructions
3. Enter invalid token → error toast
4. Enter valid token → redirects to `/`
5. Navigate to `http://localhost:5173/login?t=VALID_TOKEN` → auto-logs in
6. No console errors

- [ ] Login page renders
- [ ] Token input works with show/hide
- [ ] Submit with valid token → redirect to /
- [ ] Submit with invalid token → error toast
- [ ] Auto-login from URL works
- [ ] No TS errors
- [ ] No console errors

---

*Template v3 - One prompt, one agent, done.*
