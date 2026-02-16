import {useState} from 'react';
import {
    Bot,
    CheckCircle2,
    ChevronDown,
    ChevronRight,
    Clock,
    Code,
    Database,
    FileSearch,
    Globe,
    Loader2,
    Terminal,
    Wrench,
    XCircle,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Badge} from '@/components/ui/badge';
import {TextVisualizer} from '@/components/ui';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import type {AgentRun, ToolCall} from '@/hooks/use-agent-runs';

function getToolIcon(toolName?: string) {
    if (!toolName) return Wrench;
    const name = toolName.toLowerCase();
    if (name.includes('search') || name.includes('grep') || name.includes('find')) return FileSearch;
    if (name.includes('code') || name.includes('edit') || name.includes('write')) return Code;
    if (name.includes('http') || name.includes('fetch') || name.includes('web')) return Globe;
    if (name.includes('shell') || name.includes('bash') || name.includes('exec')) return Terminal;
    if (name.includes('db') || name.includes('sql') || name.includes('query')) return Database;
    return Wrench;
}

function StatusDot({status}: { status: string }) {
    if (status === 'running') {
        return <Loader2 className="w-3.5 h-3.5 text-blue-400 animate-spin flex-shrink-0"/>;
    }
    if (status === 'completed') {
        return <CheckCircle2 className="w-3.5 h-3.5 text-green-500 flex-shrink-0"/>;
    }
    if (status === 'failed') {
        return <XCircle className="w-3.5 h-3.5 text-red-500 flex-shrink-0"/>;
    }
    return <Clock className="w-3.5 h-3.5 text-brutal-slate flex-shrink-0"/>;
}

interface ToolCallNodeProps {
    call: ToolCall;
    agentStartTime?: number;
    agentDurationNs?: number;
}

