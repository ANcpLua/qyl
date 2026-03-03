import {useNavigate, useParams} from 'react-router-dom';
import {
    AlertCircle,
    ArrowLeft,
    Bot,
    Clock,
    Cpu,
    DollarSign,
    GitBranch,
    Link2,
    Loader2,
    ShieldCheck,
    Wrench,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {CopyableText, TextVisualizer} from '@/components/ui';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import {useAgentDecisions, useAgentRun, useToolCalls} from '@/hooks/use-agent-runs';
import {AgentTraceTree} from '@/components/agents';

function StatusBadge({status}: { status: string }) {
    const styles: Record<string, string> = {
        running: 'bg-blue-500/20 text-blue-400 border-blue-500/40',
        completed: 'bg-green-500/20 text-green-400 border-green-500/40',
        failed: 'bg-red-500/20 text-red-400 border-red-500/40',
        cancelled: 'bg-brutal-zinc/40 text-brutal-slate border-brutal-zinc',
    };
    return (
        <Badge variant="outline" className={cn('uppercase tracking-wider', styles[status] ?? styles.cancelled)}>
            {status}
        </Badge>
    );
}

function SkeletonCard() {
    return (
        <Card>
            <CardContent className="pt-4">
                <div className="h-4 w-20 bg-brutal-zinc rounded animate-pulse mb-2"/>
                <div className="h-7 w-32 bg-brutal-zinc rounded animate-pulse"/>
            </CardContent>
        </Card>
    );
}

export function AgentRunDetailPage() {
    const {runId} = useParams<{ runId: string }>();
    const navigate = useNavigate();

    const {data: run, isLoading: runLoading, error: runError} = useAgentRun(runId ?? '');
    const {data: toolCalls = [], isLoading: toolsLoading} = useToolCalls(runId ?? '');
    const {data: decisions = [], isLoading: decisionsLoading} = useAgentDecisions(runId ?? '');

    if (runError) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load agent run</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {runError instanceof Error ? runError.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    const isLoading = runLoading;
    const durationMs = run?.duration_ns ? nsToMs(run.duration_ns) : null;

    return (
        <div className="p-6 space-y-6">
            {/* Back button + header */}
            <div className="flex items-center gap-4">
                <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => navigate('/agents')}
                    className="text-brutal-slate hover:text-brutal-white"
                >
                    <ArrowLeft className="w-4 h-4 mr-1"/>
                    Back
                </Button>

                {isLoading ? (
                    <div className="flex items-center gap-2">
                        <Loader2 className="w-5 h-5 animate-spin text-brutal-slate"/>
                        <span className="text-brutal-slate">Loading…</span>
                    </div>
                ) : run ? (
                    <div className="flex items-center gap-3 flex-1">
                        <Bot className="w-5 h-5 text-signal-orange"/>
                        <h1 className="text-lg font-bold text-brutal-white tracking-wide">
                            {run.agent_name ?? 'Agent Run'}
                        </h1>
                        <StatusBadge status={run.status}/>
                        {run.model && (
                            <Badge variant="secondary" className="text-xs">{run.model}</Badge>
                        )}
                        {run.provider && (
                            <Badge variant="outline" className="text-xs text-brutal-slate border-brutal-zinc">
                                {run.provider}
                            </Badge>
                        )}
                        {run.track_mode && (
                            <Badge variant="outline" className="text-xs text-signal-cyan border-signal-cyan/40">
                                mode:{run.track_mode}
                            </Badge>
                        )}
                        {run.approval_status && (
                            <Badge variant="outline" className="text-xs text-signal-orange border-signal-orange/40">
                                approval:{run.approval_status}
                            </Badge>
                        )}
                    </div>
                ) : null}
            </div>

            {/* Stats cards */}
            <div className="grid grid-cols-2 lg:grid-cols-6 gap-4">
                {isLoading ? (
                    <>
                        <SkeletonCard/>
                        <SkeletonCard/>
                        <SkeletonCard/>
                        <SkeletonCard/>
                        <SkeletonCard/>
                        <SkeletonCard/>
                    </>
                ) : run ? (
                    <>
                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Cpu className="w-4 h-4 text-cyan-500"/>
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">INPUT TOKENS</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {run.input_tokens.toLocaleString()}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Cpu className="w-4 h-4 text-violet-500"/>
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">OUTPUT TOKENS</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {run.output_tokens.toLocaleString()}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <DollarSign className="w-4 h-4 text-signal-green"/>
                                    <span
                                        className="text-[10px] font-bold text-brutal-slate tracking-wider">TOTAL COST</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-signal-green font-mono">
                                    ${run.total_cost.toFixed(4)}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Clock className="w-4 h-4 text-signal-orange"/>
                                    <span
                                        className="text-[10px] font-bold text-brutal-slate tracking-wider">DURATION</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {durationMs !== null ? formatDuration(durationMs) : '—'}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Wrench className="w-4 h-4 text-brutal-slate"/>
                                    <span
                                        className="text-[10px] font-bold text-brutal-slate tracking-wider">TOOL CALLS</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {run.tool_call_count}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <ShieldCheck className="w-4 h-4 text-signal-orange"/>
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">APPROVAL</span>
                                </div>
                                <div className="text-base font-bold mt-1 text-brutal-white font-mono uppercase">
                                    {run.approval_status ?? 'not_required'}
                                </div>
                            </CardContent>
                        </Card>
                    </>
                ) : null}
            </div>

            {/* IDs */}
            {run && (
                <div className="flex items-center gap-6 text-sm">
                    <div className="flex items-center gap-2">
                        <span className="text-brutal-slate text-xs font-bold tracking-wider">RUN ID</span>
                        <CopyableText value={run.run_id} label="Run ID" truncate maxWidth="140px"/>
                    </div>
                    {run.trace_id && (
                        <div className="flex items-center gap-2">
                            <span className="text-brutal-slate text-xs font-bold tracking-wider">TRACE ID</span>
                            <CopyableText value={run.trace_id} label="Trace ID" truncate maxWidth="140px"
                                          textClassName="text-primary"/>
                        </div>
                    )}
                </div>
            )}

            {/* Agent Trace Tree */}
            {run && (
                <div>
                    <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em] mb-3">AGENT TRACE</h2>
                    <AgentTraceTree
                        run={run}
                        toolCalls={toolCalls}
                        isLoading={toolsLoading}
                    />
                </div>
            )}

            {/* Decisions / Evidence */}
            {run && (
                <div>
                    <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em] mb-3">DECISIONS</h2>
                    <Card>
                        <CardContent className="pt-4">
                            {decisionsLoading ? (
                                <div className="flex items-center gap-2 text-sm text-brutal-slate">
                                    <Loader2 className="w-4 h-4 animate-spin"/>
                                    Loading decisions…
                                </div>
                            ) : decisions.length === 0 ? (
                                <div className="text-sm text-brutal-slate">No decision events recorded for this run.</div>
                            ) : (
                                <div className="space-y-3">
                                    {decisions.map((decision) => (
                                        <div
                                            key={decision.decision_id}
                                            className="border border-brutal-zinc rounded p-3 bg-brutal-dark/30"
                                        >
                                            <div className="flex items-center gap-2 mb-2">
                                                <GitBranch className="w-3.5 h-3.5 text-signal-cyan"/>
                                                <span
                                                    className="text-xs font-bold uppercase tracking-wider text-brutal-white">
                                                    {decision.decision_type ?? 'decision'}
                                                </span>
                                                <Badge variant="outline"
                                                       className="text-[10px] border-brutal-zinc text-brutal-slate">
                                                    {decision.outcome ?? 'unknown'}
                                                </Badge>
                                                <Badge variant="outline"
                                                       className="text-[10px] border-signal-orange/40 text-signal-orange">
                                                    {decision.approval_status ?? 'not_required'}
                                                </Badge>
                                            </div>

                                            {decision.reason && (
                                                <p className="text-sm text-brutal-slate mb-2">{decision.reason}</p>
                                            )}

                                            {decision.evidence_json && (
                                                <div className="text-xs text-brutal-slate font-mono">
                                                    <div className="flex items-center gap-1 mb-1">
                                                        <Link2 className="w-3 h-3"/>
                                                        evidence
                                                    </div>
                                                    <TextVisualizer
                                                        content={decision.evidence_json}
                                                        label="Evidence Links"
                                                        defaultExpanded={false}
                                                        maxCollapsedHeight={80}
                                                        showTreeView={true}
                                                    />
                                                </div>
                                            )}
                                        </div>
                                    ))}
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </div>
            )}

            {/* Error message */}
            {run?.error_message && (
                <div>
                    <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em] mb-3">ERROR</h2>
                    <div
                        className="text-sm text-red-400 bg-red-500/10 border-2 border-red-500/30 rounded p-4 font-mono whitespace-pre-wrap">
                        {run.error_message}
                    </div>
                </div>
            )}
        </div>
    );
}
