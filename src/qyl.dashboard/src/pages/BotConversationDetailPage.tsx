import {useNavigate, useParams} from 'react-router-dom';
import {AlertCircle, ArrowLeft, CheckCircle, ChevronRight, Clock, Cpu, Loader2, Wrench,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Badge} from '@/components/ui/badge';
import {Card, CardContent} from '@/components/ui/card';
import {useConversationDetail, type ConversationTurn} from '@/hooks/use-analytics';

function formatTs(iso: string): string {
    return new Date(iso).toLocaleString('en-US', {
        month: 'short', day: 'numeric',
        hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
    });
}

function formatTokens(n: number): string {
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
    return String(n);
}

function TurnStatusBadge({statusCode}: {statusCode: number}) {
    if (statusCode === 2) {
        return (
            <Badge variant="outline" className="text-[10px] bg-red-500/20 text-red-400 border-red-500/40">
                ERROR
            </Badge>
        );
    }
    if (statusCode === 1) {
        return (
            <Badge variant="outline" className="text-[10px] bg-signal-green/20 text-signal-green border-signal-green/40">
                OK
            </Badge>
        );
    }
    return (
        <Badge variant="outline" className="text-[10px] bg-brutal-zinc/20 text-brutal-slate border-brutal-zinc/40">
            UNSET
        </Badge>
    );
}

function OperationBadge({name}: {name: string | null}) {
    if (!name) return null;
    const styles: Record<string, string> = {
        chat: 'bg-signal-cyan/20 text-signal-cyan border-signal-cyan/40',
        invoke_agent: 'bg-signal-violet/20 text-signal-violet border-signal-violet/40',
        execute_tool: 'bg-signal-orange/20 text-signal-orange border-signal-orange/40',
        embeddings: 'bg-signal-yellow/20 text-signal-yellow border-signal-yellow/40',
    };
    return (
        <Badge variant="outline" className={cn('text-[10px]', styles[name] ?? 'bg-brutal-zinc/20 text-brutal-slate border-brutal-zinc/40')}>
            {name}
        </Badge>
    );
}

function TurnCard({turn, index}: {turn: ConversationTurn; index: number}) {
    const hasError = turn.statusCode === 2;
    const isTool = turn.operationName === 'execute_tool';

    return (
        <div className={cn(
            'border-2 p-4 space-y-3 transition-colors',
            hasError ? 'border-red-500/50 bg-red-500/5' : isTool ? 'border-signal-orange/30 bg-signal-orange/5' : 'border-brutal-zinc bg-brutal-carbon',
        )}>
            {/* Turn header */}
            <div className="flex items-center gap-3">
                <span className="font-mono text-[10px] text-brutal-zinc w-6 text-right">{index + 1}</span>
                <div className="flex-1 min-w-0">
                    <span className="text-sm font-bold text-brutal-white truncate block">{turn.name}</span>
                </div>
                <OperationBadge name={turn.operationName}/>
                <TurnStatusBadge statusCode={turn.statusCode}/>
            </div>

            {/* Metadata grid */}
            <div className="grid grid-cols-3 gap-4 ml-9">
                <div>
                    <div className="label-industrial">TIMESTAMP</div>
                    <div className="font-mono text-xs text-brutal-slate mt-0.5">{formatTs(turn.timestamp)}</div>
                </div>
                <div>
                    <div className="label-industrial">DURATION</div>
                    <div className="font-mono text-xs text-brutal-slate mt-0.5">{turn.durationMs.toFixed(1)}ms</div>
                </div>
                {(turn.inputTokens > 0 || turn.outputTokens > 0) && (
                    <div>
                        <div className="label-industrial">TOKENS</div>
                        <div className="font-mono text-xs text-brutal-slate mt-0.5">
                            {formatTokens(turn.inputTokens)} in / {formatTokens(turn.outputTokens)} out
                        </div>
                    </div>
                )}
                {turn.model && (
                    <div>
                        <div className="label-industrial">MODEL</div>
                        <div className="font-mono text-xs text-brutal-slate mt-0.5 truncate">{turn.model}</div>
                    </div>
                )}
                {turn.provider && (
                    <div>
                        <div className="label-industrial">PROVIDER</div>
                        <div className="font-mono text-xs text-brutal-slate mt-0.5">{turn.provider}</div>
                    </div>
                )}
                {turn.toolName && (
                    <div>
                        <div className="label-industrial">TOOL</div>
                        <div className="font-mono text-xs text-signal-orange mt-0.5">{turn.toolName}</div>
                    </div>
                )}
                {turn.stopReason && (
                    <div>
                        <div className="label-industrial">STOP REASON</div>
                        <div className="font-mono text-xs text-brutal-slate mt-0.5">{turn.stopReason}</div>
                    </div>
                )}
                {turn.dataSourceId && (
                    <div>
                        <div className="label-industrial">DATA SOURCE</div>
                        <div className="font-mono text-xs text-signal-cyan mt-0.5">{turn.dataSourceId}</div>
                    </div>
                )}
            </div>

            {/* Error message */}
            {turn.statusMessage && (
                <div className="ml-9 px-3 py-2 bg-red-500/10 border-l-2 border-red-500">
                    <p className="text-xs text-red-400">{turn.statusMessage}</p>
                </div>
            )}
        </div>
    );
}

