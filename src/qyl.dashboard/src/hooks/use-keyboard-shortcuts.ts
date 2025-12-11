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

export function useKeyboardShortcuts() {
  const [isModalOpen, setIsModalOpen] = useState(false);
  // Use ref instead of module-level Map to avoid memory leaks with HMR/StrictMode
  const shortcutsRef = useRef<Map<string, ShortcutHandler>>(new Map());

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

      // Show shortcut help with ?
      if (e.key === '?' && !e.ctrlKey && !e.altKey && !e.metaKey) {
        e.preventDefault();
        setIsModalOpen((prev) => !prev);
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
    setIsModalOpen,
    shortcuts: Array.from(shortcutsRef.current.values()),
  };
}

// Common navigation shortcuts hook
export function useNavigationShortcuts(navigate: (path: string) => void) {
  const {registerShortcut} = useKeyboardShortcuts();

  useEffect(() => {
    const unsubscribes = [
      registerShortcut({
        key: 'g',
        description: 'Go to Resources',
        handler: () => navigate('/'),
      }),
      registerShortcut({
        key: 't',
        description: 'Go to Traces',
        handler: () => navigate('/traces'),
      }),
      registerShortcut({
        key: 'l',
        description: 'Go to Logs',
        handler: () => navigate('/logs'),
      }),
      registerShortcut({
        key: 'm',
        description: 'Go to Metrics',
        handler: () => navigate('/metrics'),
      }),
      registerShortcut({
        key: 'a',
        description: 'Go to GenAI',
        handler: () => navigate('/genai'),
      }),
      registerShortcut({
        key: ',',
        description: 'Open Settings',
        handler: () => navigate('/settings'),
      }),
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
      registerShortcut({
        key: 'escape',
        description: 'Close panel / Clear selection',
        handler: () => {
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