function ToolCallNode({call, agentStartTime, agentDurationNs}: ToolCallNodeProps) {
    const [expanded, setExpanded] = useState(false);
    const Icon = getToolIcon(call.tool_name);
    const durationMs = call.duration_ns ? nsToMs(call.duration_ns) : null;

    // Timeline bar relative to agent run
    let leftPercent = 0;
    let widthPercent = 1;
    if (agentStartTime && agentDurationNs && agentDurationNs > 0 && call.start_time) {
        leftPercent = Math.max(0, ((call.start_time - agentStartTime) / agentDurationNs) * 100);
        const callDur = call.duration_ns ?? 0;
        widthPercent = Math.max(0.5, (callDur / agentDurationNs) * 100);
    }

    const hasPayload = call.arguments_json || call.result_json || call.error_message;

    return (
        <div className="ml-6 border-l-2 border-brutal-zinc">
            <div
                className={cn(
                    'flex items-center gap-2 px-3 py-2 hover:bg-brutal-dark/50 transition-colors',
                    hasPayload && 'cursor-pointer',
                )}
                onClick={() => hasPayload && setExpanded(!expanded)}
            >
                {/* Connector */}
                <div className="w-4 h-px bg-brutal-zinc -ml-[calc(0.75rem+1px)]"/>

                {/* Expand/collapse */}
                <div className="w-4 flex-shrink-0">
                    {hasPayload ? (
                        expanded ? (
                            <ChevronDown className="w-3.5 h-3.5 text-brutal-slate"/>
                        ) : (
                            <ChevronRight className="w-3.5 h-3.5 text-brutal-slate"/>
                        )
                    ) : null}
                </div>

                <Icon className="w-4 h-4 text-brutal-slate flex-shrink-0"/>
                <StatusDot status={call.status}/>

                <span className="font-mono text-sm text-brutal-white truncate">
                    {call.tool_name ?? 'unknown'}
                </span>

                {call.tool_type && (
                    <Badge variant="outline" className="text-[10px] text-brutal-slate border-brutal-zinc">
                        {call.tool_type}
                    </Badge>
                )}

                {/* Timeline bar */}
                <div className="flex-1 h-4 relative bg-brutal-dark/40 rounded overflow-hidden mx-2 min-w-[80px]">
                    <div
                        className={cn(
                            'absolute top-0.5 h-3 rounded-sm',
                            call.status === 'failed'
                                ? 'bg-red-500/60'
                                : call.status === 'running'
                                    ? 'bg-blue-500/60 animate-pulse'
                                    : 'bg-signal-orange/50',
                        )}
                        style={{
                            left: `${leftPercent}%`,
                            width: `${Math.min(widthPercent, 100 - leftPercent)}%`,
                        }}
                    />
                </div>

                {durationMs !== null && (
                    <span className="font-mono text-xs text-brutal-slate flex-shrink-0 w-16 text-right">
                        {formatDuration(durationMs)}
                    </span>
                )}
            </div>

            {/* Expanded details */}
            {expanded && hasPayload && (
                <div className="ml-10 mr-3 mb-2 space-y-2">
                    {call.arguments_json && (
                        <TextVisualizer
                            content={call.arguments_json}
                            label="Arguments"
                            defaultExpanded={true}
                            maxCollapsedHeight={120}
                            showTreeView={true}
                        />
                    )}
                    {call.result_json && (
                        <TextVisualizer
                            content={call.result_json}
                            label="Result"
                            defaultExpanded={false}
                            maxCollapsedHeight={120}
                            showTreeView={true}
                        />
                    )}
                    {call.error_message && (
                        <div
                            className="text-sm text-red-400 bg-red-500/10 border border-red-500/30 rounded p-2 font-mono">
                            {call.error_message}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}

interface AgentTraceTreeProps {
    run: AgentRun;
    toolCalls: ToolCall[];
    isLoading?: boolean;
}

export function AgentTraceTree({run, toolCalls, isLoading}: AgentTraceTreeProps) {
    const [rootExpanded, setRootExpanded] = useState(true);
    const durationMs = run.duration_ns ? nsToMs(run.duration_ns) : null;
    const sortedCalls = [...toolCalls].sort((a, b) => a.sequence_number - b.sequence_number);

    return (
        <div className="border-2 border-brutal-zinc bg-brutal-carbon rounded">
            {/* Root: Agent invocation */}
            <div
                className="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-brutal-dark/50 transition-colors"
                onClick={() => setRootExpanded(!rootExpanded)}
            >
                {rootExpanded ? (
                    <ChevronDown className="w-4 h-4 text-brutal-slate"/>
                ) : (
                    <ChevronRight className="w-4 h-4 text-brutal-slate"/>
                )}

                <Bot className="w-5 h-5 text-signal-orange"/>
                <StatusDot status={run.status}/>

                <span className="font-bold text-brutal-white tracking-wide">
                    {run.agent_name ?? 'Agent Run'}
                </span>

                {run.model && (
                    <Badge variant="secondary" className="text-[10px]">
                        {run.model}
                    </Badge>
                )}

                {run.provider && (
                    <Badge variant="outline" className="text-[10px] text-brutal-slate border-brutal-zinc">
                        {run.provider}
                    </Badge>
                )}

                <div className="flex-1"/>

                <span className="font-mono text-xs text-brutal-slate">
                    {run.tool_call_count} tool{run.tool_call_count !== 1 ? 's' : ''}
                </span>

                {durationMs !== null && (
                    <span className="font-mono text-xs text-brutal-slate">
                        {formatDuration(durationMs)}
                    </span>
                )}

                {run.total_cost > 0 && (
                    <span className="font-mono text-xs text-signal-green">
                        ${run.total_cost.toFixed(4)}
                    </span>
                )}
            </div>

            {/* Children: Tool calls */}
            {rootExpanded && (
                <div className="border-t border-brutal-zinc">
                    {isLoading ? (
                        <div className="flex items-center justify-center py-8">
                            <Loader2 className="w-5 h-5 animate-spin text-brutal-slate"/>
                            <span className="ml-2 text-sm text-brutal-slate">Loading tool calls…</span>
                        </div>
                    ) : sortedCalls.length === 0 ? (
                        <div className="py-6 text-center text-sm text-brutal-slate">
                            No tool calls recorded
                        </div>
                    ) : (
                        <div className="py-1">
                            {sortedCalls.map((call) => (
                                <ToolCallNode
                                    key={call.call_id}
                                    call={call}
                                    agentStartTime={run.start_time}
                                    agentDurationNs={run.duration_ns ?? undefined}
                                />
                            ))}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
