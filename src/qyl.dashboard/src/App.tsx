import {QueryClient, QueryClientProvider, useQuery} from '@tanstack/react-query';
import {BrowserRouter, Navigate, Route, Routes} from 'react-router-dom';
import {IconContext} from '@phosphor-icons/react';
import {Toaster} from '@/components/ui/sonner';
import {DashboardLayout} from '@/components/layout';
import {
    AlertsPage,
    CostPage,
    DashboardPage,
    ErrorsOutagesPage,
    GenAIPage,
    IssueDetailPage,
    IssuesPage,
    LogsPage,
    OnboardingPage,
    PerformancePage,
    SearchPage,
    ServicesPage,
    SettingsPage,
    SpanExplorerPage,
    TracesPage,
} from '@/pages';

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
                </BrowserRouter>
                <Toaster richColors position="bottom-right"/>
            </QueryClientProvider>
        </IconContext.Provider>
    );
}
