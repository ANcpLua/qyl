import {useState} from 'react';
import {Link, useNavigate, useParams} from 'react-router-dom';
import {
    AlertCircle,
    ArrowLeft,
    ArrowRight,
    CheckCircle2,
    Clock,
    Eye,
    Loader2,
    RefreshCw,
    RotateCcw,
    User,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {CopyableText} from '@/components/ui';
import type {IssueEvent} from '@/hooks/use-issues';
import {useAssignIssue, useIssue, useIssueEvents, useUpdateIssueStatus} from '@/hooks/use-issues';

const statusStyles: Record<string, string> = {
    new: 'bg-red-500/20 text-red-400 border-red-500/40',
    acknowledged: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    resolved: 'bg-green-500/20 text-green-400 border-green-500/40',
    regressed: 'bg-purple-500/20 text-purple-400 border-purple-500/40',
    reopened: 'bg-orange-500/20 text-orange-400 border-orange-500/40',
};

function StatusBadge({status}: { status: string }) {
    return (
        <Badge variant="outline" className={cn('uppercase tracking-wider', statusStyles[status] ?? statusStyles.new)}>
            {status}
        </Badge>
    );
}

function formatTimestamp(iso?: string): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    });
}

const statusTransitions: Record<string, { label: string; target: string; icon: typeof Eye }[]> = {
    new: [{label: 'Acknowledge', target: 'acknowledged', icon: Eye}],
    acknowledged: [{label: 'Resolve', target: 'resolved', icon: CheckCircle2}],
    resolved: [{label: 'Reopen', target: 'reopened', icon: RotateCcw}],
    regressed: [
        {label: 'Acknowledge', target: 'acknowledged', icon: Eye},
        {label: 'Resolve', target: 'resolved', icon: CheckCircle2},
    ],
    reopened: [
        {label: 'Acknowledge', target: 'acknowledged', icon: Eye},
        {label: 'Resolve', target: 'resolved', icon: CheckCircle2},
    ],
};

const eventTypeIcons: Record<string, typeof AlertCircle> = {
    status_change: RefreshCw,
    occurrence: AlertCircle,
    assignment: User,
};

