import {useState} from 'react';
import {AlertCircle, Code2, FileCode, GitPullRequest, Loader2, Send} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {useGitHubEvents, useTriggerCodeReview} from '@/hooks/use-seer';
import type {CodeReviewComment, CodeReviewResult} from '@/hooks/use-seer';

const severityStyles: Record<string, string> = {
    critical: 'bg-red-500/20 text-red-400 border-red-500/40',
    warning: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    suggestion: 'bg-cyan-500/20 text-cyan-400 border-cyan-500/40',
};

const severityOrder: Record<string, number> = {
    critical: 0,
    warning: 1,
    suggestion: 2,
};

const eventTypeStyles: Record<string, string> = {
    pull_request: 'bg-purple-500/20 text-purple-400 border-purple-500/40',
    push: 'bg-green-500/20 text-green-400 border-green-500/40',
    issues: 'bg-red-500/20 text-red-400 border-red-500/40',
    issue_comment: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    pull_request_review: 'bg-cyan-500/20 text-cyan-400 border-cyan-500/40',
};

function formatTimestamp(iso?: string): string {
    if (!iso) return '\u2014';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
    });
}

function SeverityBadge({severity}: { severity: string }) {
    return (
        <Badge variant="outline"
               className={cn('text-[10px] uppercase tracking-wider', severityStyles[severity] ?? severityStyles.suggestion)}>
            {severity}
        </Badge>
    );
}

function EventTypeBadge({eventType}: { eventType: string }) {
    return (
        <Badge variant="outline"
               className={cn('text-[10px] uppercase tracking-wider', eventTypeStyles[eventType] ?? 'bg-brutal-zinc/20 text-brutal-slate border-brutal-zinc')}>
            {eventType.replace(/_/g, ' ')}
        </Badge>
    );
}

function ReviewComment({comment}: { comment: CodeReviewComment }) {
    return (
        <div className="border border-brutal-zinc bg-brutal-dark/30 p-4 space-y-2">
            <div className="flex items-center gap-3">
                <SeverityBadge severity={comment.severity}/>
                <div className="flex items-center gap-2 text-xs text-brutal-slate">
                    <FileCode className="w-3 h-3"/>
                    <span className="font-mono">{comment.file}</span>
                    <span className="text-brutal-zinc">:</span>
                    <span className="font-mono">{comment.line}</span>
                </div>
            </div>
            <p className="text-sm text-brutal-white">{comment.comment}</p>
            {comment.suggestion && (
                <pre className="text-xs font-mono bg-brutal-carbon border border-brutal-zinc p-3 text-signal-green overflow-x-auto whitespace-pre-wrap">
                    {comment.suggestion}
                </pre>
            )}
        </div>
    );
}

function ReviewResults({result}: { result: CodeReviewResult }) {
    const sortedComments = [...result.comments].sort(
        (a, b) => (severityOrder[a.severity] ?? 99) - (severityOrder[b.severity] ?? 99),
    );

    const criticalCount = result.comments.filter((c) => c.severity === 'critical').length;
    const warningCount = result.comments.filter((c) => c.severity === 'warning').length;
    const suggestionCount = result.comments.filter((c) => c.severity === 'suggestion').length;

    return (
        <Card>
            <CardContent className="pt-4 space-y-4">
                <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                        <Code2 className="w-4 h-4 text-signal-orange"/>
                        <span className="text-xs font-bold tracking-[0.3em] text-brutal-slate">REVIEW RESULTS</span>
                    </div>
                    <div className="flex items-center gap-2 text-xs text-brutal-slate">
                        <span className="font-mono">{result.repoFullName}</span>
                        <span className="text-brutal-zinc">#</span>
                        <span className="font-mono">{result.prNumber}</span>
                    </div>
                </div>

                <div className="flex items-center gap-4">
                    {criticalCount > 0 && (
                        <span className="text-xs font-bold text-red-400">{criticalCount} critical</span>
                    )}
                    {warningCount > 0 && (
                        <span className="text-xs font-bold text-amber-400">{warningCount} warning{warningCount !== 1 ? 's' : ''}</span>
                    )}
                    {suggestionCount > 0 && (
                        <span className="text-xs font-bold text-cyan-400">{suggestionCount} suggestion{suggestionCount !== 1 ? 's' : ''}</span>
                    )}
                    {result.comments.length === 0 && (
                        <span className="text-xs text-signal-green font-bold">No issues found</span>
                    )}
                </div>

                {sortedComments.length > 0 && (
                    <div className="space-y-3">
                        {sortedComments.map((comment, index) => (
                            <ReviewComment key={`${comment.file}-${comment.line}-${index}`} comment={comment}/>
                        ))}
                    </div>
                )}
            </CardContent>
        </Card>
    );
}

function SkeletonRow() {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            <div className="w-32 h-5 bg-brutal-zinc rounded"/>
            <div className="w-20 h-4 bg-brutal-zinc rounded"/>
            <div className="flex-1 h-4 bg-brutal-zinc rounded"/>
            <div className="w-24 h-4 bg-brutal-zinc rounded"/>
            <div className="w-12 h-4 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
        </div>
    );
}

