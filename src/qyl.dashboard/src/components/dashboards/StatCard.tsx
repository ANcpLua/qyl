import type {StatCardData} from '@/hooks/use-dashboards';
import {cn} from '@/lib/utils';

interface StatCardProps {
    title: string;
    data: StatCardData;
    className?: string;
}

export function StatCard({title, data, className}: StatCardProps) {
    return (
        <div
            className={cn(
                'border-3 border-brutal-zinc bg-brutal-carbon p-4 space-y-2',
                className
            )}
        >
            <div className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                {title}
            </div>
            <div className="flex items-baseline gap-2">
                <span className="text-2xl font-bold text-brutal-white tracking-tight">
                    {data.value}
                </span>
                {data.unit && (
                    <span className="text-xs font-bold text-brutal-slate">{data.unit}</span>
                )}
            </div>
            {data.change !== undefined && data.change !== null && data.change !== 0 && (
                <div
                    className={cn(
                        'text-xs font-bold',
                        data.change > 0 ? 'text-signal-red' : 'text-signal-green'
                    )}
                >
                    {data.change > 0 ? '+' : ''}{data.change.toFixed(1)}%
                </div>
            )}
        </div>
    );
}
