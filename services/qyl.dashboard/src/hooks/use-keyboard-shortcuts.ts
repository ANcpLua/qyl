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

let globalModalOpen = false;
const modalListeners = new Set<(open: boolean) => void>();

function setGlobalModalOpen(open: boolean) {
    globalModalOpen = open;
    modalListeners.forEach((listener) => listener(open));
}

export function useKeyboardShortcuts() {
    const [isModalOpen, setIsModalOpen] = useState(globalModalOpen);
    // Instance-local handlers avoid HMR and StrictMode leaks.
    const shortcutsRef = useRef<Map<string, ShortcutHandler>>(new Map());

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
            const target = e.target as HTMLElement;
            if (
                target.tagName === 'INPUT' ||
                target.tagName === 'TEXTAREA' ||
                target.isContentEditable
            ) {
                return;
            }

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

export function useNavigationShortcuts(
    navigate: (path: string) => void,
    registerShortcut: (shortcut: ShortcutHandler) => () => void
) {

    useEffect(() => {
        const unsubscribes = [
            registerShortcut({
                key: 't',
                description: 'Go to Traces',
                handler: () => navigate('/traces'),
            }),
            registerShortcut({
                key: 'c',
                description: 'Go to Console / Logs',
                handler: () => navigate('/logs'),
            }),
            registerShortcut({
                key: '$',
                description: 'Go to GenAI Cost',
                handler: () => navigate('/cost'),
            }),
            registerShortcut({
                key: 'escape',
                description: 'Close panel / Clear selection',
                handler: () => {
                    if (globalModalOpen) {
                        setGlobalModalOpen(false);
                        return;
                    }
                    window.dispatchEvent(new CustomEvent('qyl:escape'));
                },
            }),
        ];

        return () => unsubscribes.forEach((fn) => fn());
    }, [navigate, registerShortcut]);
}