export function CodeReviewPage() {
    const [repo, setRepo] = useState('');
    const [prNumber, setPrNumber] = useState('');
    const triggerReview = useTriggerCodeReview();
    const {data: events, isLoading} = useGitHubEvents(20);

    const reviewResult = triggerReview.data;

    function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
        e.preventDefault();
        const num = parseInt(prNumber, 10);
        if (!repo.trim() || isNaN(num) || num <= 0) return;
        triggerReview.mutate({repo: repo.trim(), prNumber: num});
    }

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div>
                <h1 className="text-2xl font-bold text-brutal-white tracking-tight">CODE REVIEW</h1>
                <p className="text-sm text-brutal-slate mt-1">AI-Powered Code Review</p>
            </div>

            {/* Trigger form */}
            <Card>
                <CardContent className="pt-4 space-y-4">
                    <div className="flex items-center gap-2">
                        <GitPullRequest className="w-4 h-4 text-signal-orange"/>
                        <span className="text-xs font-bold tracking-[0.3em] text-brutal-slate">TRIGGER REVIEW</span>
                    </div>

                    <form onSubmit={handleSubmit} className="flex items-end gap-3">
                        <div className="flex-1 space-y-1">
                            <label className="text-[10px] font-bold text-brutal-slate tracking-wider">REPOSITORY</label>
                            <Input
                                placeholder="owner/repo"
                                value={repo}
                                onChange={(e) => setRepo(e.target.value)}
                                disabled={triggerReview.isPending}
                                aria-label="Repository"
                            />
                        </div>
                        <div className="w-32 space-y-1">
                            <label className="text-[10px] font-bold text-brutal-slate tracking-wider">PR NUMBER</label>
                            <Input
                                placeholder="123"
                                value={prNumber}
                                onChange={(e) => {
                                    const value = e.target.value;
                                    if (value === '' || /^\d+$/.test(value)) {
                                        setPrNumber(value);
                                    }
                                }}
                                inputMode="numeric"
                                pattern="[0-9]*"
                                disabled={triggerReview.isPending}
                                aria-label="PR number"
                            />
                        </div>
                        <Button
                            type="submit"
                            disabled={!repo.trim() || !prNumber || triggerReview.isPending}
                            className="gap-2"
                        >
                            {triggerReview.isPending ? (
                                <Loader2 className="w-4 h-4 animate-spin"/>
                            ) : (
                                <Send className="w-4 h-4"/>
                            )}
                            {triggerReview.isPending ? 'Reviewing...' : 'Trigger Review'}
                        </Button>
                    </form>

                    {triggerReview.isError && (
                        <div className="flex items-center gap-2 text-sm text-red-400">
                            <AlertCircle className="w-4 h-4"/>
                            <span>
                                {triggerReview.error instanceof Error
                                    ? triggerReview.error.message
                                    : 'Failed to trigger code review'}
                            </span>
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Review results */}
            {reviewResult && <ReviewResults result={reviewResult}/>}

            {/* Recent GitHub Events */}
            <div className="space-y-3">
                <div className="flex items-center gap-2">
                    <Code2 className="w-4 h-4 text-signal-orange"/>
                    <span className="text-xs font-bold tracking-[0.3em] text-brutal-slate">RECENT GITHUB EVENTS</span>
                    {events && (
                        <span className="text-xs font-bold text-brutal-zinc">
                            {events.length} EVENT{events.length !== 1 ? 'S' : ''}
                        </span>
                    )}
                </div>

                <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                    {/* Table header */}
                    <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                        <div className="w-32">EVENT TYPE</div>
                        <div className="w-20">ACTION</div>
                        <div className="flex-1">REPOSITORY</div>
                        <div className="w-24">SENDER</div>
                        <div className="w-12 text-right">PR#</div>
                        <div className="w-28 text-right">TIME</div>
                    </div>

                    {/* Table body */}
                    {isLoading ? (
                        <>
                            <SkeletonRow/>
                            <SkeletonRow/>
                            <SkeletonRow/>
                            <SkeletonRow/>
                            <SkeletonRow/>
                        </>
                    ) : !events || events.length === 0 ? (
                        <div className="py-12 text-center">
                            <AlertCircle className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                            <p className="text-brutal-slate text-sm">No GitHub events found</p>
                            <p className="text-brutal-zinc text-xs mt-1">
                                Events will appear as webhooks are received from GitHub
                            </p>
                        </div>
                    ) : (
                        events.map((event) => (
                            <div
                                key={event.eventId}
                                className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 transition-colors"
                            >
                                <div className="w-32">
                                    <EventTypeBadge eventType={event.eventType}/>
                                </div>
                                <div className="w-20">
                                    <span className="text-xs font-mono text-brutal-slate">
                                        {event.action ?? '\u2014'}
                                    </span>
                                </div>
                                <div className="flex-1 min-w-0">
                                    <span className="text-sm font-mono text-brutal-white truncate block">
                                        {event.repoFullName}
                                    </span>
                                </div>
                                <div className="w-24">
                                    <span className="text-xs text-brutal-slate truncate block">
                                        {event.sender ?? '\u2014'}
                                    </span>
                                </div>
                                <div className="w-12 text-right">
                                    {event.prNumber != null ? (
                                        <span className="font-mono text-xs text-signal-orange">
                                            #{event.prNumber}
                                        </span>
                                    ) : (
                                        <span className="text-xs text-brutal-zinc">{'\u2014'}</span>
                                    )}
                                </div>
                                <div className="w-28 text-right">
                                    <span className="font-mono text-xs text-brutal-slate">
                                        {formatTimestamp(event.createdAt)}
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
