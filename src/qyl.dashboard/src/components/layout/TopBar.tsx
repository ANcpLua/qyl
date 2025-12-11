import {useState} from 'react';
import {useLocation} from 'react-router-dom';
import {Clock, Pause, Play, RefreshCw, Search,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue,} from '@/components/ui/select';

interface TopBarProps {
  isLive: boolean;
  onLiveToggle: () => void;
  onRefresh: () => void;
  timeRange: string;
  onTimeRangeChange: (range: string) => void;
  onSearch?: (query: string) => void;
}

const pageTitle: Record<string, string> = {
  '/': 'Resources',
  '/traces': 'Traces',
  '/logs': 'Structured Logs',
  '/metrics': 'Metrics',
  '/genai': 'GenAI Telemetry',
  '/settings': 'Settings',
};

const timeRanges = [
  {value: '5m', label: 'Last 5 minutes'},
  {value: '15m', label: 'Last 15 minutes'},
  {value: '30m', label: 'Last 30 minutes'},
  {value: '1h', label: 'Last 1 hour'},
  {value: '3h', label: 'Last 3 hours'},
  {value: '6h', label: 'Last 6 hours'},
  {value: '12h', label: 'Last 12 hours'},
  {value: '24h', label: 'Last 24 hours'},
  {value: '7d', label: 'Last 7 days'},
];

export function TopBar({
                         isLive,
                         onLiveToggle,
                         onRefresh,
                         timeRange,
                         onTimeRangeChange,
                         onSearch,
                       }: TopBarProps) {
  const location = useLocation();
  const [searchValue, setSearchValue] = useState('');

  const title = pageTitle[location.pathname] ?? 'qyl.';

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    // Use callback if provided, otherwise dispatch event for page-level handling
    if (onSearch) {
      onSearch(searchValue);
    } else {
      window.dispatchEvent(
        new CustomEvent('qyl:search', {detail: {query: searchValue}})
      );
    }
  };

  return (
    <header className="h-14 border-b border-border bg-card/50 backdrop-blur-sm flex items-center px-4 gap-4">
      {/* Page title */}
      <h1 className="text-lg font-semibold">{title}</h1>

      {/* Search */}
      <form onSubmit={handleSearch} className="flex-1 max-w-md">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground"/>
          <Input
            data-search-input
            type="text"
            placeholder="Search traces, logs, resources... (Ctrl+/)"
            value={searchValue}
            onChange={(e) => setSearchValue(e.target.value)}
            className="pl-9 bg-muted/50"
          />
        </div>
      </form>

      {/* Spacer */}
      <div className="flex-1"/>

      {/* Time range selector */}
      <div className="flex items-center gap-2">
        <Clock className="w-4 h-4 text-muted-foreground"/>
        <Select value={timeRange} onValueChange={onTimeRangeChange}>
          <SelectTrigger className="w-40">
            <SelectValue/>
          </SelectTrigger>
          <SelectContent>
            {timeRanges.map((range) => (
              <SelectItem key={range.value} value={range.value}>
                {range.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Live toggle */}
      <Button
        variant={isLive ? 'default' : 'outline'}
        size="sm"
        onClick={onLiveToggle}
        className={cn(isLive && 'bg-green-600 hover:bg-green-700')}
      >
        {isLive ? (
          <>
            <Pause className="w-4 h-4 mr-1"/>
            Live
          </>
        ) : (
          <>
            <Play className="w-4 h-4 mr-1"/>
            Paused
          </>
        )}
      </Button>

      {/* Refresh */}
      <Button variant="outline" size="icon" onClick={onRefresh}>
        <RefreshCw className="w-4 h-4"/>
      </Button>
    </header>
  );
}
