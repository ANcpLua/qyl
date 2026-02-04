import {useCallback, useEffect, useRef, useState} from 'react';

interface ShortcutHandler {
    key: string;
    ctrl?: boolean;
    alt?: boolean;
    shift?: boolean;
    meta?: boolean;
    handler: () => void;
    description: string;
}

function getShortcutKey(e: KeyboardEvent): string {
    const parts: string[] = [];
    if (e.ctrlKey || e.metaKey) parts.push('ctrl');
    if (e.altKey) parts.push('alt');
    if (e.shiftKey) parts.push('shift');
    parts.push(e.key.toLowerCase());
    return parts.join('+');
}

// Global state for modal - shared across hook instances
let globalModalOpen = false;
const modalListeners = new Set<(open: boolean) => void>();

function setGlobalModalOpen(open: boolean) {
    globalModalOpen = open;
    modalListeners.forEach((listener) => listener(open));
}

export function useKeyboardShortcuts() {
    const [isModalOpen, setIsModalOpen] = useState(globalModalOpen);
    // Use ref instead of module-level Map to avoid memory leaks with HMR/StrictMode
    const shortcutsRef = useRef<Map<string, ShortcutHandler>>(new Map());

    // Subscribe to global modal state
    useEffect(() => {
        const listener = (open: boolean) => setIsModalOpen(open);
        modalListeners.add(listener);
        return () => {
            modalListeners.delete(listener);
        };
    }, []);

    const setModalOpen = useCallback((open: boolean) => {
        setGlobalModalOpen(open);
    }, []);

    const registerShortcut = useCallback((shortcut: ShortcutHandler) => {
        const key = [
            shortcut.ctrl ? 'ctrl' : null,
            shortcut.alt ? 'alt' : null,
            shortcut.shift ? 'shift' : null,
            shortcut.key.toLowerCase(),
        ]
            .filter(Boolean)
            .join('+');

        shortcutsRef.current.set(key, shortcut);

        return () => {
            shortcutsRef.current.delete(key);
        };
    }, []);

    useEffect(() => {
        const shortcuts = shortcutsRef.current;

        const handleKeyDown = (e: KeyboardEvent) => {
            // Don't trigger shortcuts when typing in inputs
            const target = e.target as HTMLElement;
            if (
                target.tagName === 'INPUT' ||
                target.tagName === 'TEXTAREA' ||
                target.isContentEditable
            ) {
                return;
            }

            // Show shortcut help with ? (Shift + /)
            if (e.key === '?' || (e.key === '/' && e.shiftKey)) {
                e.preventDefault();
                setGlobalModalOpen(!globalModalOpen);
                return;
            }

            const key = getShortcutKey(e);
            const shortcut = shortcuts.get(key);

            if (shortcut) {
                e.preventDefault();
                shortcut.handler();
            }
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, []);

    return {
        registerShortcut,
        isModalOpen,
        setModalOpen,
        shortcuts: Array.from(shortcutsRef.current.values()),
    };
}

// Navigation shortcuts aligned with Aspire Dashboard pattern
// R = Resources, C = Console/Logs, S = Structured logs (same as logs for now)
// T = Traces, M = Metrics/GenAI
export function useNavigationShortcuts(navigate: (path: string) => void) {
    const {registerShortcut} = useKeyboardShortcuts();

    useEffect(() => {
        const unsubscribes = [
            // R = Resources (home page)
            registerShortcut({
                key: 'r',
                description: 'Go to Resources',
                handler: () => navigate('/'),
            }),
            // T = Traces
            registerShortcut({
                key: 't',
                description: 'Go to Traces',
                handler: () => navigate('/traces'),
            }),
            // C = Console / Logs
            registerShortcut({
                key: 'c',
                description: 'Go to Console / Logs',
                handler: () => navigate('/logs'),
            }),
            // S = Structured logs (alias for logs)
            registerShortcut({
                key: 's',
                description: 'Go to Structured logs',
                handler: () => navigate('/logs'),
            }),
            // M = Metrics / GenAI
            registerShortcut({
                key: 'm',
                description: 'Go to Metrics / GenAI',
                handler: () => navigate('/genai'),
            }),
            // , = Settings (common convention)
            registerShortcut({
                key: ',',
                description: 'Open Settings',
                handler: () => navigate('/settings'),
            }),
            // Ctrl+/ = Focus search
            registerShortcut({
                key: '/',
                ctrl: true,
                description: 'Focus Search',
                handler: () => {
                    const searchInput = document.querySelector<HTMLInputElement>(
                        '[data-search-input]'
                    );
                    searchInput?.focus();
                },
            }),
            // Escape = Close panel / Clear selection
            registerShortcut({
                key: 'escape',
                description: 'Close panel / Clear selection',
                handler: () => {
                    // Close modal if open
                    if (globalModalOpen) {
                        setGlobalModalOpen(false);
                        return;
                    }
                    // Dispatch custom event for panels to handle
                    window.dispatchEvent(new CustomEvent('qyl:escape'));
                },
            }),
        ];

        return () => unsubscribes.forEach((fn) => fn());
    }, [navigate, registerShortcut]);
}

// Format shortcut for display
export function formatShortcut(shortcut: ShortcutHandler): string {
    const parts: string[] = [];
    if (shortcut.ctrl) parts.push('Ctrl');
    if (shortcut.alt) parts.push('Alt');
    if (shortcut.shift) parts.push('Shift');
    parts.push(shortcut.key.toUpperCase());
    return parts.join(' + ');
}
