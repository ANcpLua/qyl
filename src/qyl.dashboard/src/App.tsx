import {lazy, Suspense} from 'react';
import {QueryClient, QueryClientProvider, useQuery} from '@tanstack/react-query';
import {BrowserRouter, Navigate, Route, Routes} from 'react-router-dom';
import {IconContext} from '@phosphor-icons/react';
import {Toaster} from '@/components/ui/sonner';
import {DashboardLayout} from '@/components/layout';

const TracesPage = lazy(() => import('@/pages/TracesPage').then(m => ({default: m.TracesPage})));
const LogsPage = lazy(() => import('@/pages/LogsPage').then(m => ({default: m.LogsPage})));
const GenAIPage = lazy(() => import('@/pages/GenAIPage').then(m => ({default: m.GenAIPage})));
const DashboardPage = lazy(() => import('@/pages/DashboardPage').then(m => ({default: m.DashboardPage})));
const CostPage = lazy(() => import('@/pages/CostPage').then(m => ({default: m.CostPage})));
const ServicesPage = lazy(() => import('@/pages/ServicesPage').then(m => ({default: m.ServicesPage})));
const SearchPage = lazy(() => import('@/pages/SearchPage').then(m => ({default: m.SearchPage})));
const SettingsPage = lazy(() => import('@/pages/SettingsPage').then(m => ({default: m.SettingsPage})));
const IssuesPage = lazy(() => import('@/pages/IssuesPage').then(m => ({default: m.IssuesPage})));
const IssueDetailPage = lazy(() => import('@/pages/IssueDetailPage').then(m => ({default: m.IssueDetailPage})));
const OnboardingPage = lazy(() => import('@/pages/OnboardingPage').then(m => ({default: m.OnboardingPage})));
const AlertsPage = lazy(() => import('@/pages/AlertsPage').then(m => ({default: m.AlertsPage})));
const PerformancePage = lazy(() => import('@/pages/PerformancePage').then(m => ({default: m.PerformancePage})));
const ErrorsOutagesPage = lazy(() => import('@/pages/ErrorsOutagesPage').then(m => ({default: m.ErrorsOutagesPage})));
const SpanExplorerPage = lazy(() => import('@/pages/SpanExplorerPage').then(m => ({default: m.SpanExplorerPage})));

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 1000 * 30, // 30 seconds
            retry: 1,
            refetchOnWindowFocus: false,
        },
    },
});

function FirstVisitGate() {
    const {data, isLoading} = useQuery({
        queryKey: ['github-status'],
        queryFn: async () => {
            const res = await fetch('/api/v1/github/status');
            if (!res.ok) return {configured: false};
            return res.json() as Promise<{ configured: boolean }>;
        },
        staleTime: 1000 * 60 * 5,
    });

    if (isLoading) return null;
    if (!data?.configured) return <Navigate to="/onboarding" replace/>;
    return <DashboardPage/>;
}

export default function App() {
    return (
        <IconContext.Provider value={{weight: 'bold'}}>
            <QueryClientProvider client={queryClient}>
                <BrowserRouter>
                    <Suspense>
                        <Routes>
                            <Route path="/index.html" element={<Navigate to="/" replace/>}/>
                            <Route element={<DashboardLayout/>}>
                                <Route path="/" element={<FirstVisitGate/>}/>
                                <Route path="/traces" element={<TracesPage/>}/>
                                <Route path="/logs" element={<LogsPage/>}/>
                                <Route path="/genai" element={<GenAIPage/>}/>
                                <Route path="/cost" element={<CostPage/>}/>
                                <Route path="/services" element={<ServicesPage/>}/>
                                <Route path="/dashboards" element={<DashboardPage/>}/>
                                <Route path="/dashboards/:id" element={<DashboardPage/>}/>
                                <Route path="/search" element={<SearchPage/>}/>
                                <Route path="/settings" element={<SettingsPage/>}/>
                                <Route path="/issues" element={<IssuesPage/>}/>
                                <Route path="/issues/:issueId" element={<IssueDetailPage/>}/>
                                <Route path="/onboarding" element={<OnboardingPage/>}/>
                                <Route path="/alerts" element={<AlertsPage/>}/>
                                <Route path="/performance" element={<PerformancePage/>}/>
                                <Route path="/errors" element={<ErrorsOutagesPage/>}/>
                                <Route path="/spans" element={<SpanExplorerPage/>}/>
                            </Route>
                        </Routes>
                    </Suspense>
                </BrowserRouter>
                <Toaster richColors position="bottom-right"/>
            </QueryClientProvider>
        </IconContext.Provider>
    );
}
