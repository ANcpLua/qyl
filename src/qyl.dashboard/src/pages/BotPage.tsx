import {useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {
    AlertCircle,
    BarChart2,
    BookOpen,
    ChevronLeft,
    ChevronRight,
    HelpCircle,
    Loader2,
    MessageSquare,
    Search,
    ThumbsDown,
    ThumbsUp,
    TrendingUp,
    Users,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Badge} from '@/components/ui/badge';
import {Card, CardContent} from '@/components/ui/card';
import {Input} from '@/components/ui/input';
import {
    useCoverageGaps,
    useConversations,
    useSatisfaction,
    useSourceAnalytics,
    useTopQuestions,
    useUsers,
    type ConversationSummary,
    type CoverageGap,
    type SourceUsage,
    type TopQuestionCluster,
    type UserSummary,
} from '@/hooks/use-analytics';

// =============================================================================
// Types
// =============================================================================

type Tab = 'conversations' | 'coverage-gaps' | 'top-questions' | 'sources' | 'satisfaction' | 'users';
type Period = 'weekly' | 'monthly' | 'quarterly';

const TABS: {id: Tab; label: string; icon: typeof MessageSquare}[] = [
    {id: 'conversations', label: 'CONVERSATIONS', icon: MessageSquare},
    {id: 'coverage-gaps', label: 'COVERAGE GAPS', icon: AlertCircle},
    {id: 'top-questions', label: 'TOP QUESTIONS', icon: HelpCircle},
    {id: 'sources', label: 'SOURCES', icon: BookOpen},
    {id: 'satisfaction', label: 'SATISFACTION', icon: ThumbsUp},
    {id: 'users', label: 'USERS', icon: Users},
];

// =============================================================================
// Shared helpers
// =============================================================================

function formatTs(iso: string | null | undefined): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short', day: 'numeric',
        hour: '2-digit', minute: '2-digit', hour12: false,
    });
}

function formatTokens(n: number): string {
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
    return String(n);
}

function periodLabel(period: Period, offset: number): string {
    const now = new Date();
    if (period === 'weekly') {
        const monday = new Date(now);
        monday.setDate(now.getDate() - ((now.getDay() + 6) % 7) - 7 * offset);
        const sunday = new Date(monday);
        sunday.setDate(monday.getDate() + 6);
        const fmt = (d: Date) => d.toLocaleDateString('en-US', {month: 'short', day: 'numeric'});
        return `${fmt(monday)} – ${fmt(sunday)}`;
    }
    if (period === 'quarterly') {
        const qStart = new Date(now.getFullYear(), Math.floor(now.getMonth() / 3) * 3, 1);
        qStart.setMonth(qStart.getMonth() - 3 * offset);
        const q = Math.floor(qStart.getMonth() / 3) + 1;
        return `Q${q} ${qStart.getFullYear()}`;
    }
    // monthly
    const d = new Date(now.getFullYear(), now.getMonth() - offset, 1);
    return d.toLocaleString('en-US', {month: 'long', year: 'numeric'}).toUpperCase();
}

// =============================================================================
// Skeleton & Error
// =============================================================================

function SkeletonRow({cols}: {cols: number}) {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            {Array.from({length: cols}, (_, i) => (
                <div key={i} className="h-4 bg-brutal-zinc rounded flex-1"/>
            ))}
        </div>
    );
}

function EmptyState({icon: Icon, message, sub}: {icon: typeof AlertCircle; message: string; sub: string}) {
    return (
        <div className="py-12 text-center">
            <Icon className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
            <p className="text-brutal-slate text-sm">{message}</p>
            <p className="text-brutal-zinc text-xs mt-1">{sub}</p>
        </div>
    );
}

function ErrorCard({error}: {error: unknown}) {
    return (
        <div className="py-8 text-center">
            <AlertCircle className="w-10 h-10 mx-auto mb-3 text-red-500"/>
            <p className="text-red-400 text-sm">Failed to load data</p>
            <p className="text-brutal-slate text-xs mt-1">
                {error instanceof Error ? error.message : 'Unknown error'}
            </p>
        </div>
    );
}

