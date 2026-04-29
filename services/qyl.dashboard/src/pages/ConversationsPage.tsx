import {useMemo, useState} from 'react';
import {
    createColumnHelper,
    flexRender,
    getCoreRowModel,
    getSortedRowModel,
    type SortingState,
    useReactTable,
} from '@tanstack/react-table';
import {Bot, Clock, Coins, Hash, Loader2, MessagesSquare, Wrench} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {
    type ConversationListItem,
    type ConversationSpan,
    useConversationDetail,
    useConversations,
} from '@/hooks/use-conversations';

const columnHelper = createColumnHelper<ConversationListItem>();

function truncate(s: string, n: number) {
    return s.length <= n ? s : `${s.slice(0, n)}…`;
}

function formatRelative(iso: string) {
    const dt = new Date(iso).getTime();
    const diff = Date.now() - dt;
    const min = 60_000;
    const hr = 60 * min;
    const day = 24 * hr;
    if (diff < min) return 'just now';
    if (diff < hr) return `${Math.floor(diff / min)}m ago`;
    if (diff < day) return `${Math.floor(diff / hr)}h ago`;
    return `${Math.floor(diff / day)}d ago`;
}

function formatMs(ms: number) {
    if (ms < 1) return `${ms.toFixed(2)}ms`;
    if (ms < 1000) return `${ms.toFixed(0)}ms`;
    if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
    return `${(ms / 60_000).toFixed(1)}m`;
}

function formatCost(usd: number) {
    if (usd === 0) return '—';
    if (usd < 0.001) return '<$0.001';
    return `$${usd.toFixed(usd < 0.1 ? 4 : 2)}`;
}

