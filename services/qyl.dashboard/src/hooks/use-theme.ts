import {useCallback, useEffect, useState} from 'react';
import {z} from 'zod';

const themeSchema = z.enum(['light', 'dark', 'system']);

type Theme = z.infer<typeof themeSchema>;

// A theme written by an older build — or by anything else on this origin — is untrusted input,
// so a stale or corrupt value falls back to the default instead of being asserted into `Theme`.
const storedThemeSchema = themeSchema.catch('dark');

export function readStoredTheme(stored: string | null): Theme {
    return storedThemeSchema.parse(stored);
}

function getSystemTheme(): 'light' | 'dark' {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function applyTheme(theme: Theme) {
    const resolved = theme === 'system' ? getSystemTheme() : theme;
    document.documentElement.classList.toggle('dark', resolved === 'dark');
}

export function useTheme() {
    const [theme, setThemeState] = useState<Theme>(() => {
        if (typeof window === 'undefined') return 'dark';
        return readStoredTheme(localStorage.getItem('theme'));
    });

    const setTheme = useCallback((newTheme: Theme) => {
        setThemeState(newTheme);
        localStorage.setItem('theme', newTheme);
        applyTheme(newTheme);
    }, []);

    useEffect(() => {
        applyTheme(theme);

        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        const handleChange = () => {
            if (theme === 'system') {
                applyTheme('system');
            }
        };

        mediaQuery.addEventListener('change', handleChange);
        return () => mediaQuery.removeEventListener('change', handleChange);
    }, [theme]);

    const toggleTheme = useCallback(() => {
        setTheme(theme === 'dark' ? 'light' : 'dark');
    }, [theme, setTheme]);

    const resolvedTheme = theme === 'system' ? getSystemTheme() : theme;

    return {theme, setTheme, toggleTheme, resolvedTheme};
}
