import {QueryClient, QueryClientProvider, useQuery} from '@tanstack/react-query';
import {BrowserRouter, Navigate, Route, Routes} from 'react-router-dom';
import {Toaster} from '@/components/ui/sonner';
import {DashboardLayout} from '@/components/layout';
import {
    AgentRunDetailPage,
    AgentsPage,
    BotConversationDetailPage,
    BotPage,
    BotUserJourneyPage,
    DashboardPage,
    GenAIPage,
    IssueDetailPage,
    IssuesPage,
    LogsPage,
    OnboardingPage,
    ResourcesPage,
    SearchPage,
    SettingsPage,
    TracesPage,
    WorkflowRunDetailPage,
    WorkflowRunsPage,
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
    return <ResourcesPage/>;
}

export default function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <BrowserRouter>
                <Routes>
                    <Route path="/index.html" element={<Navigate to="/" replace/>}/>
                    <Route element={<DashboardLayout/>}>
                        <Route path="/" element={<FirstVisitGate/>}/>
                        <Route path="/traces" element={<TracesPage/>}/>
                        <Route path="/logs" element={<LogsPage/>}/>
                        <Route path="/genai" element={<GenAIPage/>}/>
                        <Route path="/dashboards/:id" element={<DashboardPage/>}/>
                        <Route path="/search" element={<SearchPage/>}/>
                        <Route path="/settings" element={<SettingsPage/>}/>
                        <Route path="/agents" element={<AgentsPage/>}/>
                        <Route path="/agents/:runId" element={<AgentRunDetailPage/>}/>
                        <Route path="/issues" element={<IssuesPage/>}/>
                        <Route path="/issues/:issueId" element={<IssueDetailPage/>}/>
                        <Route path="/workflows" element={<WorkflowRunsPage/>}/>
                        <Route path="/workflows/:runId" element={<WorkflowRunDetailPage/>}/>
                        <Route path="/onboarding" element={<OnboardingPage/>}/>
                        <Route path="/bot" element={<BotPage/>}/>
                        <Route path="/bot/conversations/:conversationId" element={<BotConversationDetailPage/>}/>
                        <Route path="/bot/users/:userId/journey" element={<BotUserJourneyPage/>}/>
                    </Route>
                </Routes>
            </BrowserRouter>
            <Toaster richColors position="bottom-right"/>
        </QueryClientProvider>
    );
}
