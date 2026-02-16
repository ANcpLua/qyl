import {useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {AlertCircle, ChevronRight, DollarSign, Filter, GitBranch, Loader2, Workflow,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue,} from '@/components/ui/select';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import type {WorkflowRun} from '@/hooks/use-workflows';
import {useWorkflowRuns} from '@/hooks/use-workflows';

function StatusBadge({status}: { status: string }) {
    const styles: Record<string, string> = {
        pending: 'bg-slate-500/20 text-slate-400 border-slate-500/40',
        running: 'bg-blue-500/20 text-blue-400 border-blue-500/40',
        completed: 'bg-green-500/20 text-green-400 border-green-500/40',
        failed: 'bg-red-500/20 text-red-400 border-red-500/40',
        cancelled: 'bg-brutal-zinc/40 text-brutal-slate border-brutal-zinc',
    };
    return (
        <Badge variant="outline"
               className={cn('text-[10px] uppercase tracking-wider', styles[status] ?? styles.cancelled)}>
            {status}
        </Badge>
    );
}

function ProgressBar({completed, total}: { completed: number; total: number }) {
    const pct = total > 0 ? Math.round((completed / total) * 100) : 0;
    return (
        <div className="flex items-center gap-2">
            <div className="w-20 h-1.5 bg-brutal-zinc rounded-full overflow-hidden">
                <div
                    className={cn(
                        'h-full rounded-full transition-all',
                        pct === 100 ? 'bg-green-500' : pct > 0 ? 'bg-blue-500' : 'bg-brutal-zinc',
                    )}
                    style={{width: `${pct}%`}}
                />
            </div>
            <span className="font-mono text-xs text-brutal-slate">
                {completed}/{total}
            </span>
        </div>
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
            <div className="w-32 h-4 bg-brutal-zinc rounded"/>
            <div className="w-20 h-4 bg-brutal-zinc rounded"/>
            <div className="w-24 h-4 bg-brutal-zinc rounded"/>
            <div className="w-16 h-4 bg-brutal-zinc rounded"/>
            <div className="flex-1"/>
            <div className="w-16 h-4 bg-brutal-zinc rounded"/>
            <div className="w-16 h-4 bg-brutal-zinc rounded"/>
        </div>
    );
}

function WorkflowRunRow({run, onClick}: { run: WorkflowRun; onClick: () => void }) {
    const durationMs = run.duration_ns ? nsToMs(run.duration_ns) : null;

    return (
        <div
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            onClick={onClick}
        >
            <div className="w-40 min-w-0">
                <span className="text-sm font-bold text-brutal-white truncate block">
                    {run.workflow_name ?? 'Unnamed Workflow'}
                </span>
                {run.workflow_type && (
                    <span className="text-[10px] text-brutal-slate">{run.workflow_type}</span>
                )}
            </div>

            <div className="w-24">
                <StatusBadge status={run.status}/>
            </div>

            <div className="w-32">
                <ProgressBar completed={run.completed_nodes} total={run.node_count}/>
            </div>

            <div className="w-20 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {durationMs !== null ? formatDuration(durationMs) : '—'}
                </span>
            </div>

            <div className="w-20 min-w-0">
                <span className="text-xs text-brutal-slate truncate block">
                    {run.trigger ?? '—'}
                </span>
            </div>

            <div className="w-20 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatTime(run.start_time)}
                </span>
            </div>

            <ChevronRight
                className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

export function WorkflowRunsPage() {
    const navigate = useNavigate();
    const [statusFilter, setStatusFilter] = useState<string>('');

    const {data: runs, isLoading, error} = useWorkflowRuns({
        status: statusFilter && statusFilter !== 'all' ? statusFilter : undefined,
    });

    const totalRuns = runs?.length ?? 0;
    const totalCost = runs?.reduce((sum, r) => sum + r.total_cost, 0) ?? 0;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load workflow runs</p>
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
                            <Workflow className="w-4 h-4 text-signal-orange"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">WORKFLOW RUNS</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-brutal-white">{totalRuns}</div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <GitBranch className="w-4 h-4 text-cyan-500"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">COMPLETED</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-brutal-white">
                                {runs?.filter((r) => r.status === 'completed').length ?? 0}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <DollarSign className="w-4 h-4 text-signal-green"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TOTAL COST</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-signal-green">${totalCost.toFixed(4)}</div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Filters */}
            <div className="flex items-center gap-4">
                <div className="relative flex items-center gap-2">
                    <Filter className="w-4 h-4 text-brutal-slate"/>
                </div>

                <Select value={statusFilter} onValueChange={setStatusFilter}>
                    <SelectTrigger className="w-40" aria-label="Filter by status">
                        <SelectValue placeholder="All statuses"/>
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="all">All statuses</SelectItem>
                        <SelectItem value="pending">Pending</SelectItem>
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
                <div
                    className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-40">WORKFLOW</div>
                    <div className="w-24">STATUS</div>
                    <div className="w-32">PROGRESS</div>
                    <div className="w-20 text-right">DURATION</div>
                    <div className="w-20">TRIGGER</div>
                    <div className="w-20 text-right">TIME</div>
                    <div className="w-4"/>
                </div>

                {/* Body */}
                {isLoading ? (
                    <>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                    </>
                ) : !runs || runs.length === 0 ? (
                    <div className="py-12 text-center">
                        <Workflow className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">No workflow runs found</p>
                        <p className="text-brutal-zinc text-xs mt-1">Workflow runs will appear as your workflows are
                            traced</p>
                    </div>
                ) : (
                    runs.map((run) => (
                        <WorkflowRunRow
                            key={run.run_id}
                            run={run}
                            onClick={() => navigate(`/workflows/${run.run_id}`)}
                        />
                    ))
                )}
            </div>
        </div>
    );
}
