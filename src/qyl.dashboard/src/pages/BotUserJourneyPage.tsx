import {useNavigate, useParams} from 'react-router-dom';
import {AlertCircle, ArrowLeft, Calendar, CheckCircle, ChevronRight, Clock, Cpu, Loader2, ThumbsDown, ThumbsUp,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {useUserJourney, type UserConversation} from '@/hooks/use-analytics';

function formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-US', {
        year: 'numeric', month: 'short', day: 'numeric',
    });
}

function formatTokens(n: number): string {
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
    return String(n);
}

function ConversationRow({c, onClick}: {c: UserConversation; onClick: () => void}) {
    return (
        <div
            onClick={onClick}
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
        >
            <div className="w-32">
                <span className="font-mono text-xs text-brutal-slate">{formatDate(c.date)}</span>
            </div>
            <div className="flex-1 min-w-0">
                <span className="text-sm text-brutal-white truncate block">{c.topic ?? '—'}</span>
            </div>
            <div className="w-16 text-right">
                <span className="font-mono text-xs text-brutal-slate">{c.turnCount} turns</span>
            </div>
            <div className="w-24 text-right">
                {c.satisfied
                    ? <ThumbsUp className="w-4 h-4 text-signal-green ml-auto"/>
                    : <ThumbsDown className="w-4 h-4 text-red-400 ml-auto"/>
                }
            </div>
            <div className="w-40 min-w-0">
                <span className="font-mono text-[10px] text-brutal-zinc truncate block">{c.conversationId.slice(0, 16)}…</span>
            </div>
            <ChevronRight className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

export function BotUserJourneyPage() {
    const navigate = useNavigate();
    const {userId} = useParams<{userId: string}>();
    const {data, isLoading, error} = useUserJourney(userId ?? '');

    const satisfiedCount = data?.conversations.filter((c) => c.satisfied).length ?? 0;
    const unsatisfiedCount = (data?.conversations.length ?? 0) - satisfiedCount;
    const satisfactionRate = data?.conversations.length
        ? satisfiedCount / data.conversations.length
        : 0;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load user journey</p>
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
            {/* Header */}
            <div className="flex items-center gap-4">
                <button
                    onClick={() => navigate('/bot')}
                    className="flex items-center gap-2 text-brutal-slate hover:text-brutal-white transition-colors cursor-pointer"
                    aria-label="Back to bot analytics"
                >
                    <ArrowLeft className="w-4 h-4"/>
                    <span className="text-[10px] font-bold tracking-wider">BOT ANALYTICS</span>
                </button>
                <ChevronRight className="w-4 h-4 text-brutal-zinc"/>
                <div className="section-header">USER JOURNEY</div>
                {userId && (
                    <span className="font-mono text-xs text-signal-cyan">{decodeURIComponent(userId)}</span>
                )}
            </div>

            {/* Stat cards */}
            <div className="grid grid-cols-4 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Clock className="w-4 h-4 text-signal-cyan"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">CONVERSATIONS</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className="text-2xl font-bold mt-1 text-brutal-white">{data?.conversations.length ?? 0}</div>
                        }
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Cpu className="w-4 h-4 text-signal-cyan"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TOTAL TOKENS</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className="text-2xl font-bold mt-1 text-signal-cyan">{formatTokens(data?.totalTokens ?? 0)}</div>
                        }
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Calendar className="w-4 h-4 text-signal-orange"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">RETENTION</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className="text-2xl font-bold mt-1 text-signal-orange">{data?.retentionDays ?? 0}d</div>
                        }
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            {satisfactionRate >= 0.8
                                ? <CheckCircle className="w-4 h-4 text-signal-green"/>
                                : <AlertCircle className="w-4 h-4 text-signal-yellow"/>
                            }
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">SATISFACTION</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className={cn(
                                'text-2xl font-bold mt-1',
                                satisfactionRate >= 0.8 ? 'text-signal-green' : satisfactionRate >= 0.6 ? 'text-signal-yellow' : 'text-red-400',
                            )}>
                                {Math.round(satisfactionRate * 100)}%
                            </div>
                        }
                    </CardContent>
                </Card>
            </div>

            {/* Frequent topics */}
            {!isLoading && (data?.frequentTopics.length ?? 0) > 0 && (
                <div className="border-2 border-brutal-zinc bg-brutal-carbon px-4 py-3">
                    <span className="label-industrial mr-4">FREQUENT TOPICS</span>
                    <div className="flex flex-wrap gap-2 mt-2">
                        {data!.frequentTopics.map((t) => (
                            <span key={t} className="text-xs text-signal-cyan bg-signal-cyan/10 border border-signal-cyan/30 px-3 py-1">
                                {t}
                            </span>
                        ))}
                    </div>
                </div>
            )}

            {/* Conversation history */}
            <div>
                <div className="section-header mb-4">CONVERSATION HISTORY</div>
                <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                    <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                        <div className="w-32">DATE</div>
                        <div className="flex-1">TOPIC</div>
                        <div className="w-16 text-right">TURNS</div>
                        <div className="w-24 text-right">FEEDBACK</div>
                        <div className="w-40">CONVERSATION ID</div>
                        <div className="w-4"/>
                    </div>
                    {isLoading ? (
                        <div className="flex items-center justify-center py-12">
                            <div className="w-8 h-8 border-3 border-signal-orange border-t-transparent animate-spin"/>
                        </div>
                    ) : (data?.conversations.length ?? 0) === 0 ? (
                        <div className="py-12 text-center">
                            <AlertCircle className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                            <p className="text-brutal-slate text-sm">No conversations found</p>
                            <p className="text-brutal-zinc text-xs mt-1">User not found or has no recorded conversations</p>
                        </div>
                    ) : (
                        data!.conversations.map((c) => (
                            <ConversationRow
                                key={c.conversationId}
                                c={c}
                                onClick={() => navigate(`/bot/conversations/${encodeURIComponent(c.conversationId)}`)}
                            />
                        ))
                    )}
                </div>
            </div>

            {/* Satisfaction summary */}
            {!isLoading && (data?.conversations.length ?? 0) > 0 && (
                <div className="grid grid-cols-2 gap-4">
                    <div className="flex items-center gap-3 px-4 py-3 border-2 border-signal-green/30 bg-signal-green/5">
                        <ThumbsUp className="w-5 h-5 text-signal-green"/>
                        <span className="text-sm font-bold text-signal-green">{satisfiedCount}</span>
                        <span className="text-xs text-brutal-slate tracking-wider">SATISFIED CONVERSATIONS</span>
                    </div>
                    <div className="flex items-center gap-3 px-4 py-3 border-2 border-red-500/30 bg-red-500/5">
                        <ThumbsDown className="w-5 h-5 text-red-400"/>
                        <span className="text-sm font-bold text-red-400">{unsatisfiedCount}</span>
                        <span className="text-xs text-brutal-slate tracking-wider">UNSATISFIED CONVERSATIONS</span>
                    </div>
                </div>
            )}
        </div>
    );
}
