import {useNavigate, useParams} from 'react-router-dom';
import {AlertCircle, ArrowLeft, Brain, Clock, Sparkles, User} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {FixabilityBadge} from '@/components/seer/FixabilityBadge';
import {useTriageResult} from '@/hooks/use-seer';

const automationStyles: Record<string, string> = {
    auto: 'bg-green-500/20 text-green-400 border-green-500/40',
    assisted: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    manual: 'bg-brutal-zinc/20 text-brutal-slate border-brutal-zinc/40',
    skip: 'bg-red-500/20 text-red-400 border-red-500/40',
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
                <div className="h-4 w-20 bg-brutal-zinc rounded animate-pulse mb-2"/>
                <div className="h-7 w-32 bg-brutal-zinc rounded animate-pulse"/>
            </CardContent>
        </Card>
    );
}

export function IssueTriagePage() {
    const {issueId} = useParams<{issueId: string}>();
    const navigate = useNavigate();

    const {data: triage, isLoading, error} = useTriageResult(issueId);

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load triage assessment</p>
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
                <h1 className="text-xs font-bold text-brutal-slate tracking-[0.3em]">TRIAGE ASSESSMENT</h1>
                {issueId && (
                    <span className="font-mono text-[10px] text-brutal-slate">
                        {issueId.slice(0, 12)}...
                    </span>
                )}
            </div>

            {/* Loading state */}
            {isLoading && (
                <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
                    <SkeletonCard/>
                    <SkeletonCard/>
                    <SkeletonCard/>
                    <SkeletonCard/>
                </div>
            )}

            {/* Not found state */}
            {!isLoading && !triage && (
                <Card>
                    <CardContent className="py-12 text-center">
                        <Brain className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">No triage assessment found for this issue</p>
                        <p className="text-brutal-zinc text-xs mt-1">Triage runs automatically when new issues are detected</p>
                    </CardContent>
                </Card>
            )}

            {/* Main triage content */}
            {triage && (
                <>
                    {/* Fixability + Automation cards */}
                    <Card>
                        <CardContent className="pt-6 pb-6">
                            <div className="flex items-start gap-8">
                                {/* Fixability score */}
                                <div className="flex flex-col items-center gap-2">
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">
                                        FIXABILITY
                                    </span>
                                    <div className="text-4xl font-bold text-brutal-white font-mono">
                                        {Math.round(triage.fixability_score * 100)}%
                                    </div>
                                    <FixabilityBadge
                                        score={triage.fixability_score}
                                        automationLevel={triage.automation_level}
                                    />
                                </div>

                                {/* Divider */}
                                <div className="w-px h-24 bg-brutal-zinc self-center"/>

                                {/* Automation level */}
                                <div className="flex flex-col gap-2">
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">
                                        AUTOMATION LEVEL
                                    </span>
                                    <Badge
                                        variant="outline"
                                        className={cn(
                                            'uppercase tracking-wider text-sm px-3 py-1 w-fit',
                                            automationStyles[triage.automation_level] ?? automationStyles.skip,
                                        )}
                                    >
                                        {triage.automation_level}
                                    </Badge>
                                </div>

                                {/* Meta info */}
                                <div className="flex flex-col gap-3 ml-auto">
                                    {triage.triggered_by && (
                                        <div className="flex items-center gap-2">
                                            <User className="w-3.5 h-3.5 text-brutal-slate"/>
                                            <span className="text-[10px] font-bold text-brutal-slate tracking-wider">
                                                TRIGGERED BY
                                            </span>
                                            <span className="text-xs text-brutal-white font-mono">
                                                {triage.triggered_by}
                                            </span>
                                        </div>
                                    )}
                                    <div className="flex items-center gap-2">
                                        <Clock className="w-3.5 h-3.5 text-brutal-slate"/>
                                        <span className="text-[10px] font-bold text-brutal-slate tracking-wider">
                                            CREATED AT
                                        </span>
                                        <span className="text-xs text-brutal-white font-mono">
                                            {formatTimestamp(triage.created_at)}
                                        </span>
                                    </div>
                                </div>
                            </div>
                        </CardContent>
                    </Card>

                    {/* AI Summary */}
                    {triage.ai_summary && (
                        <div>
                            <div className="flex items-center gap-2 mb-3">
                                <Sparkles className="w-4 h-4 text-signal-orange"/>
                                <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em]">AI SUMMARY</h2>
                            </div>
                            <div className="border-2 border-brutal-zinc bg-brutal-dark p-4 font-mono text-sm text-brutal-white whitespace-pre-wrap leading-relaxed">
                                {triage.ai_summary}
                            </div>
                        </div>
                    )}

                    {/* Triage ID */}
                    <div className="flex items-center gap-2 text-xs">
                        <span className="text-brutal-slate font-bold tracking-wider">TRIAGE ID</span>
                        <span className="font-mono text-brutal-zinc">{triage.triage_id}</span>
                    </div>
                </>
            )}
        </div>
    );
}
