import type {TopNRow} from '@/hooks/use-dashboards';
import {cn} from '@/lib/utils';

interface TopNTableProps {
    title: string;
    data: TopNRow[];
}

export function TopNTable({title, data}: TopNTableProps) {
    return (
        <div className="border-3 border-brutal-zinc bg-brutal-carbon p-4 space-y-3">
            <div className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                {title}
            </div>
            <div className="overflow-x-auto">
                <table className="w-full text-xs">
                    <thead>
                    <tr className="border-b-2 border-brutal-zinc text-brutal-slate">
                        <th className="text-left py-2 px-2 font-bold tracking-wider">NAME</th>
                        <th className="text-right py-2 px-2 font-bold tracking-wider">VALUE</th>
                        {data.some(r => r.count != null) && (
                            <th className="text-right py-2 px-2 font-bold tracking-wider">COUNT</th>
                        )}
                        {data.some(r => r.errorRate != null && r.errorRate > 0) && (
                            <th className="text-right py-2 px-2 font-bold tracking-wider">ERR%</th>
                        )}
                    </tr>
                    </thead>
                    <tbody>
                    {data.map((row, i) => (
                        <tr
                            key={i}
                            className="border-b border-brutal-zinc/50 hover:bg-brutal-dark transition-colors"
                        >
                            <td className="py-2 px-2 text-brutal-white font-mono truncate max-w-[280px]"
                                title={row.name}>
                                {row.name}
                            </td>
                            <td className="py-2 px-2 text-right text-signal-orange font-bold tabular-nums">
                                {row.value.toFixed(1)}{row.unit ? ` ${row.unit}` : ''}
                            </td>
                            {data.some(r => r.count != null) && (
                                <td className="py-2 px-2 text-right text-brutal-slate tabular-nums">
                                    {row.count?.toLocaleString() ?? '-'}
                                </td>
                            )}
                            {data.some(r => r.errorRate != null && r.errorRate > 0) && (
                                <td className={cn(
                                    'py-2 px-2 text-right font-bold tabular-nums',
                                    (row.errorRate ?? 0) > 5 ? 'text-signal-red' : 'text-brutal-slate'
                                )}>
                                    {row.errorRate?.toFixed(1) ?? '0'}%
                                </td>
                            )}
                        </tr>
                    ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
