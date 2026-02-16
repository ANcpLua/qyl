import {useEffect, useRef, useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {AlertCircle, Bot, ChevronRight, FileText, Network, Search, Workflow, Zap,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import type {SearchResult} from '@/hooks/use-search';
import {useSearch} from '@/hooks/use-search';

const entityTypes = [
    {key: '', label: 'All'},
    {key: 'span', label: 'Spans'},
    {key: 'log', label: 'Logs'},
    {key: 'error', label: 'Errors'},
    {key: 'agent_run', label: 'Agent Runs'},
    {key: 'workflow', label: 'Workflows'},
] as const;

const entityStyles: Record<string, { color: string; icon: typeof Search }> = {
    span: {color: 'bg-blue-500/20 text-blue-400 border-blue-500/40', icon: Network},
    log: {color: 'bg-green-500/20 text-green-400 border-green-500/40', icon: FileText},
    error: {color: 'bg-red-500/20 text-red-400 border-red-500/40', icon: AlertCircle},
    agent_run: {color: 'bg-purple-500/20 text-purple-400 border-purple-500/40', icon: Bot},
    workflow: {color: 'bg-amber-500/20 text-amber-400 border-amber-500/40', icon: Workflow},
};

const pillStyles: Record<string, string> = {
    '': 'bg-brutal-zinc/40 text-brutal-white border-brutal-zinc',
    span: 'bg-blue-500/20 text-blue-400 border-blue-500/40',
    log: 'bg-green-500/20 text-green-400 border-green-500/40',
    error: 'bg-red-500/20 text-red-400 border-red-500/40',
    agent_run: 'bg-purple-500/20 text-purple-400 border-purple-500/40',
    workflow: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
};

function formatTimestamp(nanos?: number): string {
    if (!nanos) return '';
    return new Date(nanos / 1_000_000).toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    });
}

function entityRoute(result: SearchResult): string {
    switch (result.entity_type) {
        case 'span':
            return `/traces`;
        case 'log':
            return `/logs`;
        case 'error':
            return `/issues/${result.entity_id}`;
        case 'agent_run':
            return `/agents/${result.entity_id}`;
        case 'workflow':
            return `/workflows/${result.entity_id}`;
        default:
            return '/';
    }
}

function HighlightedSnippet({text}: { text: string }) {
    const parts = text.split(/(<mark>.*?<\/mark>)/g);
    return (
        <span>
            {parts.map((part, i) => {
                if (part.startsWith('<mark>') && part.endsWith('</mark>')) {
                    return (
                        <span key={i} className="bg-signal-orange/30 text-signal-orange font-bold px-0.5 rounded">
                            {part.slice(6, -7)}
                        </span>
                    );
                }
                return <span key={i}>{part}</span>;
            })}
        </span>
    );
}

function SkeletonRow() {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            <div className="w-5 h-5 bg-brutal-zinc rounded"/>
            <div className="flex-1 space-y-2">
                <div className="w-48 h-4 bg-brutal-zinc rounded"/>
                <div className="w-80 h-3 bg-brutal-zinc rounded"/>
            </div>
            <div className="w-24 h-3 bg-brutal-zinc rounded"/>
        </div>
    );
}

function ResultRow({result, onClick}: { result: SearchResult; onClick: () => void }) {
    const style = entityStyles[result.entity_type] ?? entityStyles.span;
    const Icon = style.icon;

    return (
        <div
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            onClick={onClick}
        >
            <Icon className={cn('w-5 h-5 flex-shrink-0', style.color.split(' ')[1])}/>

            <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                    <span className="text-sm font-bold text-brutal-white truncate">
                        {result.title}
                    </span>
                    <Badge variant="outline" className={cn('text-[10px] uppercase tracking-wider', style.color)}>
                        {result.entity_type.replace('_', ' ')}
                    </Badge>
                </div>
                {result.snippet && (
                    <p className="text-xs text-brutal-slate mt-0.5 truncate">
                        <HighlightedSnippet text={result.snippet}/>
                    </p>
                )}
            </div>

            {result.timestamp && (
                <span className="font-mono text-xs text-brutal-slate flex-shrink-0">
                    {formatTimestamp(result.timestamp)}
                </span>
            )}

            <ChevronRight
                className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

export function SearchPage() {
    const navigate = useNavigate();
    const inputRef = useRef<HTMLInputElement>(null);
    const [query, setQuery] = useState('');
    const [activeType, setActiveType] = useState('');

    const filterTypes = activeType ? [activeType] : [];
    const {data: results, isLoading, error} = useSearch(query, filterTypes);

    // Cmd+K focus
    useEffect(() => {
        function handleKeyDown(e: KeyboardEvent) {
            if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
                e.preventDefault();
                inputRef.current?.focus();
            }
        }

        document.addEventListener('keydown', handleKeyDown);
        return () => document.removeEventListener('keydown', handleKeyDown);
    }, []);

    const hasQuery = query.length >= 2;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Search failed</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {error instanceof Error ? error.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return (
        <div className="p-6 space-y-6">
            {/* Search bar */}
            <div className="relative">
                <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-brutal-slate"/>
                <input
                    ref={inputRef}
                    type="text"
                    placeholder="Search across all telemetry data…"
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    className="w-full h-14 pl-12 pr-20 text-lg bg-slate-800 border-2 border-slate-700 rounded text-brutal-white placeholder:text-brutal-slate focus:outline-none focus:border-signal-orange transition-colors"
                    aria-label="Search telemetry data"
                />
                <kbd className="absolute right-4 top-1/2 -translate-y-1/2 kbd text-xs text-brutal-slate">⌘K</kbd>
            </div>

            {/* Entity type pills */}
            <div className="flex items-center gap-2 flex-wrap">
                {entityTypes.map((et) => (
                    <button
                        key={et.key}
                        onClick={() => setActiveType(et.key)}
                        className={cn(
                            'px-3 py-1.5 text-xs font-bold tracking-wider border-2 rounded transition-all',
                            activeType === et.key
                                ? pillStyles[et.key]
                                : 'bg-transparent text-brutal-slate border-brutal-zinc hover:border-brutal-slate hover:text-brutal-white'
                        )}
                    >
                        {et.label.toUpperCase()}
                    </button>
                ))}
            </div>

            {/* Results */}
            <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                {!hasQuery ? (
                    <div className="py-16 text-center">
                        <Search className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">Search across all telemetry data</p>
                        <p className="text-brutal-zinc text-xs mt-1">Spans, logs, errors, agent runs, and workflows</p>
                    </div>
                ) : isLoading ? (
                    <>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                    </>
                ) : !results || results.length === 0 ? (
                    <div className="py-16 text-center">
                        <Zap className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">No results found</p>
                        <p className="text-brutal-zinc text-xs mt-1">Try different keywords or filters</p>
                    </div>
                ) : (
                    <>
                        <div
                            className="px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                            {results.length} RESULT{results.length !== 1 ? 'S' : ''}
                        </div>
                        {results.map((result) => (
                            <ResultRow
                                key={`${result.entity_type}-${result.entity_id}`}
                                result={result}
                                onClick={() => navigate(entityRoute(result))}
                            />
                        ))}
                    </>
                )}
            </div>
        </div>
    );
}
