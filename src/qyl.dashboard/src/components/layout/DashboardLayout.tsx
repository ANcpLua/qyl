import {useCallback, useState} from 'react';
import {Outlet, useNavigate, useOutletContext} from 'react-router-dom';
import {TooltipProvider} from '@/components/ui/tooltip';
import {Sidebar} from './Sidebar';
import {TopBar} from './TopBar';
import {useLiveStream} from '@/hooks/use-telemetry';
import {useNavigationShortcuts} from '@/hooks/use-keyboard-shortcuts';
import type {Span} from '@/types';

export function DashboardLayout() {
    const navigate = useNavigate();
    const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
    const [isLive, setIsLive] = useState(true);
    const [timeRange, setTimeRange] = useState('15m');

    // Live stream
    const {isConnected, recentSpans, reconnect} = useLiveStream({
        enabled: isLive,
        onConnect: () => console.log('SSE connected'),
        onDisconnect: () => console.log('SSE disconnected'),
    });

    // Keyboard shortcuts
    useNavigationShortcuts(navigate);

    const handleRefresh = useCallback(() => {
        // Dispatch refresh event for pages to handle
        window.dispatchEvent(new CustomEvent('qyl:refresh'));
    }, []);

    const handleLiveToggle = useCallback(() => {
        setIsLive((prev) => !prev);
    }, []);

    return (
        <TooltipProvider>
            <div className="flex h-screen bg-background">
                <Sidebar
                    collapsed={sidebarCollapsed}
                    onCollapsedChange={setSidebarCollapsed}
                    isLive={isConnected}
                />

                <div className="flex-1 flex flex-col min-w-0">
                    <TopBar
                        isLive={isLive && isConnected}
                        onLiveToggle={handleLiveToggle}
                        onRefresh={handleRefresh}
                        timeRange={timeRange}
                        onTimeRangeChange={setTimeRange}
                    />

                    <main className="flex-1 overflow-auto">
                        <Outlet context={{isLive, timeRange, recentSpans, reconnect}}/>
                    </main>
                </div>
            </div>
        </TooltipProvider>
    );
}

// Hook for pages to access layout context
interface DashboardContext {
    isLive: boolean;
    timeRange: string;
    recentSpans: Span[];
    reconnect: () => void;
}

export function useDashboardContext() {
    return useOutletContext<DashboardContext>();
}
