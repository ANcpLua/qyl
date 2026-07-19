import {useEffect, useState} from 'react';
import {useLocation} from 'react-router-dom';
import {Clock, RefreshCw, Zap} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {ThemeToggle} from '@/components/ui/theme-toggle';
import {HealthIndicator} from '@/components/health/HealthIndicator';

interface TopBarProps {
    onRefresh: () => void;
}

const pageTitle: Record<string, string> = {
    '/traces': 'TRACES',
    '/logs': 'STRUCTURED LOGS',
};

export function TopBar({
                           onRefresh,
                       }: TopBarProps) {
    const location = useLocation();
    const [currentTime, setCurrentTime] = useState(new Date());

    useEffect(() => {
        const interval = setInterval(() => setCurrentTime(new Date()), 1000);
        return () => clearInterval(interval);
    }, []);

    const title = pageTitle[location.pathname] ?? 'QYL.';

    const formatTime = (date: Date) => {
        return date.toLocaleTimeString('en-US', {
            hour12: false,
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
        });
    };

    return (
        <header
            className="h-12 border-b border-brutal-zinc/70 bg-brutal-carbon/92 backdrop-blur-sm flex items-center px-3 md:px-3.5 gap-2.5 shadow-[0_8px_22px_-18px_rgba(0,0,0,0.8)]">
            <div className="flex items-center gap-2 shrink-0">
                <Zap className="w-4 h-4 text-signal-orange"/>
                <h1 className="text-xs md:text-sm font-semibold tracking-[0.14em] text-brutal-white">{title}</h1>
            </div>

            <div className="hidden md:block w-px h-5 bg-brutal-zinc/70"/>

            <div className="flex-1"/>

            <div
                className="hidden lg:flex items-center gap-1.5 px-2.5 py-1 bg-brutal-dark/85 border border-brutal-zinc">
                <Clock className="w-4 h-4 text-signal-cyan"/>
                <span className="font-mono text-xs text-signal-cyan">{formatTime(currentTime)}</span>
            </div>

            <HealthIndicator/>

            <ThemeToggle/>

            <Button
                variant="outline"
                size="icon"
                onClick={onRefresh}
                className="border border-brutal-zinc bg-brutal-dark/85 text-brutal-slate hover:border-signal-orange hover:text-signal-orange hover:bg-signal-orange/10"
                aria-label="Refresh data"
            >
                <RefreshCw className="w-4 h-4"/>
            </Button>
        </header>
    );
}