function EventRow({event}: { event: IssueEvent }) {
    const Icon = eventTypeIcons[event.event_type] ?? Clock;

    return (
        <div className="flex items-start gap-3 px-4 py-3 border-b border-brutal-zinc">
            <div className="mt-0.5">
                <Icon className="w-4 h-4 text-brutal-slate"/>
            </div>
            <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                    <span className="text-xs font-bold text-brutal-white uppercase tracking-wider">
                        {event.event_type.replace('_', ' ')}
                    </span>
                    {event.actor && (
                        <span className="text-xs text-brutal-slate">by {event.actor}</span>
                    )}
                </div>
                {event.old_value && event.new_value && (
                    <div className="flex items-center gap-2 mt-1 text-xs">
                        <span className="text-brutal-slate">{event.old_value}</span>
                        <ArrowRight className="w-3 h-3 text-brutal-zinc"/>
                        <span className="text-brutal-white">{event.new_value}</span>
                    </div>
                )}
                {event.reason && (
                    <p className="text-xs text-brutal-slate mt-1">{event.reason}</p>
                )}
                {event.trace_id && (
                    <Link
                        to={`/traces?traceId=${event.trace_id}`}
                        className="text-xs text-signal-orange hover:underline mt-1 inline-block"
                    >
                        View trace →
                    </Link>
                )}
            </div>
            <span className="font-mono text-[10px] text-brutal-slate flex-shrink-0">
                {formatTimestamp(event.timestamp)}
            </span>
        </div>
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

export function IssueDetailPage() {
    const {issueId} = useParams<{ issueId: string }>();
    const navigate = useNavigate();
    const [assignInput, setAssignInput] = useState('');

    const {data: issue, isLoading: issueLoading, error: issueError} = useIssue(issueId ?? '');
    const {data: events = [], isLoading: eventsLoading} = useIssueEvents(issueId ?? '');
    const updateStatus = useUpdateIssueStatus();
    const assignIssue = useAssignIssue();

    if (issueError) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load issue</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {issueError instanceof Error ? issueError.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    const isLoading = issueLoading;
    const transitions = issue ? (statusTransitions[issue.status] ?? []) : [];

    return (
        <div className="p-6 space-y-6">
            {/* Back button + header */}
            <div className="flex items-center gap-4">
                <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => navigate('/issues')}
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
                ) : issue ? (
                    <div className="flex items-center gap-3 flex-1">
                        <AlertCircle className="w-5 h-5 text-red-400"/>
                        <h1 className="text-lg font-bold text-brutal-white tracking-wide">
                            {issue.error_type}
                        </h1>
                        <StatusBadge status={issue.status}/>
                        {issue.owner && (
                            <Badge variant="outline" className="text-xs text-brutal-slate border-brutal-zinc">
                                {issue.owner}
                            </Badge>
                        )}
                    </div>
                ) : null}
            </div>

            {/* Error message */}
            {issue?.message && (
                <div
                    className="text-sm text-brutal-slate bg-brutal-dark border-2 border-brutal-zinc rounded p-4 font-mono whitespace-pre-wrap">
                    {issue.message}
                </div>
            )}

            {/* Stats cards */}
            <div className="grid grid-cols-3 gap-4">
                {isLoading ? (
                    <>
                        <SkeletonCard/>
                        <SkeletonCard/>
                        <SkeletonCard/>
                    </>
                ) : issue ? (
                    <>
                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <AlertCircle className="w-4 h-4 text-red-400"/>
                                    <span
                                        className="text-[10px] font-bold text-brutal-slate tracking-wider">EVENTS</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {issue.event_count.toLocaleString()}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Clock className="w-4 h-4 text-brutal-slate"/>
                                    <span
                                        className="text-[10px] font-bold text-brutal-slate tracking-wider">FIRST SEEN</span>
                                </div>
                                <div className="text-sm font-bold mt-1 text-brutal-white font-mono">
                                    {formatTimestamp(issue.first_seen)}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Clock className="w-4 h-4 text-signal-orange"/>
                                    <span
                                        className="text-[10px] font-bold text-brutal-slate tracking-wider">LAST SEEN</span>
                                </div>
                                <div className="text-sm font-bold mt-1 text-brutal-white font-mono">
                                    {formatTimestamp(issue.last_seen)}
                                </div>
                            </CardContent>
                        </Card>
                    </>
                ) : null}
            </div>

            {/* IDs */}
            {issue && (
                <div className="flex items-center gap-6 text-sm">
                    <div className="flex items-center gap-2">
                        <span className="text-brutal-slate text-xs font-bold tracking-wider">ISSUE ID</span>
                        <CopyableText value={issue.issue_id} label="Issue ID" truncate maxWidth="140px"/>
                    </div>
                    {issue.trace_id && (
                        <div className="flex items-center gap-2">
                            <span className="text-brutal-slate text-xs font-bold tracking-wider">TRACE ID</span>
                            <CopyableText value={issue.trace_id} label="Trace ID" truncate maxWidth="140px"
                                          textClassName="text-primary"/>
                        </div>
                    )}
                </div>
            )}

            {/* Actions */}
            {issue && (
                <div className="flex items-center gap-3">
                    {transitions.map(({label, target, icon: BtnIcon}) => (
                        <Button
                            key={target}
                            size="sm"
                            variant="outline"
                            disabled={updateStatus.isPending}
                            onClick={() => updateStatus.mutate({issueId: issue.issue_id, status: target})}
                            className="text-xs font-bold tracking-wider"
                        >
                            <BtnIcon className="w-4 h-4 mr-1"/>
                            {label}
                        </Button>
                    ))}

                    <div className="flex items-center gap-2 ml-auto">
                        <Input
                            placeholder="Assign to…"
                            value={assignInput}
                            onChange={(e) => setAssignInput(e.target.value)}
                            className="w-40 h-8 text-xs"
                            aria-label="Assign owner"
                        />
                        <Button
                            size="sm"
                            variant="outline"
                            disabled={!assignInput.trim() || assignIssue.isPending}
                            onClick={() => {
                                assignIssue.mutate({issueId: issue.issue_id, owner: assignInput.trim()});
                                setAssignInput('');
                            }}
                            className="text-xs font-bold tracking-wider"
                        >
                            <User className="w-4 h-4 mr-1"/>
                            Assign
                        </Button>
                    </div>
                </div>
            )}

            {/* Event timeline */}
            <div>
                <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em] mb-3">EVENT TIMELINE</h2>
                <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                    {eventsLoading ? (
                        <div className="py-8 text-center">
                            <Loader2 className="w-6 h-6 mx-auto animate-spin text-brutal-slate"/>
                        </div>
                    ) : events.length === 0 ? (
                        <div className="py-8 text-center">
                            <Clock className="w-8 h-8 mx-auto mb-2 text-brutal-zinc"/>
                            <p className="text-brutal-slate text-sm">No events recorded</p>
                        </div>
                    ) : (
                        events.map((event) => (
                            <EventRow key={event.event_id} event={event}/>
                        ))
                    )}
                </div>
            </div>
        </div>
    );
}