// =============================================================================
// Period controls
// =============================================================================

interface PeriodControlsProps {
    period: Period;
    offset: number;
    onPeriodChange: (p: Period) => void;
    onOffsetChange: (o: number) => void;
}

function PeriodControls({period, offset, onPeriodChange, onOffsetChange}: PeriodControlsProps) {
    return (
        <div className="flex items-center gap-2">
            {(['weekly', 'monthly', 'quarterly'] as Period[]).map((p) => (
                <button
                    key={p}
                    onClick={() => {onPeriodChange(p); onOffsetChange(0);}}
                    className={cn(
                        'px-3 py-1.5 text-[10px] font-bold tracking-wider border-2 transition-all cursor-pointer',
                        period === p
                            ? 'bg-signal-orange/20 text-signal-orange border-signal-orange'
                            : 'text-brutal-slate border-brutal-zinc hover:border-brutal-slate hover:text-brutal-white bg-brutal-dark',
                    )}
                >
                    {p.toUpperCase()}
                </button>
            ))}
            <div className="w-px h-5 bg-brutal-zinc mx-1"/>
            <button
                onClick={() => onOffsetChange(offset + 1)}
                className="p-1.5 border-2 border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-brutal-slate hover:text-brutal-white transition-all cursor-pointer"
                aria-label="Previous period"
            >
                <ChevronLeft className="w-3.5 h-3.5"/>
            </button>
            <span className="text-[10px] font-bold tracking-wider text-brutal-white min-w-[120px] text-center">
                {periodLabel(period, offset)}
            </span>
            <button
                onClick={() => onOffsetChange(Math.max(0, offset - 1))}
                disabled={offset === 0}
                className={cn(
                    'p-1.5 border-2 transition-all cursor-pointer',
                    offset === 0
                        ? 'border-brutal-zinc text-brutal-zinc cursor-not-allowed'
                        : 'border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-brutal-slate hover:text-brutal-white',
                )}
                aria-label="Next period"
            >
                <ChevronRight className="w-3.5 h-3.5"/>
            </button>
        </div>
    );
}

// =============================================================================
// Stat card
// =============================================================================

function StatCard({icon: Icon, label, value, color = 'text-brutal-white', loading}: {
    icon: typeof MessageSquare;
    label: string;
    value: string | number;
    color?: string;
    loading: boolean;
}) {
    return (
        <Card>
            <CardContent className="pt-4">
                <div className="flex items-center gap-2">
                    <Icon className={cn('w-4 h-4', color)}/>
                    <span className="text-xs font-bold text-brutal-slate tracking-wider">{label}</span>
                </div>
                {loading
                    ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                    : <div className={cn('text-2xl font-bold mt-1', color)}>{value}</div>
                }
            </CardContent>
        </Card>
    );
}

// =============================================================================
// Tab: Conversations
// =============================================================================

