import {useCallback, useState} from 'react';
import {Outlet, useNavigate, useOutletContext} from 'react-router-dom';
import {useQueryClient} from '@tanstack/react-query';
import {TooltipProvider} from '@/components/ui/tooltip';
import {Sidebar} from './Sidebar';
import {TopBar} from './TopBar';
import {telemetryKeys, useLiveStream} from '@/hooks/use-telemetry';
import {useCopilotStatus} from '@/hooks/use-copilot';
import {CopilotButton} from '@/components/copilot/CopilotButton';
import {CopilotPanel} from '@/components/copilot/CopilotPanel';
import {useKeyboardShortcuts, useNavigationShortcuts} from '@/hooks/use-keyboard-shortcuts';
import {KeyboardShortcutsModal} from '@/components/KeyboardShortcutsModal';
import type {Span} from '@/types';

export function DashboardLayout() {
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
    const [isLive, setIsLive] = useState(true);
    const [timeRange, setTimeRange] = useState('15m');
    const [copilotOpen, setCopilotOpen] = useState(false);

    // Copilot status (polls every 5min)
    const {data: copilotStatus} = useCopilotStatus();

    // Live stream
    const {isConnected, recentSpans, reconnect, clearSpans} = useLiveStream({
        enabled: isLive,
        onConnect: () => console.log('[QYL] SSE stream connected'),
        onDisconnect: () => console.log('[QYL] SSE stream disconnected'),
    });

    // Keyboard shortcuts
    useNavigationShortcuts(navigate);
    const {isModalOpen, setModalOpen} = useKeyboardShortcuts();

    const handleRefresh = useCallback(() => {
        // Invalidate all telemetry queries and dispatch refresh event
        queryClient.invalidateQueries({queryKey: telemetryKeys.all});
        clearSpans();
        window.dispatchEvent(new CustomEvent('qyl:refresh'));
    }, [queryClient, clearSpans]);

    const handleLiveToggle = useCallback(() => {
        setIsLive((prev) => !prev);
    }, []);

    return (
        <TooltipProvider>
            <div className="flex h-screen bg-brutal-black">
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

                    <main className="flex-1 overflow-auto bg-brutal-black">
                        <Outlet context={{isLive, timeRange, recentSpans, reconnect}}/>
                    </main>
                </div>
            </div>

            {/* Keyboard Shortcuts Help Modal */}
            <KeyboardShortcutsModal
                open={isModalOpen}
                onOpenChange={setModalOpen}
            />

            {/* Copilot - only shown when authenticated */}
            {copilotStatus?.isAuthenticated && (
                <>
                    <CopilotPanel
                        open={copilotOpen}
                        onClose={() => setCopilotOpen(false)}
                        username={copilotStatus.username}
                    />
                    <CopilotButton
                        onClick={() => setCopilotOpen(prev => !prev)}
                        isOpen={copilotOpen}
                        username={copilotStatus.username}
                    />
                </>
            )}
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
