import {useEffect, useState} from 'react';
import {useLocation} from 'react-router-dom';
import {ArrowsClockwise, Clock, Lightning, MagnifyingGlass, Pause, Play} from '@phosphor-icons/react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import {ThemeToggle} from '@/components/ui/theme-toggle';
import {HealthIndicator} from '@/components/health';
import {ClearTelemetryDialog} from '@/components/ClearTelemetryDialog';

interface TopBarProps {
    isLive: boolean;
    streamConnected: boolean;
    onLiveToggle: () => void;
    onRefresh: () => void;
    timeRange: string;
    onTimeRangeChange: (range: string) => void;
    onSearch?: (query: string) => void;
}

const pageTitle: Record<string, string> = {
    '/': 'RESOURCES',
    '/traces': 'TRACES',
    '/logs': 'STRUCTURED LOGS',
    '/metrics': 'METRICS',
    '/genai': 'GENAI TELEMETRY',
    '/search': 'SEARCH',
    '/settings': 'SETTINGS',
    '/bot': 'BOT ANALYTICS',
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
                           onSearch,
                       }: TopBarProps) {
    const location = useLocation();
    const [searchValue, setSearchValue] = useState('');
    const [currentTime, setCurrentTime] = useState(new Date());

    // Update time every second
    useEffect(() => {
        const interval = setInterval(() => setCurrentTime(new Date()), 1000);
        return () => clearInterval(interval);
    }, []);

    const title = pageTitle[location.pathname]
        ?? (location.pathname.startsWith('/dashboards/')
            ? (location.pathname.split('/').pop() ?? 'dashboard').replace(/-/g, ' ').toUpperCase()
            : 'QYL.');

    const handleSearch = (e: React.SyntheticEvent<HTMLFormElement>) => {
        e.preventDefault();
        if (onSearch) {
            onSearch(searchValue);
        } else {
            window.dispatchEvent(
                new CustomEvent('qyl:search', {detail: {query: searchValue}})
            );
        }
    };

    const formatTime = (date: Date) => {
        return date.toLocaleTimeString('en-US', {
            hour12: false,
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
        });
    };

    return (
        <header className="h-12 border-b border-brutal-zinc/70 bg-brutal-carbon/92 backdrop-blur-sm flex items-center px-3 md:px-3.5 gap-2.5 shadow-[0_8px_22px_-18px_rgba(0,0,0,0.8)]">
            {/* Page title - BRUTALIST style */}
            <div className="flex items-center gap-2 shrink-0">
                <Lightning className="w-4 h-4 text-signal-orange"/>
                <h1 className="text-xs md:text-sm font-semibold tracking-[0.14em] text-brutal-white">{title}</h1>
            </div>

            {/* Separator */}
            <div className="hidden md:block w-px h-5 bg-brutal-zinc/70"/>

            {/* Search - BRUTALIST input */}
            <form onSubmit={handleSearch} className="flex-1 min-w-[12rem] md:min-w-[16rem] max-w-xl">
                <div className="relative">
                    <MagnifyingGlass className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate"/>
                    <Input
                        data-search-input
                        type="text"
                        placeholder="SEARCH... (CTRL+/)"
                        value={searchValue}
                        onChange={(e) => setSearchValue(e.target.value)}
                        className="h-8 md:h-9 pl-9 bg-brutal-dark/85 border border-brutal-zinc text-brutal-white placeholder:text-brutal-slate text-xs tracking-[0.08em] focus:border-signal-orange"
                        aria-label="Search"
                    />
                </div>
            </form>

            {/* Spacer */}
            <div className="hidden xl:block flex-1"/>

            {/* Current time display */}
            <div className="hidden lg:flex items-center gap-1.5 px-2.5 py-1 bg-brutal-dark/85 border border-brutal-zinc">
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

            {/* Clear telemetry */}
            <ClearTelemetryDialog onCleared={onRefresh}/>

            {/* Refresh - BRUTALIST */}
            <Button
                variant="outline"
                size="icon"
                onClick={onRefresh}
                className="border border-brutal-zinc bg-brutal-dark/85 text-brutal-slate hover:border-signal-orange hover:text-signal-orange hover:bg-signal-orange/10"
                aria-label="Refresh data"
            >
                <ArrowsClockwise className="w-4 h-4"/>
            </Button>
        </header>
    );
}
