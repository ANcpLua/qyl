import {useCallback, useState} from 'react';
import {Outlet, useLocation, useNavigate, useOutletContext} from 'react-router-dom';
import {useQueryClient} from '@tanstack/react-query';
import {TooltipProvider} from '@/components/ui/tooltip';
import {Sidebar} from './Sidebar';
import {TopBar} from './TopBar';
import {telemetryKeys, useLiveStream} from '@/hooks/use-telemetry';
import {useKeyboardShortcuts, useNavigationShortcuts} from '@/hooks/use-keyboard-shortcuts';
import {KeyboardShortcutsModal} from '@/components/KeyboardShortcutsModal';
import type {Span} from '@/types';
import {cn} from '@/lib/utils';

export function DashboardLayout() {
    const location = useLocation();
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
    const [isLive, setIsLive] = useState(false);
    const [timeRange, setTimeRange] = useState('15m');
    // Live stream
    const {isConnected, recentSpans, reconnect, clearSpans} = useLiveStream({
        enabled: isLive,
    });

    // Keyboard shortcuts
    const keyboard = useKeyboardShortcuts();
    useNavigationShortcuts(navigate, keyboard.registerShortcut);
    const {isModalOpen, setModalOpen} = keyboard;

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
