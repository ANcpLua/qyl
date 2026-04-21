import {Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis} from 'recharts';
import type {TimeSeriesPoint} from '@/hooks/use-dashboards';

interface TimeSeriesChartProps {
    title: string;
    data: TimeSeriesPoint[];
}

// Chart styling using CSS custom properties (oklch tokens from index.css)
const CHART_STROKE = 'var(--color-signal-orange)';
const GRID_STROKE = 'var(--color-brutal-dark)';
const AXIS_TICK = {fill: 'var(--color-brutal-zinc)', fontSize: 10};
const AXIS_LINE = {stroke: 'var(--color-brutal-dark)'};
const TOOLTIP_STYLE: React.CSSProperties = {
    backgroundColor: 'var(--color-brutal-black)',
    border: '2px solid var(--color-brutal-dark)',
    borderRadius: 0,
    color: 'var(--color-brutal-white)',
    fontSize: 12,
};

export function TimeSeriesChart({title, data}: TimeSeriesChartProps) {
    return (
        <div className="border-3 border-brutal-zinc bg-brutal-carbon p-4 space-y-3">
            <div className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                {title}
            </div>
            <div className="h-48">
                <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={0}>
                    <AreaChart data={data} margin={{top: 4, right: 4, bottom: 0, left: 0}}>
                        <defs>
                            <linearGradient id="fillValue" x1="0" y1="0" x2="0" y2="1">
                                <stop offset="5%" stopColor="var(--color-signal-orange)" stopOpacity={0.3}/>
                                <stop offset="95%" stopColor="var(--color-signal-orange)" stopOpacity={0}/>
                            </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke={GRID_STROKE}/>
                        <XAxis
                            dataKey="time"
                            tick={AXIS_TICK}
                            axisLine={AXIS_LINE}
                            tickLine={false}
                        />
                        <YAxis
                            tick={AXIS_TICK}
                            axisLine={AXIS_LINE}
                            tickLine={false}
                            width={50}
                        />
                        <Tooltip contentStyle={TOOLTIP_STYLE}/>
                        <Area
                            type="monotone"
                            dataKey="value"
                            stroke={CHART_STROKE}
                            strokeWidth={2}
                            fill="url(#fillValue)"
                        />
                    </AreaChart>
                </ResponsiveContainer>
            </div>
        </div>
    );
}
