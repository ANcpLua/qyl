import {lazy, Suspense} from 'react';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {BrowserRouter, Navigate, Route, Routes} from 'react-router-dom';
import {Toaster} from '@/components/ui/sonner';
import {ErrorBoundary} from '@/components/ui/error-boundary';
import {DashboardLayout} from '@/components/layout';

const TracesPage = lazy(() => import('@/pages/TracesPage').then(m => ({default: m.TracesPage})));
const LogsPage = lazy(() => import('@/pages/LogsPage').then(m => ({default: m.LogsPage})));
const CostPage = lazy(() => import('@/pages/CostPage').then(m => ({default: m.CostPage})));

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 30_000,
            retry: 1,
            refetchOnWindowFocus: false,
        },
    },
});

export default function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <BrowserRouter>
                <ErrorBoundary>
                    <Suspense>
                        <Routes>
                            <Route path="/index.html" element={<Navigate to="/traces" replace/>}/>
                            <Route element={<DashboardLayout/>}>
                                <Route path="/" element={<Navigate to="/traces" replace/>}/>
                                <Route path="/traces" element={<TracesPage/>}/>
                                <Route path="/logs" element={<LogsPage/>}/>
                                <Route path="/cost" element={<CostPage/>}/>
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
