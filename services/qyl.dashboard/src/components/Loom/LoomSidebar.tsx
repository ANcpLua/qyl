import {Brain, ChevronRight, ExternalLink, Loader2, Sparkles} from 'lucide-react';
import {Link} from 'react-router-dom';
import {cn} from '@/lib/utils';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {FixabilityBadge} from './FixabilityBadge';
import {PipelineStatus} from './PipelineStatus';
import type {AutofixStep, TriageResult} from '@/hooks/use-Loom';
import {useFixRunSteps, useTriageResult} from '@/hooks/use-Loom';
import {useFixRuns} from '@/hooks/use-coding-agents';

interface LoomSidebarProps {
    issueId: string;
    className?: string;
}

function StepToStatus(step: AutofixStep): 'pending' | 'running' | 'completed' | 'failed' {
    if (step.status === 'completed') return 'completed';
    if (step.status === 'running' || step.status === 'in_progress') return 'running';
    if (step.status === 'failed' || step.status === 'error') return 'failed';
    return 'pending';
}

function TriageSection({triage}: { triage: TriageResult }) {
    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between">
                <span className="text-[10px] font-bold text-brutal-slate tracking-[0.2em]">TRIAGE</span>
                <FixabilityBadge score={triage.fixability_score} automationLevel={triage.automation_level}/>
            </div>
            {triage.ai_summary && (
                <div className="text-sm text-brutal-white/80 leading-relaxed border-l-2 border-signal-violet/40 pl-3">
                    {triage.ai_summary}
                </div>
            )}
        </div>
    );
}

function AutofixSection({steps, issueId, runId}: { steps: AutofixStep[]; issueId: string; runId: string }) {
    const pipelineSteps = steps.map((s) => ({name: s.step_name, status: StepToStatus(s)}));
    const isRunning = steps.some((s) => s.status === 'running' || s.status === 'in_progress');
    const latestOutput = [...steps].reverse().find((s) => s.output_json)?.output_json;

    let summary: string | undefined;
    if (latestOutput) {
        try {
            const parsed = JSON.parse(latestOutput);
            summary = parsed.summary ?? parsed.root_cause ?? parsed.recommendation;
        } catch { /* ignore */
        }
    }

    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between">
                <span className="text-[10px] font-bold text-brutal-slate tracking-[0.2em]">AUTOFIX PIPELINE</span>
                {isRunning && <Loader2 className="w-3.5 h-3.5 animate-spin text-signal-yellow"/>}
            </div>
            <PipelineStatus steps={pipelineSteps}/>
            {summary && (
                <div className="text-sm text-brutal-white/80 leading-relaxed border-l-2 border-signal-cyan/40 pl-3">
                    {summary}
                </div>
            )}
            <Link
                to={`/loom?issueId=${issueId}&runId=${runId}`}
                className="inline-flex items-center gap-1 text-xs text-primary hover:underline"
            >
                View full analysis <ExternalLink className="w-3 h-3"/>
            </Link>
        </div>
    );
}

export function LoomSidebar({issueId, className}: LoomSidebarProps) {
    const {data: triage, isLoading: triageLoading} = useTriageResult(issueId);
    const {data: fixRuns = []} = useFixRuns(issueId);
    const latestRun = fixRuns[0];
    const {data: steps = []} = useFixRunSteps(issueId, latestRun?.run_id);

    const hasData = triage || steps.length > 0;
    const isLoading = triageLoading;

    return (
        <div className={cn('w-80 border-l-2 border-brutal-zinc bg-brutal-carbon flex flex-col', className)}>
            {/* Header */}
            <div className="flex items-center gap-2 px-4 py-3 border-b border-brutal-zinc">
                <Brain className="w-4 h-4 text-signal-violet"/>
                <span className="text-xs font-bold tracking-[0.2em] text-brutal-slate">LOOM</span>
                <Badge variant="outline" className="ml-auto text-[10px] border-signal-violet/40 text-signal-violet">
                    AI
                </Badge>
            </div>

            {/* Content */}
            <div className="flex-1 overflow-auto p-4 space-y-6">
                {isLoading ? (
                    <div className="flex flex-col items-center gap-3 py-8">
                        <Loader2 className="w-6 h-6 animate-spin text-signal-violet"/>
                        <span className="text-xs text-brutal-slate">Analyzing issue…</span>
                    </div>
                ) : !hasData ? (
                    <div className="flex flex-col items-center gap-3 py-8 text-center">
                        <Sparkles className="w-8 h-8 text-brutal-zinc"/>
                        <p className="text-sm text-brutal-slate">No Loom analysis yet</p>
                        <p className="text-xs text-brutal-zinc">Triage this issue to start AI analysis</p>
                        <Link to={`/issues/${issueId}/triage`}>
                            <Button size="sm" variant="outline" className="text-xs">
                                <ChevronRight className="w-3 h-3 mr-1"/>
                                Run Triage
                            </Button>
                        </Link>
                    </div>
                ) : (
                    <>
                        {triage && <TriageSection triage={triage}/>}
                        {steps.length > 0 && latestRun && (
                            <>
                                <div className="h-px bg-brutal-zinc"/>
                                <AutofixSection steps={steps} issueId={issueId} runId={latestRun.run_id}/>
                            </>
                        )}
                    </>
                )}
            </div>

            {/* Provenance footer */}
            <div className="px-4 py-2 border-t border-brutal-zinc">
                <p className="text-[10px] text-brutal-zinc italic">AI-generated analysis — verify against raw
                    telemetry</p>
            </div>
        </div>
    );
}
