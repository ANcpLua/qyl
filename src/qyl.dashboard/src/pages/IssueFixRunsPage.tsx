import {useState} from 'react';
import {useNavigate, useParams} from 'react-router-dom';
import {AlertCircle, ArrowLeft, ChevronDown, ChevronRight, Clock, Loader2, Play} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {DiffViewer} from '@/components/Loom/DiffViewer';
import {PipelineStatus} from '@/components/Loom/PipelineStatus';
import type {AutofixStep} from '@/hooks/use-Loom';
import {useFixRunSteps} from '@/hooks/use-Loom';
import type {FixRun} from '@/hooks/use-coding-agents';
import {useFixRuns} from '@/hooks/use-coding-agents';

const statusStyles: Record<string, string> = {
    pending: 'bg-brutal-zinc/20 text-brutal-slate border-brutal-zinc/40',
    running: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    completed: 'bg-green-500/20 text-green-400 border-green-500/40',
    failed: 'bg-red-500/20 text-red-400 border-red-500/40',
};

function formatTimestamp(iso?: string): string {
    if (!iso) return '\u2014';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    });
}

function SkeletonCard() {
    return (
        <Card>
            <CardContent className="pt-4">
                <div className="h-4 w-40 bg-brutal-zinc rounded animate-pulse mb-2"/>
                <div className="h-7 w-64 bg-brutal-zinc rounded animate-pulse"/>
            </CardContent>
        </Card>
    );
}

function StepDetail({step}: {step: AutofixStep}) {
    const [expanded, setExpanded] = useState(false);
    const Icon = expanded ? ChevronDown : ChevronRight;

    return (
        <div className="border border-brutal-zinc bg-brutal-dark/30">
            <button
                onClick={() => setExpanded(!expanded)}
                className="w-full flex items-center gap-2 px-3 py-2 text-left hover:bg-brutal-zinc/10"
            >
                <Icon className="w-3.5 h-3.5 text-brutal-slate flex-shrink-0"/>
                <span className="text-xs font-bold text-brutal-white uppercase tracking-wider">
                    {step.step_number}. {step.step_name}
                </span>
                <Badge
                    variant="outline"
                    className={cn('ml-auto text-[10px] uppercase', statusStyles[step.status] ?? statusStyles.pending)}
                >
                    {step.status}
                </Badge>
            </button>
            {expanded && (
                <div className="px-3 pb-3 space-y-2 border-t border-brutal-zinc">
                    {step.error_message && (
                        <div className="text-xs text-red-400 font-mono bg-red-500/10 p-2 mt-2">
                            {step.error_message}
                        </div>
                    )}
                    {step.input_json && (
                        <div className="mt-2">
                            <span className="text-[10px] font-bold text-brutal-slate tracking-wider">INPUT</span>
                            <pre className="text-xs text-brutal-slate font-mono bg-brutal-carbon p-2 mt-1 overflow-x-auto max-h-48 overflow-y-auto">
                                {step.input_json}
                            </pre>
                        </div>
                    )}
                    {step.output_json && (
                        <div>
                            <span className="text-[10px] font-bold text-brutal-slate tracking-wider">OUTPUT</span>
                            <pre className="text-xs text-brutal-slate font-mono bg-brutal-carbon p-2 mt-1 overflow-x-auto max-h-48 overflow-y-auto">
                                {step.output_json}
                            </pre>
                        </div>
                    )}
                    <div className="flex gap-4 text-[10px] text-brutal-zinc font-mono">
                        {step.started_at && <span>started {formatTimestamp(step.started_at)}</span>}
                        {step.completed_at && <span>completed {formatTimestamp(step.completed_at)}</span>}
                    </div>
                </div>
            )}
        </div>
    );
}

