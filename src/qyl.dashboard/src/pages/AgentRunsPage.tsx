import {useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {
    AlertCircle,
    Bot,
    ChevronRight,
    DollarSign,
    Filter,
    Loader2,
    Cpu,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import {useAgentRuns} from '@/hooks/use-agent-runs';
import type {AgentRun} from '@/hooks/use-agent-runs';

function StatusBadge({status}: {status: string}) {
    const styles: Record<string, string> = {
        running: 'bg-blue-500/20 text-blue-400 border-blue-500/40',
        completed: 'bg-green-500/20 text-green-400 border-green-500/40',
        failed: 'bg-red-500/20 text-red-400 border-red-500/40',
        cancelled: 'bg-brutal-zinc/40 text-brutal-slate border-brutal-zinc',
    };
    return (
        <Badge variant="outline" className={cn('text-[10px] uppercase tracking-wider', styles[status] ?? styles.cancelled)}>
            {status}
        </Badge>
    );
}

function formatTime(nanos?: number): string {
    if (!nanos) return '—';
    return new Date(nanos / 1_000_000).toLocaleTimeString('en-US', {
        hour12: false,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
    });
}

function SkeletonRow() {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            <div className="w-32 h-4 bg-brutal-zinc rounded" />
            <div className="w-20 h-4 bg-brutal-zinc rounded" />
            <div className="w-20 h-4 bg-brutal-zinc rounded" />
            <div className="w-16 h-5 bg-brutal-zinc rounded" />
            <div className="flex-1" />
            <div className="w-24 h-4 bg-brutal-zinc rounded" />
            <div className="w-16 h-4 bg-brutal-zinc rounded" />
            <div className="w-16 h-4 bg-brutal-zinc rounded" />
            <div className="w-16 h-4 bg-brutal-zinc rounded" />
        </div>
    );
}

function AgentRunRow({run, onClick}: {run: AgentRun; onClick: () => void}) {
    const durationMs = run.duration_ns ? nsToMs(run.duration_ns) : null;
    const totalTokens = run.input_tokens + run.output_tokens;

    return (
        <div
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            onClick={onClick}
        >
            <div className="w-40 min-w-0">
                <span className="text-sm font-bold text-brutal-white truncate block">
                    {run.agent_name ?? 'Unnamed Agent'}
                </span>
                {run.agent_type && (
                    <span className="text-[10px] text-brutal-slate">{run.agent_type}</span>
                )}
            </div>

            <div className="w-28 min-w-0">
                <span className="text-xs font-mono text-brutal-slate truncate block">
                    {run.model ?? '—'}
                </span>
            </div>

            <div className="w-20 min-w-0">
                <span className="text-xs text-brutal-slate truncate block">
                    {run.provider ?? '—'}
                </span>
            </div>

            <div className="w-24">
                <StatusBadge status={run.status} />
            </div>

            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {run.input_tokens.toLocaleString()} / {run.output_tokens.toLocaleString()}
                </span>
                {totalTokens > 0 && (
                    <div className="text-[10px] text-brutal-slate">
                        Σ {totalTokens.toLocaleString()}
                    </div>
                )}
            </div>

            <div className="w-20 text-right">
                <span className="font-mono text-xs text-signal-green">
                    ${run.total_cost.toFixed(4)}
                </span>
            </div>

            <div className="w-20 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {durationMs !== null ? formatDuration(durationMs) : '—'}
                </span>
            </div>

            <div className="w-20 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatTime(run.start_time)}
                </span>
            </div>

            <ChevronRight className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0" />
        </div>
    );
}

export function AgentRunsPage() {
    const navigate = useNavigate();
    const [agentNameFilter, setAgentNameFilter] = useState('');
    const [statusFilter, setStatusFilter] = useState<string>('');

    const {data: runs, isLoading, error} = useAgentRuns({
        agentName: agentNameFilter || undefined,
        status: statusFilter && statusFilter !== 'all' ? statusFilter : undefined,
    });

    // Stats
    const totalRuns = runs?.length ?? 0;
    const totalCost = runs?.reduce((sum, r) => sum + r.total_cost, 0) ?? 0;
    const totalTokens = runs?.reduce((sum, r) => sum + r.input_tokens + r.output_tokens, 0) ?? 0;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500" />
                        <p className="text-red-400">Failed to load agent runs</p>
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
            {/* Stats cards */}
            <div className="grid grid-cols-3 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Bot className="w-4 h-4 text-signal-orange" />
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">AGENT RUNS</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate" />
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-brutal-white">{totalRuns}</div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Cpu className="w-4 h-4 text-cyan-500" />
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TOTAL TOKENS</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate" />
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-brutal-white">{totalTokens.toLocaleString()}</div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <DollarSign className="w-4 h-4 text-signal-green" />
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TOTAL COST</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate" />
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-signal-green">${totalCost.toFixed(4)}</div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Filters */}
            <div className="flex items-center gap-4">
                <div className="relative flex-1 max-w-sm">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate" />
                    <Input
                        placeholder="Filter by agent name…"
                        value={agentNameFilter}
                        onChange={(e) => setAgentNameFilter(e.target.value)}
                        className="pl-9"
                        aria-label="Filter by agent name"
                    />
                </div>

                <Select value={statusFilter} onValueChange={setStatusFilter}>
                    <SelectTrigger className="w-40" aria-label="Filter by status">
                        <SelectValue placeholder="All statuses" />
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="all">All statuses</SelectItem>
                        <SelectItem value="running">Running</SelectItem>
                        <SelectItem value="completed">Completed</SelectItem>
                        <SelectItem value="failed">Failed</SelectItem>
                        <SelectItem value="cancelled">Cancelled</SelectItem>
                    </SelectContent>
                </Select>

                <div className="text-xs font-bold text-brutal-slate tracking-wider">
                    {totalRuns} RUN{totalRuns !== 1 ? 'S' : ''}
                </div>
            </div>

            {/* Table */}
            <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                {/* Header */}
                <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-40">AGENT</div>
                    <div className="w-28">MODEL</div>
                    <div className="w-20">PROVIDER</div>
                    <div className="w-24">STATUS</div>
                    <div className="w-28 text-right">TOKENS (IN/OUT)</div>
                    <div className="w-20 text-right">COST</div>
                    <div className="w-20 text-right">DURATION</div>
                    <div className="w-20 text-right">TIME</div>
                    <div className="w-4" />
                </div>

                {/* Body */}
                {isLoading ? (
                    <>
                        <SkeletonRow />
                        <SkeletonRow />
                        <SkeletonRow />
                        <SkeletonRow />
                        <SkeletonRow />
                    </>
                ) : !runs || runs.length === 0 ? (
                    <div className="py-12 text-center">
                        <Bot className="w-12 h-12 mx-auto mb-4 text-brutal-zinc" />
                        <p className="text-brutal-slate text-sm">No agent runs found</p>
                        <p className="text-brutal-zinc text-xs mt-1">Agent runs will appear as your AI agents are traced</p>
                    </div>
                ) : (
                    runs.map((run) => (
                        <AgentRunRow
                            key={run.run_id}
                            run={run}
                            onClick={() => navigate(`/agents/${run.run_id}`)}
                        />
                    ))
                )}
            </div>
        </div>
    );
}