export function ConversationsPage() {
    const [sorting, setSorting] = useState<SortingState>([{id: 'lastSeen', desc: true}]);
    const [selectedSession, setSelectedSession] = useState<string | null>(null);

    const list = useConversations(100, 168);
    const detail = useConversationDetail(selectedSession);

    const columns = useMemo(() => [
        columnHelper.accessor('sessionId', {
            header: 'SESSION',
            cell: info => (
                <span className="font-mono text-[11px]">{truncate(info.getValue(), 28)}</span>
            ),
        }),
        columnHelper.accessor('spanCount', {
            header: 'SPANS',
            cell: info => (
                <span className="font-mono text-[11px] tabular-nums">{info.getValue()}</span>
            ),
        }),
        columnHelper.accessor('lastSeen', {
            header: 'LAST',
            cell: info => (
                <span className="text-[11px] text-brutal-slate">{formatRelative(info.getValue())}</span>
            ),
        }),
        columnHelper.accessor('durationMs', {
            header: 'DURATION',
            cell: info => (
                <span className="font-mono text-[11px] tabular-nums">{formatMs(info.getValue())}</span>
            ),
        }),
        columnHelper.accessor('totalCostUsd', {
            header: 'COST',
            cell: info => (
                <span className="font-mono text-[11px] tabular-nums text-signal-orange">
                    {formatCost(info.getValue())}
                </span>
            ),
        }),
    ], []);

    const table = useReactTable({
        data: list.data?.items ?? [],
        columns,
        state: {sorting},
        onSortingChange: setSorting,
        getCoreRowModel: getCoreRowModel(),
        getSortedRowModel: getSortedRowModel(),
    });

    return (
        <div className="grid grid-cols-[minmax(0,420px)_1fr] gap-3 h-full">
            <Card className="overflow-hidden flex flex-col">
                <CardHeader className="border-b border-brutal-zinc/70 bg-brutal-dark/80 px-3 py-2">
                    <div className="flex items-center gap-2 text-[11px] font-bold tracking-[0.18em] text-signal-orange">
                        <MessagesSquare className="w-4 h-4"/>
                        CONVERSATIONS
                        {list.data && (
                            <Badge variant="outline" className="ml-auto">
                                {list.data.total}
                            </Badge>
                        )}
                    </div>
                </CardHeader>
                <CardContent className="p-0 overflow-auto flex-1">
                    {list.isLoading && (
                        <div className="flex items-center gap-2 text-brutal-slate text-[11px] px-3 py-4">
                            <Loader2 className="w-3.5 h-3.5 animate-spin"/>
                            LOADING…
                        </div>
                    )}
                    {list.isError && (
                        <div className="text-signal-red text-[11px] px-3 py-4">
                            FAILED TO LOAD CONVERSATIONS
                        </div>
                    )}
                    {list.data && list.data.items.length === 0 && (
                        <div className="text-brutal-slate text-[11px] px-3 py-4">
                            NO SESSIONS IN LAST 168H
                        </div>
                    )}
                    {list.data && list.data.items.length > 0 && (
                        <table className="w-full">
                            <thead>
                            {table.getHeaderGroups().map(hg => (
                                <tr key={hg.id} className="border-b border-brutal-zinc/70">
                                    {hg.headers.map(h => (
                                        <th
                                            key={h.id}
                                            className="px-2 py-1.5 text-left text-[10px] font-bold tracking-[0.12em] text-brutal-slate cursor-pointer select-none"
                                            onClick={h.column.getToggleSortingHandler()}
                                        >
                                            {flexRender(h.column.columnDef.header, h.getContext())}
                                        </th>
                                    ))}
                                </tr>
                            ))}
                            </thead>
                            <tbody>
                            {table.getRowModel().rows.map(row => {
                                const isActive = row.original.sessionId === selectedSession;
                                return (
                                    <tr
                                        key={row.id}
                                        onClick={() => setSelectedSession(row.original.sessionId)}
                                        className={cn(
                                            'border-b border-brutal-zinc/40 cursor-pointer transition-colors',
                                            isActive
                                                ? 'bg-signal-orange/12 text-signal-orange'
                                                : 'hover:bg-brutal-dark/60'
                                        )}
                                    >
                                        {row.getVisibleCells().map(cell => (
                                            <td key={cell.id} className="px-2 py-1.5">
                                                {flexRender(cell.column.columnDef.cell, cell.getContext())}
                                            </td>
                                        ))}
                                    </tr>
                                );
                            })}
                            </tbody>
                        </table>
                    )}
                </CardContent>
            </Card>

            <Card className="overflow-hidden flex flex-col">
                <CardHeader className="border-b border-brutal-zinc/70 bg-brutal-dark/80 px-3 py-2">
                    <div
                        className="flex items-center gap-2 text-[11px] font-bold tracking-[0.18em] text-signal-orange">
                        <Hash className="w-4 h-4"/>
                        THREAD
                        {selectedSession && (
                            <span className="ml-2 font-mono text-brutal-slate text-[11px]">
                                {truncate(selectedSession, 48)}
                            </span>
                        )}
                    </div>
                </CardHeader>
                <CardContent className="p-0 overflow-auto flex-1">
                    {!selectedSession && (
                        <div className="text-brutal-slate text-[11px] px-3 py-4">
                            SELECT A SESSION TO VIEW THREAD
                        </div>
                    )}
                    {selectedSession && detail.isLoading && (
                        <div className="flex items-center gap-2 text-brutal-slate text-[11px] px-3 py-4">
                            <Loader2 className="w-3.5 h-3.5 animate-spin"/>
                            LOADING THREAD…
                        </div>
                    )}
                    {selectedSession && detail.isError && (
                        <div className="text-signal-red text-[11px] px-3 py-4">
                            FAILED TO LOAD THREAD
                        </div>
                    )}
                    {selectedSession && detail.data && (
                        <ThreadView
                            spans={detail.data.spans}
                            captureContent={detail.data.captureFlags.messageContent}
                            captureInputs={detail.data.captureFlags.recordInputs}
                            captureOutputs={detail.data.captureFlags.recordOutputs}
                        />
                    )}
                </CardContent>
            </Card>
        </div>
    );
}

interface ThreadViewProps {
    spans: ConversationSpan[];
    captureContent: boolean;
    captureInputs: boolean;
    captureOutputs: boolean;
}

