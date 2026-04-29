import {useMemo, useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {
    createColumnHelper,
    flexRender,
    getCoreRowModel,
    getSortedRowModel,
    type SortingState,
    useReactTable,
} from '@tanstack/react-table';
import {Bot, Copy, Loader2, ShieldAlert} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {type AgentRegistration, useAgentInventory} from '@/hooks/use-agent-inventory';

const columnHelper = createColumnHelper<AgentRegistration>();

function truncateHash(hash: string | null) {
    if (!hash) return '—';
    return hash.length <= 12 ? hash : `${hash.slice(0, 12)}…`;
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

async function copyToClipboard(text: string) {
    try {
        await navigator.clipboard.writeText(text);
    } catch {
        // No-op: clipboard may be unavailable in non-secure contexts.
    }
}

export function AgentsPage() {
    const navigate = useNavigate();
    const [sorting, setSorting] = useState<SortingState>([{id: 'name', desc: false}]);
    const inventory = useAgentInventory();

    const columns = useMemo(() => [
        columnHelper.accessor('name', {
            header: 'NAME',
            cell: info => (
                <span className="font-mono text-[11px] text-brutal-white">{info.getValue()}</span>
            ),
        }),
        columnHelper.accessor('key', {
            header: 'KEY',
            cell: info => (
                <span className="font-mono text-[11px] text-brutal-slate">{info.getValue()}</span>
            ),
        }),
        columnHelper.accessor('instructionsHash', {
            header: 'INSTRUCTIONS_HASH',
            cell: info => {
                const full = info.getValue();
                if (!full) return <span className="text-brutal-slate text-[11px]">—</span>;
                return (
                    <button
                        type="button"
                        onClick={(e) => {
                            e.stopPropagation();
                            void copyToClipboard(full);
                        }}
                        className="inline-flex items-center gap-1 font-mono text-[10px] text-brutal-slate hover:text-signal-orange transition-colors"
                        title={`Click to copy: ${full}`}
                    >
                        {truncateHash(full)}
                        <Copy className="w-3 h-3"/>
                    </button>
                );
            },
        }),
        columnHelper.accessor('providerName', {
            header: 'PROVIDER',
            cell: info => (
                <span className="text-[11px] text-brutal-slate">{info.getValue() ?? '—'}</span>
            ),
        }),
        columnHelper.accessor('registeredAtUtc', {
            header: 'REGISTERED',
            cell: info => (
                <span className="text-[11px] text-brutal-slate">{formatRelative(info.getValue())}</span>
            ),
        }),
    ], []);

    const table = useReactTable({
        data: inventory.data?.items ?? [],
        columns,
        state: {sorting},
        onSortingChange: setSorting,
        getCoreRowModel: getCoreRowModel(),
        getSortedRowModel: getSortedRowModel(),
    });

    return (
        <Card className="overflow-hidden flex flex-col h-full">
            <CardHeader className="border-b border-brutal-zinc/70 bg-brutal-dark/80 px-3 py-2">
                <div className="flex items-center gap-2 text-[11px] font-bold tracking-[0.18em] text-signal-orange">
                    <Bot className="w-4 h-4"/>
                    AGENTS
                    {inventory.data?.available && (
                        <Badge variant="outline" className="ml-auto">
                            {inventory.data.total}
                        </Badge>
                    )}
                </div>
            </CardHeader>
            <CardContent className="p-0 overflow-auto flex-1">
                {inventory.isLoading && (
                    <div className="flex items-center gap-2 text-brutal-slate text-[11px] px-3 py-4">
                        <Loader2 className="w-3.5 h-3.5 animate-spin"/>
                        LOADING INVENTORY…
                    </div>
                )}
                {inventory.isError && (
                    <div className="text-signal-red text-[11px] px-3 py-4">
                        FAILED TO LOAD INVENTORY
                    </div>
                )}
                {inventory.data && !inventory.data.available && (
                    <div className="flex items-start gap-2 px-3 py-4 text-[11px] text-brutal-slate">
                        <ShieldAlert className="w-4 h-4 text-signal-orange flex-shrink-0 mt-0.5"/>
                        <div>
                            <div className="font-bold tracking-[0.12em] text-signal-orange">INVENTORY RESTRICTED</div>
                            <div className="mt-1">
                                /qyl/inventory/agents returned {inventory.data.statusCode}. Endpoint is gated by the
                                QylAdmin policy or dev-only mapping.
                            </div>
                        </div>
                    </div>
                )}
                {inventory.data?.available && inventory.data.items.length === 0 && (
                    <div className="text-brutal-slate text-[11px] px-3 py-4">
                        NO AGENTS REGISTERED
                    </div>
                )}
                {inventory.data?.available && inventory.data.items.length > 0 && (
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
                        {table.getRowModel().rows.map(row => (
                            <tr
                                key={row.id}
                                onClick={() => navigate(
                                    `/conversations?agent=${encodeURIComponent(row.original.name)}`,
                                )}
                                className={cn(
                                    'border-b border-brutal-zinc/40 cursor-pointer transition-colors',
                                    'hover:bg-brutal-dark/60',
                                )}
                            >
                                {row.getVisibleCells().map(cell => (
                                    <td key={cell.id} className="px-2 py-1.5">
                                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                                    </td>
                                ))}
                            </tr>
                        ))}
                        </tbody>
                    </table>
                )}
            </CardContent>
        </Card>
    );
}
