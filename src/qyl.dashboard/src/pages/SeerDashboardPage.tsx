import {useNavigate} from 'react-router-dom';
import {Activity, AlertCircle, Bot, ChevronRight, GitPullRequest, RefreshCw, Zap} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {FixabilityBadge} from '@/components/Loom/FixabilityBadge';
import type {GitHubEvent, TriageResult} from '@/hooks/use-Loom';
import {useGitHubEvents, usePendingHandoffs, useRegressions, useTriageResults} from '@/hooks/use-Loom';

function formatTimestamp(iso?: string): string {
    if (!iso) return '--';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
    });
}

function truncate(text: string | undefined, max: number): string {
    if (!text) return '--';
    return text.length > max ? text.slice(0, max) + '...' : text;
}

const eventTypeStyles: Record<string, string> = {
    push: 'bg-green-500/20 text-green-400 border-green-500/40',
    pull_request: 'bg-purple-500/20 text-purple-400 border-purple-500/40',
    issues: 'bg-red-500/20 text-red-400 border-red-500/40',
    issue_comment: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    pull_request_review: 'bg-purple-500/20 text-purple-400 border-purple-500/40',
};

function SkeletonRows({count, widths}: { count: number; widths: string[] }) {
    return Array.from({length: count}, (_, i) => (
        <div key={i} className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            {widths.map((w, j) => <div key={j} className={cn('h-4 bg-brutal-zinc', w)}/>)}
        </div>
    ));
}

function EmptyState({title, subtitle}: { title: string; subtitle: string }) {
    return (
        <div className="py-12 text-center">
            <AlertCircle className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
            <p className="text-brutal-slate text-sm">{title}</p>
            <p className="text-brutal-zinc text-xs mt-1">{subtitle}</p>
        </div>
    );
}

function StatCard({icon, label, value, color, loading}: {
    icon: React.ReactNode; label: string; value: number; color: string; loading: boolean;
}) {
    if (loading) {
        return (
            <Card>
                <CardContent className="pt-4">
                    <div className="flex items-center gap-2 animate-pulse">
                        <div className="w-4 h-4 bg-brutal-zinc"/>
                        <div className="w-24 h-3 bg-brutal-zinc"/>
                    </div>
                    <div className="w-12 h-7 bg-brutal-zinc mt-2 animate-pulse"/>
                </CardContent>
            </Card>
        );
    }
    return (
        <Card>
            <CardContent className="pt-4">
                <div className="flex items-center gap-2">
                    {icon}
                    <span className="text-xs font-bold text-brutal-slate tracking-wider">{label}</span>
                </div>
                <div className={cn('text-2xl font-bold mt-1', color)}>{value}</div>
            </CardContent>
        </Card>
    );
}

function TriageRow({result, onClick}: { result: TriageResult; onClick: () => void }) {
    return (
        <div
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            role="button"
            tabIndex={0}
            onClick={onClick}
            onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onClick(); }
            }}
        >
            <div className="w-28 min-w-0">
                <span className="text-sm font-bold text-brutal-white truncate block font-mono">
                    {truncate(result.issue_id, 12)}
                </span>
            </div>
            <div className="w-16">
                <span className="font-mono text-xs text-brutal-slate">
                    {Math.round(result.fixability_score * 100)}%
                </span>
            </div>
            <div className="w-20">
                <FixabilityBadge score={result.fixability_score} automationLevel={result.automation_level}/>
            </div>
            <div className="flex-1 min-w-0">
                <span className="text-xs text-brutal-slate truncate block">{truncate(result.ai_summary, 60)}</span>
            </div>
            <div className="w-20">
                <span className="text-xs text-brutal-slate truncate block">{result.triggered_by ?? '--'}</span>
            </div>
            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">{formatTimestamp(result.created_at)}</span>
            </div>
            <ChevronRight className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

function EventRow({event}: { event: GitHubEvent }) {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc">
            <div className="w-24">
                <Badge variant="outline" className={cn(
                    'text-[10px] uppercase tracking-wider',
                    eventTypeStyles[event.eventType] ?? 'bg-brutal-zinc/20 text-brutal-slate border-brutal-zinc/40',
                )}>
                    {event.eventType.replace(/_/g, ' ')}
                </Badge>
            </div>
            <div className="w-40 min-w-0">
                <span className="text-xs font-bold text-brutal-white truncate block">{event.repoFullName}</span>
            </div>
            <div className="w-20">
                <span className="text-xs text-brutal-slate">{event.action ?? '--'}</span>
            </div>
            <div className="w-20">
                <span className="text-xs text-brutal-slate truncate block">{event.sender ?? '--'}</span>
            </div>
            <div className="flex-1 text-right">
                <span className="font-mono text-xs text-brutal-slate">{formatTimestamp(event.createdAt)}</span>
            </div>
        </div>
    );
}

