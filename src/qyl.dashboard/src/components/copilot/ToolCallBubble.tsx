import {useState} from 'react';
import {Check, ChevronDown, ChevronRight, Loader2, Wrench} from 'lucide-react';
import type {ToolCallEvent} from '@/hooks/use-copilot';

interface ToolCallBubbleProps {
    toolCall: ToolCallEvent;
}

export function ToolCallBubble({toolCall}: ToolCallBubbleProps) {
    const [showArgs, setShowArgs] = useState(false);
    const [showResult, setShowResult] = useState(false);

    const isActive = toolCall.status === 'calling';
    const isError = toolCall.status === 'error';

    return (
        <div
            className={`border-2 bg-brutal-dark p-2.5 text-xs ${
                isError
                    ? 'border-signal-red'
                    : isActive
                        ? 'border-signal-cyan'
                        : 'border-signal-cyan/50'
            }`}
        >
            {/* Header */}
            <div className="flex items-center gap-2">
                <Wrench className="w-3.5 h-3.5 text-signal-cyan shrink-0"/>
                <span className="font-bold tracking-wider text-signal-cyan">
                    {toolCall.name}
                </span>
                <span className="ml-auto shrink-0">
                    {isActive ? (
                        <Loader2 className="w-3.5 h-3.5 text-signal-cyan animate-spin"/>
                    ) : isError ? (
                        <span className="text-signal-red text-[10px] font-bold">ERROR</span>
                    ) : (
                        <Check className="w-3.5 h-3.5 text-signal-green"/>
                    )}
                </span>
            </div>

            {/* Arguments (collapsible) */}
            {toolCall.arguments && (
                <button
                    onClick={() => setShowArgs(!showArgs)}
                    className="flex items-center gap-1 mt-2 text-[10px] text-brutal-slate hover:text-brutal-white transition-colors"
                >
                    {showArgs ? (
                        <ChevronDown className="w-3 h-3"/>
                    ) : (
                        <ChevronRight className="w-3 h-3"/>
                    )}
                    Arguments
                </button>
            )}
            {showArgs && toolCall.arguments && (
                <pre
                    className="mt-1 p-2 bg-brutal-carbon border border-brutal-zinc text-[10px] text-brutal-slate overflow-x-auto whitespace-pre-wrap break-all">
                    {formatJson(toolCall.arguments)}
                </pre>
            )}

            {/* Result (collapsible) */}
            {toolCall.result && (
                <button
                    onClick={() => setShowResult(!showResult)}
                    className="flex items-center gap-1 mt-2 text-[10px] text-brutal-slate hover:text-brutal-white transition-colors"
                >
                    {showResult ? (
                        <ChevronDown className="w-3 h-3"/>
                    ) : (
                        <ChevronRight className="w-3 h-3"/>
                    )}
                    Result
                </button>
            )}
            {showResult && toolCall.result && (
                <pre
                    className="mt-1 p-2 bg-brutal-carbon border border-brutal-zinc text-[10px] text-brutal-slate overflow-x-auto whitespace-pre-wrap break-all">
                    {toolCall.result}
                </pre>
            )}
        </div>
    );
}

function formatJson(str: string): string {
    try {
        return JSON.stringify(JSON.parse(str), null, 2);
    } catch {
        return str;
    }
}