function ConversationRow({c, onClick}: {c: ConversationSummary; onClick: () => void}) {
    return (
        <div
            onClick={onClick}
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
        >
            <div className="w-40 min-w-0">
                <span className="font-mono text-xs text-brutal-slate truncate block">{c.conversationId.slice(0, 12)}…</span>
            </div>
            <div className="flex-1 min-w-0">
                <span className="text-sm text-brutal-white truncate block">{c.firstQuestion ?? '—'}</span>
            </div>
            <div className="w-16 text-right">
                <span className="font-mono text-xs text-brutal-slate">{c.turnCount}</span>
            </div>
            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">{formatTokens(c.totalInputTokens + c.totalOutputTokens)}</span>
            </div>
            <div className="w-16 text-center">
                {c.hasErrors && (
                    <Badge variant="outline" className="text-[10px] bg-red-500/20 text-red-400 border-red-500/40">ERR</Badge>
                )}
            </div>
            <div className="w-20 text-right">
                <span className="font-mono text-xs text-brutal-slate">{c.userId?.split('@')[0] ?? '—'}</span>
            </div>
            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">{formatTs(c.startTime)}</span>
            </div>
            <ChevronRight className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

function ConversationsTab({period, offset}: {period: Period; offset: number}) {
    const navigate = useNavigate();
    const [page, setPage] = useState(1);
    const [filterErrors, setFilterErrors] = useState<boolean | undefined>();
    const [filterUser, setFilterUser] = useState('');

    const {data, isLoading, error} = useConversations(
        period, offset, page, filterErrors, filterUser || undefined,
    );

    const conversations = data?.conversations ?? [];
    const total = data?.total ?? 0;
    const totalPages = Math.max(1, Math.ceil(total / 20));
    const hasErrors = conversations.filter((c) => c.hasErrors).length;
    const totalTokens = conversations.reduce((s, c) => s + c.totalInputTokens + c.totalOutputTokens, 0);

    return (
        <div className="space-y-4">
            <div className="grid grid-cols-4 gap-4">
                <StatCard icon={MessageSquare} label="CONVERSATIONS" value={total} loading={isLoading}/>
                <StatCard icon={BarChart2} label="TURNS" value={conversations.reduce((s, c) => s + Number(c.turnCount), 0)} loading={isLoading}/>
                <StatCard icon={TrendingUp} label="TOKENS" value={formatTokens(totalTokens)} color="text-signal-cyan" loading={isLoading}/>
                <StatCard icon={AlertCircle} label="WITH ERRORS" value={hasErrors} color={hasErrors > 0 ? 'text-red-400' : 'text-brutal-white'} loading={isLoading}/>
            </div>

            {/* Filters */}
            <div className="flex items-center gap-3">
                <div className="relative max-w-sm flex-1">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate"/>
                    <Input
                        placeholder="Filter by user…"
                        value={filterUser}
                        onChange={(e) => {setFilterUser(e.target.value); setPage(1);}}
                        className="pl-9"
                        aria-label="Filter by user"
                    />
                </div>
                {(['all', 'errors', 'ok'] as const).map((f) => {
                    const active = f === 'errors' ? filterErrors === true : f === 'ok' ? filterErrors === false : filterErrors == null;
                    return (
                        <button
                            key={f}
                            onClick={() => {
                                setFilterErrors(f === 'errors' ? true : f === 'ok' ? false : undefined);
                                setPage(1);
                            }}
                            className={cn(
                                'px-3 py-1.5 text-[10px] font-bold tracking-wider border-2 transition-all cursor-pointer',
                                active
                                    ? 'bg-signal-orange/20 text-signal-orange border-signal-orange'
                                    : 'text-brutal-slate border-brutal-zinc hover:border-brutal-slate bg-brutal-dark',
                            )}
                        >
                            {f.toUpperCase()}
                        </button>
                    );
                })}
                <span className="text-xs font-bold text-brutal-slate tracking-wider ml-auto">
                    {total} TOTAL
                </span>
            </div>

            {/* Table */}
            <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-40">CONVERSATION ID</div>
                    <div className="flex-1">FIRST MESSAGE</div>
                    <div className="w-16 text-right">TURNS</div>
                    <div className="w-28 text-right">TOKENS</div>
                    <div className="w-16 text-center">STATUS</div>
                    <div className="w-20 text-right">USER</div>
                    <div className="w-28 text-right">STARTED</div>
                    <div className="w-4"/>
                </div>
                {error ? (
                    <ErrorCard error={error}/>
                ) : isLoading ? (
                    <>{Array.from({length: 5}, (_, i) => <SkeletonRow key={i} cols={7}/>)}</>
                ) : conversations.length === 0 ? (
                    <EmptyState
                        icon={MessageSquare}
                        message="No conversations found"
                        sub="Conversations will appear as copilot interactions are recorded"
                    />
                ) : (
                    conversations.map((c) => (
                        <ConversationRow
                            key={c.conversationId}
                            c={c}
                            onClick={() => navigate(`/bot/conversations/${encodeURIComponent(c.conversationId)}`)}
                        />
                    ))
                )}
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
                <div className="flex items-center justify-between">
                    <button
                        onClick={() => setPage((p) => Math.max(1, p - 1))}
                        disabled={page === 1}
                        className={cn(
                            'flex items-center gap-1 px-3 py-1.5 text-[10px] font-bold tracking-wider border-2 transition-all cursor-pointer',
                            page === 1
                                ? 'border-brutal-zinc text-brutal-zinc cursor-not-allowed'
                                : 'border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-brutal-slate hover:text-brutal-white',
                        )}
                    >
                        <ChevronLeft className="w-3 h-3"/> PREV
                    </button>
                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">
                        PAGE {page} / {totalPages}
                    </span>
                    <button
                        onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                        disabled={page === totalPages}
                        className={cn(
                            'flex items-center gap-1 px-3 py-1.5 text-[10px] font-bold tracking-wider border-2 transition-all cursor-pointer',
                            page === totalPages
                                ? 'border-brutal-zinc text-brutal-zinc cursor-not-allowed'
                                : 'border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-brutal-slate hover:text-brutal-white',
                        )}
                    >
                        NEXT <ChevronRight className="w-3 h-3"/>
                    </button>
                </div>
            )}
        </div>
    );
}

