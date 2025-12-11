import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {BrowserRouter, Route, Routes} from 'react-router-dom';
import {Toaster} from '@/components/ui/sonner';
import {DashboardLayout} from '@/components/layout';
import {GenAIPage, LogsPage, MetricsPage, ResourcesPage, SettingsPage, TracesPage,} from '@/pages';

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
          <Route element={<DashboardLayout/>}>
            <Route path="/" element={<ResourcesPage/>}/>
            <Route path="/traces" element={<TracesPage/>}/>
            <Route path="/logs" element={<LogsPage/>}/>
            <Route path="/metrics" element={<MetricsPage/>}/>
            <Route path="/genai" element={<GenAIPage/>}/>
            <Route path="/settings" element={<SettingsPage/>}/>
          </Route>
        </Routes>
      </BrowserRouter>
      <Toaster richColors position="bottom-right"/>
    </QueryClientProvider>
  );
}
