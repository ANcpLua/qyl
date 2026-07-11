import {lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState} from 'react';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {BrowserRouter, Navigate, Route, Routes} from 'react-router-dom';
import {Toaster} from '@/components/ui/sonner';
import {ErrorBoundary} from '@/components/ui/error-boundary';
import {Button} from '@/components/ui/button';
import {DashboardLayout} from '@/components/layout';
import {
    hasDashboardBuildUpdate,
    resolveCurrentDashboardBuildId,
    resolveServerBuildLabel,
    resolveServerDashboardBuildId,
} from '@/lib/build-version';
import {useCollectorMeta} from '@/lib/onboarding';

const TracesPage = lazy(() => import('@/pages/TracesPage').then(m => ({default: m.TracesPage})));
const LogsPage = lazy(() => import('@/pages/LogsPage').then(m => ({default: m.LogsPage})));
const CostPage = lazy(() => import('@/pages/CostPage').then(m => ({default: m.CostPage})));
const OnboardingPage = lazy(() => import('@/pages/OnboardingPage').then(m => ({default: m.OnboardingPage})));

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 1000 * 30, // 30 seconds
            retry: 1,
            refetchOnWindowFocus: false,
        },
    },
});

const BUILD_VERSION_CHECK_INTERVAL_MS = 60_000;

const ONBOARDING_SEEN_KEY = 'qyl:onboarding-seen';

// First visit lands on onboarding (endpoint setup); every visit after that goes straight to
// traces. Purely client-side state — the old gate probed a /api/v1/github/status endpoint the
// collector never served.
function FirstVisitGate() {
    if (localStorage.getItem(ONBOARDING_SEEN_KEY) === null) {
        localStorage.setItem(ONBOARDING_SEEN_KEY, new Date().toISOString());
        return <Navigate to="/onboarding" replace/>;
    }

    return <Navigate to="/traces" replace/>;
}

function BuildUpdateBanner() {
    const {data: meta, refetch} = useCollectorMeta();
    const currentBuildId = useMemo(() => resolveCurrentDashboardBuildId(), []);
    const [dismissedBuildId, setDismissedBuildId] = useState<string | null>(null);
    const lastCheckAtRef = useRef(0);

    const serverBuildId = resolveServerDashboardBuildId(meta);
    const serverBuildLabel = resolveServerBuildLabel(meta);
    const updateAvailable = hasDashboardBuildUpdate(currentBuildId, serverBuildId)
        && dismissedBuildId !== serverBuildId;

    const checkForBuildUpdate = useCallback(() => {
        if (currentBuildId === null) {
            return;
        }

        const now = Date.now();
        if (now - lastCheckAtRef.current < BUILD_VERSION_CHECK_INTERVAL_MS) {
            return;
        }

        lastCheckAtRef.current = now;
        void refetch();
    }, [currentBuildId, refetch]);

    useEffect(() => {
        if (currentBuildId === null) {
            return;
        }

        const handleFocus = () => {
            checkForBuildUpdate();
        };

        const handleVisibilityChange = () => {
            if (document.visibilityState === 'visible') {
                checkForBuildUpdate();
            }
        };

        const intervalId = window.setInterval(() => {
            if (document.visibilityState === 'visible') {
                checkForBuildUpdate();
            }
        }, BUILD_VERSION_CHECK_INTERVAL_MS);
        window.addEventListener('focus', handleFocus);
        document.addEventListener('visibilitychange', handleVisibilityChange);

        return () => {
            window.clearInterval(intervalId);
            window.removeEventListener('focus', handleFocus);
            document.removeEventListener('visibilitychange', handleVisibilityChange);
        };
    }, [checkForBuildUpdate, currentBuildId]);

    if (!updateAvailable || serverBuildId === null) {
        return null;
    }

    return (
        <div
            className="fixed bottom-4 left-4 z-50 max-w-sm rounded-[20px] border border-signal-violet/35 bg-brutal-carbon/94 p-4 shadow-[0_24px_80px_rgba(0,0,0,0.38)] backdrop-blur-xl">
            <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-signal-violet">
                New build available
            </div>
            <div className="mt-3 text-base font-medium tracking-[-0.02em] text-brutal-white">
                A newer qyl dashboard build is ready on this server.
            </div>
            <p className="mt-2 text-sm leading-6 text-brutal-slate">
                Reload before opening more routes so the page picks up the latest build descriptor and avoids stale
                lazy-chunk imports.
                Reload before opening more routes so the page picks up the latest build descriptor and entry bundle
                before stale lazy-chunk imports fail.
            </p>
            {serverBuildLabel && (
                <div className="mt-3 text-[11px] font-mono uppercase tracking-[0.18em] text-brutal-slate">
                    {serverBuildLabel}
                </div>
            )}
            <div className="mt-4 flex gap-3">
                <Button
                    className="h-10 rounded-full bg-signal-violet px-4 text-brutal-white hover:bg-signal-violet/90"
                    onClick={() => window.location.reload()}
                >
                    Reload now
                </Button>
                <Button
                    variant="outline"
                    className="h-10 rounded-full border-white/10 bg-white/0 px-4 text-brutal-white hover:bg-white/6"
                    onClick={() => setDismissedBuildId(serverBuildId)}
                >
                    Later
                </Button>
            </div>
        </div>
    );
}

export default function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <BrowserRouter>
                <ErrorBoundary>
                    <BuildUpdateBanner/>
                    <Suspense>
                        <Routes>
                            <Route path="/index.html" element={<Navigate to="/" replace/>}/>
                            <Route path="/onboarding" element={<OnboardingPage/>}/>
                            <Route element={<DashboardLayout/>}>
                                <Route path="/" element={<FirstVisitGate/>}/>
                                <Route path="/traces" element={<TracesPage/>}/>
                                <Route path="/logs" element={<LogsPage/>}/>
                                <Route path="/cost" element={<CostPage/>}/>
                                {/* Routes from the pre-shrink surface (issues, alerts, …) may live
                                    on in bookmarks; a no-match render would be a blank page. */}
                                <Route path="*" element={<Navigate to="/traces" replace/>}/>
                            </Route>
                        </Routes>
                    </Suspense>
                </ErrorBoundary>
            </BrowserRouter>
            <Toaster richColors position="bottom-right"/>
        </QueryClientProvider>
    );
}
