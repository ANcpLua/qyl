import {useCallback, useState} from 'react';
import {Outlet, useLocation, useNavigate, useOutletContext} from 'react-router-dom';
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
import {cn} from '@/lib/utils';

export function DashboardLayout() {
    const location = useLocation();
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

    const isOnboardingRoute = location.pathname === '/onboarding';

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
                        isLive={isLive}
                        streamConnected={isConnected}
                        onLiveToggle={handleLiveToggle}
                        onRefresh={handleRefresh}
                        timeRange={timeRange}
                        onTimeRangeChange={setTimeRange}
                    />

                    <main className={cn(
                        'flex-1 overflow-auto bg-brutal-black',
                        isOnboardingRoute ? 'bg-onboarding-canvas' : 'bg-grid-overlay'
                    )}>
                        <Outlet context={{isLive, timeRange, recentSpans, reconnect}}/>
                    </main>
                </div>
            </div>

            {/* Keyboard Shortcuts Help Modal */}
            <KeyboardShortcutsModal
                open={isModalOpen}
                onOpenChange={setModalOpen}
            />

            {/* Copilot - always available (BYOK works without GitHub auth) */}
            <CopilotPanel
                open={copilotOpen}
                onClose={() => setCopilotOpen(false)}
                username={copilotStatus?.username}
            />
            <CopilotButton
                onClick={() => setCopilotOpen(prev => !prev)}
                isOpen={copilotOpen}
                username={copilotStatus?.username}
            />
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
