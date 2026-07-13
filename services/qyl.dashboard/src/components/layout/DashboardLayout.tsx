import {useCallback, useState} from 'react';
import {Outlet, useNavigate} from 'react-router-dom';
import {useQueryClient} from '@tanstack/react-query';
import {TooltipProvider} from '@/components/ui/tooltip';
import {Sidebar} from './Sidebar';
import {TopBar} from './TopBar';
import {telemetryKeys} from '@/hooks/use-telemetry';
import {useKeyboardShortcuts, useNavigationShortcuts} from '@/hooks/use-keyboard-shortcuts';
import {KeyboardShortcutsModal} from '@/components/KeyboardShortcutsModal';

export function DashboardLayout() {
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
    // Keyboard shortcuts
    const keyboard = useKeyboardShortcuts();
    useNavigationShortcuts(navigate, keyboard.registerShortcut);
    const {isModalOpen, setModalOpen} = keyboard;

    const handleRefresh = useCallback(() => {
        // Invalidate all telemetry queries and dispatch refresh event
        queryClient.invalidateQueries({queryKey: telemetryKeys.all});
        window.dispatchEvent(new CustomEvent('qyl:refresh'));
    }, [queryClient]);

    return (
        <TooltipProvider>
            <div className="flex h-screen bg-brutal-black">
                <Sidebar
                    collapsed={sidebarCollapsed}
                    onCollapsedChange={setSidebarCollapsed}
                />

                <div className="flex-1 flex flex-col min-w-0">
                    <TopBar
                        onRefresh={handleRefresh}
                    />

                    <main className="flex-1 overflow-auto bg-brutal-black bg-grid-overlay">
                        <Outlet/>
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
