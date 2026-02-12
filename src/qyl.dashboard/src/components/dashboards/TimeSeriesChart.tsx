import {Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis} from 'recharts';
import type {TimeSeriesPoint} from '@/hooks/use-dashboards';

interface TimeSeriesChartProps {
    title: string;
    data: TimeSeriesPoint[];
}

export function TimeSeriesChart({title, data}: TimeSeriesChartProps) {
    return (
        <div className="border-3 border-brutal-zinc bg-brutal-carbon p-4 space-y-3">
            <div className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                {title}
            </div>
            <div className="h-48">
                <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={data} margin={{top: 4, right: 4, bottom: 0, left: 0}}>
                        <defs>
                            <linearGradient id="fillValue" x1="0" y1="0" x2="0" y2="1">
                                <stop offset="5%" stopColor="hsl(25, 100%, 50%)" stopOpacity={0.3}/>
                                <stop offset="95%" stopColor="hsl(25, 100%, 50%)" stopOpacity={0}/>
                            </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" stroke="hsl(0, 0%, 20%)"/>
                        <XAxis
                            dataKey="time"
                            tick={{fill: 'hsl(0, 0%, 50%)', fontSize: 10}}
                            axisLine={{stroke: 'hsl(0, 0%, 25%)'}}
                            tickLine={false}
                        />
                        <YAxis
                            tick={{fill: 'hsl(0, 0%, 50%)', fontSize: 10}}
                            axisLine={{stroke: 'hsl(0, 0%, 25%)'}}
                            tickLine={false}
                            width={50}
                        />
                        <Tooltip
                            contentStyle={{
                                backgroundColor: 'hsl(0, 0%, 8%)',
                                border: '2px solid hsl(0, 0%, 25%)',
                                borderRadius: 0,
                                color: 'hsl(0, 0%, 90%)',
                                fontSize: 12,
                            }}
                        />
                        <Area
                            type="monotone"
                            dataKey="value"
                            stroke="hsl(25, 100%, 50%)"
                            strokeWidth={2}
                            fill="url(#fillValue)"
                        />
                    </AreaChart>
                </ResponsiveContainer>
            </div>
        </div>
    );
}