const triageWidths = ['w-28', 'w-16', 'w-20', 'flex-1', 'w-20', 'w-28', 'w-4'];
const eventWidths = ['w-24', 'w-40', 'w-20', 'w-20', 'w-28'];

const stats = [
    {icon: <Activity className="w-4 h-4 text-signal-orange"/>, label: 'TRIAGE RESULTS', color: 'text-signal-orange', key: 'triage'},
    {icon: <Zap className="w-4 h-4 text-green-400"/>, label: 'AUTO-FIX ELIGIBLE', color: 'text-green-400', key: 'auto'},
    {icon: <Bot className="w-4 h-4 text-amber-400"/>, label: 'PENDING HANDOFFS', color: 'text-amber-400', key: 'pending'},
    {icon: <RefreshCw className="w-4 h-4 text-red-400"/>, label: 'REGRESSIONS', color: 'text-red-400', key: 'regressions'},
] as const;

export function LoomDashboardPage() {
    const navigate = useNavigate();
    const {data: triageResults, isLoading: triageLoading, error: triageError} = useTriageResults(20);
    const {data: pendingHandoffs, isLoading: handoffsLoading} = usePendingHandoffs();
    const {data: regressions, isLoading: regressionsLoading} = useRegressions(50);
    const {data: githubEvents, isLoading: eventsLoading} = useGitHubEvents(10);

    const statsLoading = triageLoading || handoffsLoading || regressionsLoading;
    const statValues: Record<string, number> = {
        triage: triageResults?.length ?? 0,
        auto: triageResults?.filter((t) => t.automation_level === 'auto').length ?? 0,
        pending: pendingHandoffs?.length ?? 0,
        regressions: regressions?.length ?? 0,
    };

    if (triageError) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load Loom pipeline data</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {triageError instanceof Error ? triageError.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="space-y-1">
                <h1 className="text-lg font-bold text-brutal-white tracking-wider uppercase">Loom</h1>
                <p className="text-xs text-brutal-slate tracking-wider">AI Debugging Pipeline</p>
            </div>

            {/* Stat cards */}
            <div className="grid grid-cols-4 gap-4">
                {stats.map((s) => (
                    <StatCard key={s.key} icon={s.icon} label={s.label} color={s.color}
                              value={statValues[s.key]} loading={statsLoading}/>
                ))}
            </div>

            {/* Recent Triage Results */}
            <div>
                <span className="text-xs font-bold tracking-[0.3em] text-brutal-slate mb-3 block">RECENT TRIAGE RESULTS</span>
                <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                    <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                        <div className="w-28">ISSUE ID</div>
                        <div className="w-16">SCORE</div>
                        <div className="w-20">LEVEL</div>
                        <div className="flex-1">AI SUMMARY</div>
                        <div className="w-20">TRIGGERED</div>
                        <div className="w-28 text-right">CREATED</div>
                        <div className="w-4"/>
                    </div>
                    {triageLoading ? (
                        <SkeletonRows count={5} widths={triageWidths}/>
                    ) : !triageResults || triageResults.length === 0 ? (
                        <EmptyState title="No triage results yet" subtitle="Results will appear as the AI pipeline processes issues"/>
                    ) : (
                        triageResults.map((r) => (
                            <TriageRow key={r.triage_id} result={r} onClick={() => navigate(`/issues/${r.issue_id}`)}/>
                        ))
                    )}
                </div>
            </div>

            {/* Pipeline Activity */}
            <div>
                <div className="flex items-center gap-2 mb-3">
                    <GitPullRequest className="w-4 h-4 text-brutal-slate"/>
                    <span className="text-xs font-bold tracking-[0.3em] text-brutal-slate">PIPELINE ACTIVITY</span>
                </div>
                <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                    <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                        <div className="w-24">EVENT</div>
                        <div className="w-40">REPO</div>
                        <div className="w-20">ACTION</div>
                        <div className="w-20">SENDER</div>
                        <div className="flex-1 text-right">TIME</div>
                    </div>
                    {eventsLoading ? (
                        <SkeletonRows count={5} widths={eventWidths}/>
                    ) : !githubEvents || githubEvents.length === 0 ? (
                        <EmptyState title="No GitHub events" subtitle="Events will appear when GitHub webhooks are configured"/>
                    ) : (
                        githubEvents.map((e) => <EventRow key={e.eventId} event={e}/>)
                    )}
                </div>
            </div>
        </div>
    );
}
