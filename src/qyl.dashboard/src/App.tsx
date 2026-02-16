import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {BrowserRouter, Navigate, Route, Routes} from 'react-router-dom';
import {Toaster} from '@/components/ui/sonner';
import {DashboardLayout} from '@/components/layout';
import {
    AgentRunDetailPage,
    AgentsPage,
    DashboardPage,
    GenAIPage,
    IssueDetailPage,
    IssuesPage,
    LoginPage,
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

export default function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <BrowserRouter>
                <Routes>
                    {/* Login route - outside layout */}
                    <Route path="/login" element={<LoginPage/>}/>

                    <Route path="/index.html" element={<Navigate to="/" replace/>}/>
                    <Route element={<DashboardLayout/>}>
                        <Route path="/" element={<ResourcesPage/>}/>
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
                    </Route>
                </Routes>
            </BrowserRouter>
            <Toaster richColors position="bottom-right"/>
        </QueryClientProvider>
    );
}