// =============================================================================
// Tab: Coverage Gaps
// =============================================================================

function GapRow({gap, index}: {gap: CoverageGap; index: number}) {
    const [expanded, setExpanded] = useState(false);
    return (
        <div className="border-b border-brutal-zinc">
            <div
                onClick={() => setExpanded(!expanded)}
                className="flex items-center gap-4 px-4 py-3 hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            >
                <div className="w-8 text-right">
                    <span className="font-mono text-xs text-brutal-zinc">{index + 1}</span>
                </div>
                <div className="flex-1 min-w-0">
                    <span className="text-sm font-bold text-brutal-white truncate block">{gap.topic}</span>
                </div>
                <div className="w-32 text-right">
                    <span className="font-mono text-xs text-signal-yellow">{gap.conversationCount} convos</span>
                </div>
                <ChevronRight className={cn(
                    'w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-all',
                    expanded && 'rotate-90',
                )}/>
            </div>
            {expanded && (
                <div className="px-4 pb-4 space-y-2 bg-brutal-dark/30 border-t border-brutal-zinc/50">
                    {gap.finding && (
                        <div>
                            <div className="label-industrial mt-3">FINDING</div>
                            <p className="text-xs text-brutal-slate mt-1">{gap.finding}</p>
                        </div>
                    )}
                    {gap.recommendation && (
                        <div>
                            <div className="label-industrial">RECOMMENDATION</div>
                            <p className="text-xs text-brutal-slate mt-1">{gap.recommendation}</p>
                        </div>
                    )}
                    {gap.sampleConversationIds.length > 0 && (
                        <div>
                            <div className="label-industrial">SAMPLE IDS</div>
                            <div className="flex flex-wrap gap-2 mt-1">
                                {gap.sampleConversationIds.map((id) => (
                                    <span key={id} className="font-mono text-[10px] text-signal-cyan bg-signal-cyan/10 px-2 py-0.5 border border-signal-cyan/30">
                                        {id.slice(0, 16)}…
                                    </span>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}

function CoverageGapsTab({period, offset}: {period: Period; offset: number}) {
    const {data, isLoading, error} = useCoverageGaps(period, offset);
    const gaps = data?.gaps ?? [];

    return (
        <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
                <StatCard icon={MessageSquare} label="CONVERSATIONS PROCESSED" value={data?.conversationsProcessed ?? 0} loading={isLoading}/>
                <StatCard icon={AlertCircle} label="GAPS IDENTIFIED" value={data?.gapsIdentified ?? 0} color="text-signal-yellow" loading={isLoading}/>
            </div>
            <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-8 text-right">#</div>
                    <div className="flex-1">TOPIC / SPAN NAME</div>
                    <div className="w-32 text-right">CONVERSATIONS</div>
                    <div className="w-4"/>
                </div>
                {error ? (
                    <ErrorCard error={error}/>
                ) : isLoading ? (
                    <>{Array.from({length: 5}, (_, i) => <SkeletonRow key={i} cols={3}/>)}</>
                ) : gaps.length === 0 ? (
                    <EmptyState
                        icon={AlertCircle}
                        message="No coverage gaps detected"
                        sub="Gaps appear when conversations have errors, empty responses, or high latency"
                    />
                ) : (
                    gaps.map((gap, i) => <GapRow key={gap.topic} gap={gap} index={i}/>)
                )}
            </div>
        </div>
    );
}

// =============================================================================
// Tab: Top Questions
// =============================================================================

function ClusterRow({cluster, index}: {cluster: TopQuestionCluster; index: number}) {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 transition-colors">
            <div className="w-8 text-right">
                <span className="font-mono text-xs text-brutal-zinc">{index + 1}</span>
            </div>
            <div className="flex-1 min-w-0">
                <span className="text-sm text-brutal-white truncate block">{cluster.topic}</span>
            </div>
            <div className="w-32 text-right">
                <span className="font-mono text-xs text-signal-cyan">{cluster.conversationCount}</span>
            </div>
        </div>
    );
}

function TopQuestionsTab({period, offset}: {period: Period; offset: number}) {
    const {data, isLoading, error} = useTopQuestions(period, offset);
    const clusters = data?.clusters ?? [];

    return (
        <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
                <StatCard icon={MessageSquare} label="CONVERSATIONS ANALYSED" value={data?.conversationsProcessed ?? 0} loading={isLoading}/>
                <StatCard icon={HelpCircle} label="TOPIC CLUSTERS" value={data?.clustersIdentified ?? 0} color="text-signal-cyan" loading={isLoading}/>
            </div>
            <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-8 text-right">#</div>
                    <div className="flex-1">TOPIC / SPAN NAME</div>
                    <div className="w-32 text-right">CONVERSATIONS</div>
                </div>
                {error ? (
                    <ErrorCard error={error}/>
                ) : isLoading ? (
                    <>{Array.from({length: 8}, (_, i) => <SkeletonRow key={i} cols={3}/>)}</>
                ) : clusters.length === 0 ? (
                    <EmptyState
                        icon={HelpCircle}
                        message="No question clusters yet"
                        sub="Topics will cluster as conversations accumulate"
                    />
                ) : (
                    clusters.map((c, i) => <ClusterRow key={c.topic} cluster={c} index={i}/>)
                )}
            </div>
        </div>
    );
}

// =============================================================================
// Tab: Sources
// =============================================================================

function SourceRow({source, index}: {source: SourceUsage; index: number}) {
    const [expanded, setExpanded] = useState(false);
    return (
        <div className="border-b border-brutal-zinc">
            <div
                onClick={() => setExpanded(!expanded)}
                className="flex items-center gap-4 px-4 py-3 hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            >
                <div className="w-8 text-right">
                    <span className="font-mono text-xs text-brutal-zinc">{index + 1}</span>
                </div>
                <div className="flex-1 min-w-0">
                    <span className="text-sm font-bold text-brutal-white truncate block">{source.sourceId}</span>
                </div>
                <div className="w-28 text-right">
                    <span className="font-mono text-xs text-signal-orange">{source.citationCount.toLocaleString()} citations</span>
                </div>
                <ChevronRight className={cn(
                    'w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-all',
                    expanded && 'rotate-90',
                )}/>
            </div>
            {expanded && source.topQuestions.length > 0 && (
                <div className="px-4 pb-4 bg-brutal-dark/30 border-t border-brutal-zinc/50">
                    <div className="label-industrial mt-3">TOP QUESTIONS</div>
                    <div className="mt-1 space-y-1">
                        {source.topQuestions.map((q) => (
                            <div key={q} className="text-xs text-brutal-slate border-l-2 border-brutal-zinc pl-2">{q}</div>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
}

function SourcesTab({period, offset}: {period: Period; offset: number}) {
    const {data, isLoading, error} = useSourceAnalytics(period, offset);
    const sources = data?.sources ?? [];

    return (
        <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
                <StatCard icon={BookOpen} label="SOURCES CITED" value={sources.length} loading={isLoading}/>
                <StatCard icon={BarChart2} label="TOTAL CITATIONS" value={sources.reduce((s, src) => s + src.citationCount, 0)} color="text-signal-orange" loading={isLoading}/>
            </div>
            <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-8 text-right">#</div>
                    <div className="flex-1">SOURCE ID</div>
                    <div className="w-28 text-right">CITATIONS</div>
                    <div className="w-4"/>
                </div>
                {error ? (
                    <ErrorCard error={error}/>
                ) : isLoading ? (
                    <>{Array.from({length: 5}, (_, i) => <SkeletonRow key={i} cols={3}/>)}</>
                ) : sources.length === 0 ? (
                    <EmptyState
                        icon={BookOpen}
                        message="No sources cited"
                        sub="Sources appear when gen_ai.data_source.id is set on spans"
                    />
                ) : (
                    sources.map((s, i) => <SourceRow key={s.sourceId} source={s} index={i}/>)
                )}
            </div>
        </div>
    );
}

// =============================================================================
// Tab: Satisfaction
// =============================================================================

function SatisfactionRate({rate}: {rate: number}) {
    const pct = Math.round(rate * 100);
    const color = pct >= 80 ? 'text-signal-green' : pct >= 60 ? 'text-signal-yellow' : 'text-red-400';
    return <span className={cn('text-4xl font-bold', color)}>{pct}%</span>;
}

function SatisfactionTab({period, offset}: {period: Period; offset: number}) {
    const {data, isLoading, error} = useSatisfaction(period, offset);

    if (error) return <div className="border-2 border-brutal-zinc bg-brutal-carbon p-4"><ErrorCard error={error}/></div>;

    return (
        <div className="space-y-4">
            {/* Summary cards */}
            <div className="grid grid-cols-4 gap-4">
                <StatCard icon={BarChart2} label="TOTAL FEEDBACK" value={data?.totalFeedback ?? 0} loading={isLoading}/>
                <StatCard icon={ThumbsUp} label="UPVOTES" value={data?.upvotes ?? 0} color="text-signal-green" loading={isLoading}/>
                <StatCard icon={ThumbsDown} label="DOWNVOTES" value={data?.downvotes ?? 0} color="text-red-400" loading={isLoading}/>
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <TrendingUp className="w-4 h-4 text-signal-green"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">SATISFACTION</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className="mt-1"><SatisfactionRate rate={data?.satisfactionRate ?? 0}/></div>
                        }
                    </CardContent>
                </Card>
            </div>

            <div className="grid grid-cols-2 gap-4">
                {/* By model */}
                <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                    <div className="px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                        BY MODEL
                    </div>
                    {isLoading ? (
                        Array.from({length: 3}, (_, i) => <SkeletonRow key={i} cols={3}/>)
                    ) : (data?.byModel ?? []).length === 0 ? (
                        <EmptyState icon={BarChart2} message="No model data" sub="Feedback will appear per model"/>
                    ) : (
                        (data?.byModel ?? []).map((m) => (
                            <div key={m.model} className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc">
                                <div className="flex-1 min-w-0">
                                    <span className="text-xs text-brutal-white truncate block">{m.model}</span>
                                </div>
                                <div className="w-16 text-right">
                                    <span className={cn(
                                        'font-mono text-xs font-bold',
                                        m.rate >= 0.8 ? 'text-signal-green' : m.rate >= 0.6 ? 'text-signal-yellow' : 'text-red-400',
                                    )}>
                                        {Math.round(m.rate * 100)}%
                                    </span>
                                </div>
                                <div className="w-16 text-right">
                                    <span className="font-mono text-xs text-red-400">{m.downvotes} ▼</span>
                                </div>
                            </div>
                        ))
                    )}
                </div>

                {/* By topic */}
                <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                    <div className="px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                        BY TOPIC (DOWNVOTES)
                    </div>
                    {isLoading ? (
                        Array.from({length: 3}, (_, i) => <SkeletonRow key={i} cols={3}/>)
                    ) : (data?.byTopic ?? []).length === 0 ? (
                        <EmptyState icon={ThumbsDown} message="No downvotes yet" sub="Downvoted topics will appear here"/>
                    ) : (
                        (data?.byTopic ?? []).map((t) => (
                            <div key={t.topic} className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc">
                                <div className="flex-1 min-w-0">
                                    <span className="text-xs text-brutal-white truncate block">{t.topic}</span>
                                </div>
                                <div className="w-16 text-right">
                                    <span className="font-mono text-xs text-red-400">{t.downvotes} ▼</span>
                                </div>
                                <div className="w-16 text-right">
                                    <span className={cn('font-mono text-xs', t.rate >= 0.8 ? 'text-signal-green' : 'text-signal-yellow')}>
                                        {Math.round(t.rate * 100)}%
                                    </span>
                                </div>
                            </div>
                        ))
                    )}
                </div>
            </div>
        </div>
    );
}

// =============================================================================
// Tab: Users
// =============================================================================

function UserRow({user, onClick}: {user: UserSummary; onClick: () => void}) {
    return (
        <div
            onClick={onClick}
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
        >
            <div className="w-48 min-w-0">
                <span className="text-sm text-brutal-white truncate block">{user.userId}</span>
            </div>
            <div className="w-28 text-right">
                <span className="font-mono text-xs text-signal-cyan">{user.conversationCount}</span>
            </div>
            <div className="flex-1 min-w-0">
                <div className="flex flex-wrap gap-1">
                    {user.topTopics.slice(0, 3).map((t) => (
                        <span key={t} className="text-[10px] text-brutal-slate bg-brutal-dark border border-brutal-zinc px-1.5 py-0.5 truncate max-w-24">
                            {t}
                        </span>
                    ))}
                </div>
            </div>
            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">{formatTs(user.firstSeen)}</span>
            </div>
            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">{formatTs(user.lastSeen)}</span>
            </div>
            <ChevronRight className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

function UsersTab({period, offset}: {period: Period; offset: number}) {
    const navigate = useNavigate();
    const [page, setPage] = useState(1);
    const {data, isLoading, error} = useUsers(period, offset, page);
    const users = data?.users ?? [];
    const total = data?.total ?? 0;
    const totalPages = Math.max(1, Math.ceil(total / 20));

    return (
        <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
                <StatCard icon={Users} label="ACTIVE USERS" value={total} loading={isLoading}/>
                <StatCard icon={MessageSquare} label="TOTAL CONVERSATIONS" value={users.reduce((s, u) => s + u.conversationCount, 0)} color="text-signal-cyan" loading={isLoading}/>
            </div>
            <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-48">USER ID</div>
                    <div className="w-28 text-right">CONVERSATIONS</div>
                    <div className="flex-1">TOP TOPICS</div>
                    <div className="w-28 text-right">FIRST SEEN</div>
                    <div className="w-28 text-right">LAST SEEN</div>
                    <div className="w-4"/>
                </div>
                {error ? (
                    <ErrorCard error={error}/>
                ) : isLoading ? (
                    <>{Array.from({length: 5}, (_, i) => <SkeletonRow key={i} cols={5}/>)}</>
                ) : users.length === 0 ? (
                    <EmptyState
                        icon={Users}
                        message="No users tracked"
                        sub="Users appear when enduser.id is set on GenAI spans"
                    />
                ) : (
                    users.map((u) => (
                        <UserRow
                            key={u.userId}
                            user={u}
                            onClick={() => navigate(`/bot/users/${encodeURIComponent(u.userId)}/journey`)}
                        />
                    ))
                )}
            </div>
            {totalPages > 1 && (
                <div className="flex items-center justify-between">
                    <button
                        onClick={() => setPage((p) => Math.max(1, p - 1))}
                        disabled={page === 1}
                        className={cn(
                            'flex items-center gap-1 px-3 py-1.5 text-[10px] font-bold tracking-wider border-2 transition-all cursor-pointer',
                            page === 1
                                ? 'border-brutal-zinc text-brutal-zinc cursor-not-allowed'
                                : 'border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-brutal-slate hover:text-brutal-white',
                        )}
                    >
                        <ChevronLeft className="w-3 h-3"/> PREV
                    </button>
                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">
                        PAGE {page} / {totalPages}
                    </span>
                    <button
                        onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                        disabled={page === totalPages}
                        className={cn(
                            'flex items-center gap-1 px-3 py-1.5 text-[10px] font-bold tracking-wider border-2 transition-all cursor-pointer',
                            page === totalPages
                                ? 'border-brutal-zinc text-brutal-zinc cursor-not-allowed'
                                : 'border-brutal-zinc bg-brutal-dark text-brutal-slate hover:border-brutal-slate hover:text-brutal-white',
                        )}
                    >
                        NEXT <ChevronRight className="w-3 h-3"/>
                    </button>
                </div>
            )}
        </div>
    );
}

// =============================================================================
// Main Page
// =============================================================================

export function BotPage() {
    const [activeTab, setActiveTab] = useState<Tab>('conversations');
    const [period, setPeriod] = useState<Period>('monthly');
    const [offset, setOffset] = useState(0);

    return (
        <div className="p-6 space-y-6">
            {/* Header row */}
            <div className="flex items-center justify-between">
                <div className="section-header">BOT ANALYTICS</div>
                <PeriodControls
                    period={period}
                    offset={offset}
                    onPeriodChange={(p) => {setPeriod(p); setOffset(0);}}
                    onOffsetChange={setOffset}
                />
            </div>

            {/* Tab strip */}
            <div className="flex items-center border-b-2 border-brutal-zinc">
                {TABS.map(({id, label, icon: Icon}) => (
                    <button
                        key={id}
                        onClick={() => setActiveTab(id)}
                        className={cn(
                            'flex items-center gap-2 px-4 py-2.5 text-[10px] font-bold tracking-wider border-b-2 -mb-[2px] transition-all cursor-pointer',
                            activeTab === id
                                ? 'text-signal-orange border-signal-orange bg-signal-orange/5'
                                : 'text-brutal-slate border-transparent hover:text-brutal-white hover:border-brutal-zinc',
                        )}
                    >
                        <Icon className="w-3.5 h-3.5"/>
                        {label}
                    </button>
                ))}
            </div>

            {/* Tab content */}
            {activeTab === 'conversations' && <ConversationsTab period={period} offset={offset}/>}
            {activeTab === 'coverage-gaps' && <CoverageGapsTab period={period} offset={offset}/>}
            {activeTab === 'top-questions' && <TopQuestionsTab period={period} offset={offset}/>}
            {activeTab === 'sources' && <SourcesTab period={period} offset={offset}/>}
            {activeTab === 'satisfaction' && <SatisfactionTab period={period} offset={offset}/>}
            {activeTab === 'users' && <UsersTab period={period} offset={offset}/>}
        </div>
    );
}