export function BotConversationDetailPage() {
    const navigate = useNavigate();
    const {conversationId} = useParams<{conversationId: string}>();
    const {data, isLoading, error} = useConversationDetail(conversationId ?? '');

    const turns = data?.turns ?? [];
    const chatTurns = turns.filter((t) => t.operationName === 'chat' || t.operationName === 'invoke_agent');
    const toolTurns = turns.filter((t) => t.operationName === 'execute_tool');
    const errorTurns = turns.filter((t) => t.statusCode === 2);
    const totalTokens = turns.reduce((s, t) => s + t.inputTokens + t.outputTokens, 0);

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load conversation</p>
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
                <div className="section-header">CONVERSATION</div>
                {conversationId && (
                    <span className="font-mono text-xs text-brutal-slate">{conversationId}</span>
                )}
            </div>

            {/* Stat cards */}
            <div className="grid grid-cols-4 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Clock className="w-4 h-4 text-signal-cyan"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TURNS</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className="text-2xl font-bold mt-1 text-brutal-white">{turns.length}</div>
                        }
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Cpu className="w-4 h-4 text-signal-cyan"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TOKENS</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className="text-2xl font-bold mt-1 text-signal-cyan">{formatTokens(totalTokens)}</div>
                        }
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Wrench className="w-4 h-4 text-signal-orange"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TOOL CALLS</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className="text-2xl font-bold mt-1 text-signal-orange">{toolTurns.length}</div>
                        }
                    </CardContent>
                </Card>
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            {errorTurns.length > 0
                                ? <AlertCircle className="w-4 h-4 text-red-400"/>
                                : <CheckCircle className="w-4 h-4 text-signal-green"/>
                            }
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">ERRORS</span>
                        </div>
                        {isLoading
                            ? <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            : <div className={cn('text-2xl font-bold mt-1', errorTurns.length > 0 ? 'text-red-400' : 'text-signal-green')}>
                                {errorTurns.length}
                            </div>
                        }
                    </CardContent>
                </Card>
            </div>

            {/* User info */}
            {!isLoading && chatTurns[0]?.userId && (
                <div className="flex items-center gap-3 px-4 py-2 border-2 border-brutal-zinc bg-brutal-carbon">
                    <span className="label-industrial">USER</span>
                    <span className="font-mono text-xs text-signal-cyan">{chatTurns[0].userId}</span>
                </div>
            )}

            {/* Turns timeline */}
            <div>
                <div className="section-header mb-4">TURN TIMELINE ({turns.length})</div>
                {isLoading ? (
                    <div className="flex items-center justify-center py-12">
                        <div className="w-8 h-8 border-3 border-signal-orange border-t-transparent animate-spin"/>
                    </div>
                ) : turns.length === 0 ? (
                    <div className="border-2 border-brutal-zinc bg-brutal-carbon py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">Conversation not found</p>
                        <p className="text-brutal-zinc text-xs mt-1">
                            The conversation ID may not match any recorded spans
                        </p>
                    </div>
                ) : (
                    <div className="space-y-2">
                        {turns.map((turn, i) => <TurnCard key={turn.spanId} turn={turn} index={i}/>)}
                    </div>
                )}
            </div>
        </div>
    );
}