function FixRunCard({run, issueId}: {run: FixRun; issueId: string}) {
    const [showSteps, setShowSteps] = useState(false);
    const {data: steps = [], isLoading: stepsLoading} = useFixRunSteps(
        showSteps ? issueId : undefined,
        showSteps ? run.run_id : undefined,
    );

    const pipelineSteps = steps.map((s) => ({
        name: s.step_name,
        status: s.status as 'pending' | 'running' | 'completed' | 'failed',
    }));

    return (
        <Card>
            <CardContent className="pt-4 space-y-4">
                {/* Header row */}
                <div className="flex items-center gap-3">
                    <Play className="w-4 h-4 text-signal-orange flex-shrink-0"/>
                    <span className="font-mono text-xs text-brutal-white">
                        {run.run_id.slice(0, 12)}...
                    </span>
                    <Badge
                        variant="outline"
                        className={cn('uppercase tracking-wider', statusStyles[run.status] ?? statusStyles.pending)}
                    >
                        {run.status}
                    </Badge>
                    {run.confidence_score !== undefined && run.confidence_score !== null && (
                        <span className="text-xs font-mono text-brutal-slate">
                            {Math.round(run.confidence_score * 100)}% confidence
                        </span>
                    )}
                    <Badge variant="outline" className="text-[10px] text-brutal-slate border-brutal-zinc ml-auto">
                        {run.policy}
                    </Badge>
                </div>

                {/* Description */}
                {run.fix_description && (
                    <p className="text-sm text-brutal-slate">{run.fix_description}</p>
                )}

                {/* Timestamps */}
                <div className="flex gap-4 text-[10px] text-brutal-zinc font-mono">
                    <div className="flex items-center gap-1">
                        <Clock className="w-3 h-3"/>
                        created {formatTimestamp(run.created_at)}
                    </div>
                    {run.completed_at && (
                        <div className="flex items-center gap-1">
                            <Clock className="w-3 h-3"/>
                            completed {formatTimestamp(run.completed_at)}
                        </div>
                    )}
                </div>

                {/* Steps toggle */}
                <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setShowSteps(!showSteps)}
                    className="text-[10px] font-bold tracking-wider text-brutal-slate hover:text-brutal-white px-0"
                >
                    {showSteps ? <ChevronDown className="w-3.5 h-3.5 mr-1"/> : <ChevronRight className="w-3.5 h-3.5 mr-1"/>}
                    STEPS
                </Button>

                {showSteps && (
                    <div className="space-y-1">
                        {stepsLoading ? (
                            <div className="flex items-center gap-2 py-2 text-sm text-brutal-slate">
                                <Loader2 className="w-4 h-4 animate-spin"/>
                                Loading steps...
                            </div>
                        ) : steps.length === 0 ? (
                            <p className="text-xs text-brutal-zinc py-2">No steps recorded</p>
                        ) : (
                            <>
                                <PipelineStatus steps={pipelineSteps} className="mb-3"/>
                                {steps.map((step) => (
                                    <StepDetail key={step.step_id} step={step}/>
                                ))}
                            </>
                        )}
                    </div>
                )}

                {/* Diff viewer */}
                {run.changes_json && (
                    <div>
                        <h3 className="text-[10px] font-bold text-brutal-slate tracking-wider mb-2">CHANGES</h3>
                        <DiffViewer diff={run.changes_json}/>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}

export function IssueFixRunsPage() {
    const {issueId} = useParams<{issueId: string}>();
    const navigate = useNavigate();

    const {data: fixRuns = [], isLoading, error} = useFixRuns(issueId);

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load fix runs</p>
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
            {/* Back button + header */}
            <div className="flex items-center gap-4">
                <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => navigate(`/issues/${issueId}`)}
                    className="text-brutal-slate hover:text-brutal-white"
                >
                    <ArrowLeft className="w-4 h-4 mr-1"/>
                    Back
                </Button>
                <h1 className="text-xs font-bold text-brutal-slate tracking-[0.3em]">FIX RUNS</h1>
                {issueId && (
                    <span className="font-mono text-[10px] text-brutal-slate">
                        {issueId.slice(0, 12)}...
                    </span>
                )}
            </div>

            {/* Loading state */}
            {isLoading && (
                <div className="space-y-4">
                    <SkeletonCard/>
                    <SkeletonCard/>
                </div>
            )}

            {/* Empty state */}
            {!isLoading && fixRuns.length === 0 && (
                <Card>
                    <CardContent className="py-12 text-center">
                        <Play className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">No fix runs found for this issue</p>
                        <p className="text-brutal-zinc text-xs mt-1">Fix runs are created when Loom attempts to resolve an issue</p>
                    </CardContent>
                </Card>
            )}

            {/* Fix run list */}
            {fixRuns.length > 0 && (
                <div className="space-y-4">
                    {fixRuns.map((run) => (
                        <FixRunCard key={run.run_id} run={run} issueId={issueId!}/>
                    ))}
                </div>
            )}
        </div>
    );
}
