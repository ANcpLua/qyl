import {useEffect, useState} from 'react';
import {useLocation} from 'react-router-dom';
import {Clock, Pause, Play, RefreshCw, Zap} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import {ThemeToggle} from '@/components/ui/theme-toggle';
import {HealthIndicator} from '@/components/health';

interface TopBarProps {
    isLive: boolean;
    streamConnected: boolean;
    onLiveToggle: () => void;
    onRefresh: () => void;
    timeRange: string;
    onTimeRangeChange: (range: string) => void;
}

const pageTitle: Record<string, string> = {
    '/traces': 'TRACES',
    '/logs': 'STRUCTURED LOGS',
    '/cost': 'GENAI COST',
};

const timeRanges = [
    {value: '5m', label: '5 MIN'},
    {value: '15m', label: '15 MIN'},
    {value: '30m', label: '30 MIN'},
    {value: '1h', label: '1 HOUR'},
    {value: '3h', label: '3 HOURS'},
    {value: '6h', label: '6 HOURS'},
    {value: '12h', label: '12 HOURS'},
    {value: '24h', label: '24 HOURS'},
    {value: '7d', label: '7 DAYS'},
];

export function TopBar({
                           isLive,
                           streamConnected,
                           onLiveToggle,
                           onRefresh,
                           timeRange,
                           onTimeRangeChange,
                       }: TopBarProps) {
    const location = useLocation();
    const [currentTime, setCurrentTime] = useState(new Date());

    // Update time every second
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
            {/* Page title - BRUTALIST style */}
            <div className="flex items-center gap-2 shrink-0">
                <Zap className="w-4 h-4 text-signal-orange"/>
                <h1 className="text-xs md:text-sm font-semibold tracking-[0.14em] text-brutal-white">{title}</h1>
            </div>

            {/* Separator */}
            <div className="hidden md:block w-px h-5 bg-brutal-zinc/70"/>

            {/* Spacer */}
            <div className="flex-1"/>

            {/* Current time display */}
            <div
                className="hidden lg:flex items-center gap-1.5 px-2.5 py-1 bg-brutal-dark/85 border border-brutal-zinc">
                <Clock className="w-4 h-4 text-signal-cyan"/>
                <span className="font-mono text-xs text-signal-cyan">{formatTime(currentTime)}</span>
            </div>

            {/* Time range selector - BRUTALIST */}
            <Select value={timeRange} onValueChange={onTimeRangeChange}>
                <SelectTrigger
                    className="h-8 md:h-9 w-24 md:w-28 bg-brutal-dark/85 border border-brutal-zinc text-xs font-semibold tracking-[0.08em] text-brutal-white hover:border-signal-orange">
                    <SelectValue/>
                </SelectTrigger>
                <SelectContent className="bg-brutal-carbon border border-brutal-zinc">
                    {timeRanges.map((range) => (
                        <SelectItem
                            key={range.value}
                            value={range.value}
                            className="text-xs font-semibold tracking-[0.08em] text-brutal-white hover:bg-brutal-dark focus:bg-signal-orange/20 focus:text-signal-orange"
                        >
                            {range.label}
                        </SelectItem>
                    ))}
                </SelectContent>
            </Select>

            {/* Live toggle - BRUTALIST button */}
            <Button
                variant="outline"
                size="sm"
                onClick={onLiveToggle}
                className={cn(
                    'h-8 md:h-9 border text-[11px] font-semibold tracking-[0.08em] transition-colors',
                    isLive
                        ? (streamConnected
                            ? 'bg-signal-green/20 border-signal-green text-signal-green hover:bg-signal-green/30'
                            : 'bg-signal-yellow/20 border-signal-yellow text-signal-yellow hover:bg-signal-yellow/30')
                        : 'bg-brutal-dark/85 border-signal-yellow text-signal-yellow hover:bg-signal-yellow/20'
                )}
            >
                {isLive ? (
                    <>
                        <Pause className="w-4 h-4 mr-2"/>
                        {streamConnected ? 'LIVE' : 'CONNECTING'}
                    </>
                ) : (
                    <>
                        <Play className="w-4 h-4 mr-2"/>
                        PAUSED
                    </>
                )}
            </Button>

            {/* Health indicator */}
            <HealthIndicator/>

            {/* Theme toggle */}
            <ThemeToggle/>

            {/* Refresh - BRUTALIST */}
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