function ThreadView({spans, captureContent, captureInputs, captureOutputs}: ThreadViewProps) {
    return (
        <div className="divide-y divide-brutal-zinc/50">
            <div className="flex flex-wrap gap-2 px-3 py-2 bg-brutal-dark/40 border-b border-brutal-zinc/50">
                <CaptureBadge label="MESSAGE_CONTENT" on={captureContent}/>
                <CaptureBadge label="RECORD_INPUTS" on={captureInputs}/>
                <CaptureBadge label="RECORD_OUTPUTS" on={captureOutputs}/>
            </div>
            {spans.map(span => (
                <SpanRow
                    key={span.spanId}
                    span={span}
                    captureContent={captureContent}
                    captureInputs={captureInputs}
                    captureOutputs={captureOutputs}
                />
            ))}
        </div>
    );
}

function CaptureBadge({label, on}: { label: string; on: boolean }) {
    return (
        <Badge
            variant="outline"
            className={cn(
                'text-[10px] tracking-[0.12em]',
                on ? 'border-signal-green/55 text-signal-green' : 'border-brutal-zinc/55 text-brutal-slate'
            )}
        >
            {label}: {on ? 'ON' : 'OFF'}
        </Badge>
    );
}

interface SpanRowProps {
    span: ConversationSpan;
    captureContent: boolean;
    captureInputs: boolean;
    captureOutputs: boolean;
}

function SpanRow({span, captureContent, captureInputs, captureOutputs}: SpanRowProps) {
    const isToolCall = span.toolName !== null || span.name.startsWith('execute_tool');
    const isLlmCall = span.requestModel !== null || span.responseModel !== null;
    const Icon = isToolCall ? Wrench : isLlmCall ? Bot : Clock;

    const argsRaw = span.attributes?.['gen_ai.tool.call.arguments'];
    const resultRaw = span.attributes?.['gen_ai.tool.call.result'];
    const messageContent = span.attributes?.['gen_ai.input.messages'] ?? span.attributes?.['gen_ai.output.messages'];

    return (
        <div className="px-3 py-2 hover:bg-brutal-dark/40 transition-colors">
            <div className="flex items-center gap-2">
                <Icon className="w-3.5 h-3.5 text-signal-orange flex-shrink-0"/>
                <span className="font-mono text-[11px] flex-1 truncate">{span.name}</span>
                <span className="text-[10px] text-brutal-slate tabular-nums">
                    {formatMs(span.durationMs)}
                </span>
                {span.costUsd !== null && span.costUsd > 0 && (
                    <span className="font-mono text-[10px] text-signal-orange tabular-nums flex items-center gap-0.5">
                        <Coins className="w-3 h-3"/>{formatCost(span.costUsd)}
                    </span>
                )}
            </div>
            <div className="mt-1 flex flex-wrap gap-2 text-[10px] text-brutal-slate">
                <span>{span.serviceName}</span>
                {span.requestModel && <span>· {span.requestModel}</span>}
                {span.provider && <span>· {span.provider}</span>}
                {span.toolName && <span>· tool={span.toolName}</span>}
                {span.inputTokens !== null && <span>· in={span.inputTokens}</span>}
                {span.outputTokens !== null && <span>· out={span.outputTokens}</span>}
            </div>
            {captureInputs && argsRaw !== undefined && (
                <pre
                    className="mt-2 p-2 bg-brutal-dark/80 border border-brutal-zinc/60 text-[10px] font-mono text-brutal-white/90 overflow-x-auto whitespace-pre-wrap break-all">
                    args: {JSON.stringify(argsRaw, null, 2)}
                </pre>
            )}
            {captureOutputs && resultRaw !== undefined && (
                <pre
                    className="mt-2 p-2 bg-brutal-dark/80 border border-brutal-zinc/60 text-[10px] font-mono text-brutal-white/90 overflow-x-auto whitespace-pre-wrap break-all">
                    result: {JSON.stringify(resultRaw, null, 2)}
                </pre>
            )}
            {captureContent && messageContent !== undefined && (
                <pre
                    className="mt-2 p-2 bg-brutal-dark/80 border border-brutal-zinc/60 text-[10px] font-mono text-brutal-white/90 overflow-x-auto whitespace-pre-wrap break-all">
                    messages: {JSON.stringify(messageContent, null, 2)}
                </pre>
            )}
        </div>
    );
}
