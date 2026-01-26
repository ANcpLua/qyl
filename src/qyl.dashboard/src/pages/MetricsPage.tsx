import {useState} from 'react';
import {Activity, BarChart3, Clock, TrendingDown, TrendingUp, Zap,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader, CardTitle} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue,} from '@/components/ui/select';
import {
    Area,
    AreaChart,
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    Legend,
    Line,
    LineChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts';

// Brutalist signal palette - oklch for perceptual uniformity
const CHART_COLORS = {
    orange: 'oklch(0.70 0.20 45)',
    green: 'oklch(0.85 0.25 145)',
    violet: 'oklch(0.60 0.25 300)',
    cyan: 'oklch(0.80 0.15 210)',
    yellow: 'oklch(0.90 0.18 95)',
    red: 'oklch(0.65 0.25 25)',
};

// Model-specific colors for bar charts (2026 models)
const MODEL_COLORS: Record<string, string> = {
    'gpt-5.2-high': CHART_COLORS.orange,
    'gpt-5.2': CHART_COLORS.green,
    'claude-opus-4.5': CHART_COLORS.violet,
    'gemini-3-pro': CHART_COLORS.cyan,
};

// Mock data for charts
const latencyData = [
    {time: '00:00', p50: 45, p95: 120, p99: 250},
    {time: '00:05', p50: 52, p95: 135, p99: 280},
    {time: '00:10', p50: 48, p95: 125, p99: 260},
    {time: '00:15', p50: 55, p95: 145, p99: 310},
    {time: '00:20', p50: 42, p95: 110, p99: 220},
    {time: '00:25', p50: 50, p95: 130, p99: 270},
    {time: '00:30', p50: 47, p95: 122, p99: 255},
];

const throughputData = [
    {time: '00:00', requests: 1200, errors: 12},
    {time: '00:05', requests: 1350, errors: 8},
    {time: '00:10', requests: 1180, errors: 15},
    {time: '00:15', requests: 1420, errors: 22},
    {time: '00:20', requests: 1280, errors: 10},
    {time: '00:25', requests: 1390, errors: 18},
    {time: '00:30', requests: 1310, errors: 14},
];

const tokenUsageData = [
    {time: '00:00', input: 45000, output: 12000},
    {time: '00:05', input: 52000, output: 15000},
    {time: '00:10', input: 48000, output: 13500},
    {time: '00:15', input: 61000, output: 18000},
    {time: '00:20', input: 55000, output: 16000},
    {time: '00:25', input: 58000, output: 17000},
    {time: '00:30', input: 50000, output: 14500},
];

const modelUsageData = [
    {model: 'gpt-5.2-high', requests: 450, tokens: 1250000, cost: 18.75},
    {model: 'gpt-5.2', requests: 820, tokens: 890000, cost: 6.23},
    {model: 'claude-opus-4.5', requests: 320, tokens: 780000, cost: 15.60},
    {model: 'gemini-3-pro', requests: 150, tokens: 450000, cost: 2.25},
];

interface StatCardProps {
    title: string;
    value: string | number;
    change?: number;
    icon: typeof Activity;
    iconColor?: string;
}

function StatCard({title, value, change, icon: Icon, iconColor = 'text-primary'}: StatCardProps) {
    return (
        <Card>
            <CardContent className="pt-4">
                <div className="flex items-center justify-between">
                    <div>
                        <p className="text-sm text-muted-foreground">{title}</p>
                        <p className="text-2xl font-bold mt-1">{value}</p>
                        {change !== undefined && (
                            <div className={cn(
                                'flex items-center gap-1 text-sm mt-1',
                                change >= 0 ? 'text-green-500' : 'text-red-500'
                            )}>
                                {change >= 0 ? (
                                    <TrendingUp className="w-4 h-4"/>
                                ) : (
                                    <TrendingDown className="w-4 h-4"/>
                                )}
                                <span>{Math.abs(change)}%</span>
                            </div>
                        )}
                    </div>
                    <Icon className={cn('w-8 h-8', iconColor)}/>
                </div>
            </CardContent>
        </Card>
    );
}

export function MetricsPage() {
    const [timeRange, setTimeRange] = useState('1h');

    return (
        <div className="p-6 space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <h2 className="text-lg font-semibold">Metrics Overview</h2>
                <Select value={timeRange} onValueChange={setTimeRange}>
                    <SelectTrigger className="w-32">
                        <SelectValue/>
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="15m">Last 15m</SelectItem>
                        <SelectItem value="1h">Last 1h</SelectItem>
                        <SelectItem value="6h">Last 6h</SelectItem>
                        <SelectItem value="24h">Last 24h</SelectItem>
                    </SelectContent>
                </Select>
            </div>

            {/* Stats row */}
            <div className="grid grid-cols-4 gap-4">
                <StatCard
                    title="Total Requests"
                    value="8,130"
                    change={12.5}
                    icon={Activity}
                    iconColor="text-cyan-500"
                />
                <StatCard
                    title="Avg Latency"
                    value="48ms"
                    change={-8.2}
                    icon={Clock}
                    iconColor="text-green-500"
                />
                <StatCard
                    title="Error Rate"
                    value="1.2%"
                    change={0.3}
                    icon={BarChart3}
                    iconColor="text-yellow-500"
                />
                <StatCard
                    title="GenAI Cost"
                    value="$27.70"
                    change={15.8}
                    icon={Zap}
                    iconColor="text-violet-500"
                />
            </div>

            {/* Charts */}
            <Tabs defaultValue="latency" className="space-y-4">
                <TabsList>
                    <TabsTrigger value="latency">Latency</TabsTrigger>
                    <TabsTrigger value="throughput">Throughput</TabsTrigger>
                    <TabsTrigger value="genai">GenAI Usage</TabsTrigger>
                </TabsList>

                <TabsContent value="latency">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Request Latency (percentiles)</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="h-80">
                                <ResponsiveContainer width="100%" height="100%">
                                    <LineChart data={latencyData}>
                                        <CartesianGrid strokeDasharray="3 3" stroke="oklch(0.35 0 0)"/>
                                        <XAxis
                                            dataKey="time"
                                            stroke="oklch(0.70 0 0)"
                                            fontSize={12}
                                        />
                                        <YAxis
                                            stroke="oklch(0.70 0 0)"
                                            fontSize={12}
                                            tickFormatter={(value) => `${value}ms`}
                                        />
                                        <Tooltip
                                            contentStyle={{
                                                backgroundColor: 'oklch(0.20 0 0)',
                                                border: '1px solid oklch(0.35 0 0)',
                                                borderRadius: '8px',
                                            }}
                                            labelStyle={{color: 'oklch(0.97 0 0)'}}
                                        />
                                        <Legend/>
                                        <Line
                                            type="monotone"
                                            dataKey="p50"
                                            name="p50"
                                            stroke={CHART_COLORS.orange}
                                            strokeWidth={2}
                                            dot={false}
                                        />
                                        <Line
                                            type="monotone"
                                            dataKey="p95"
                                            name="p95"
                                            stroke={CHART_COLORS.violet}
                                            strokeWidth={2}
                                            dot={false}
                                        />
                                        <Line
                                            type="monotone"
                                            dataKey="p99"
                                            name="p99"
                                            stroke={CHART_COLORS.yellow}
                                            strokeWidth={2}
                                            dot={false}
                                        />
                                    </LineChart>
                                </ResponsiveContainer>
                            </div>
                        </CardContent>
                    </Card>
                </TabsContent>

                <TabsContent value="throughput">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Request Throughput</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="h-80">
                                <ResponsiveContainer width="100%" height="100%">
                                    <AreaChart data={throughputData}>
                                        <CartesianGrid strokeDasharray="3 3" stroke="oklch(0.35 0 0)"/>
                                        <XAxis
                                            dataKey="time"
                                            stroke="oklch(0.70 0 0)"
                                            fontSize={12}
                                        />
                                        <YAxis
                                            stroke="oklch(0.70 0 0)"
                                            fontSize={12}
                                        />
                                        <Tooltip
                                            contentStyle={{
                                                backgroundColor: 'oklch(0.20 0 0)',
                                                border: '1px solid oklch(0.35 0 0)',
                                                borderRadius: '8px',
                                            }}
                                        />
                                        <Legend/>
                                        <Area
                                            type="monotone"
                                            dataKey="requests"
                                            name="Requests"
                                            stroke={CHART_COLORS.orange}
                                            fill="oklch(0.70 0.20 45 / 0.3)"
                                        />
                                        <Area
                                            type="monotone"
                                            dataKey="errors"
                                            name="Errors"
                                            stroke={CHART_COLORS.red}
                                            fill="oklch(0.65 0.25 25 / 0.3)"
                                        />
                                    </AreaChart>
                                </ResponsiveContainer>
                            </div>
                        </CardContent>
                    </Card>
                </TabsContent>

                <TabsContent value="genai">
                    <div className="grid grid-cols-2 gap-4">
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base">Token Usage Over Time</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <div className="h-64">
                                    <ResponsiveContainer width="100%" height="100%">
                                        <AreaChart data={tokenUsageData}>
                                            <CartesianGrid strokeDasharray="3 3" stroke="oklch(0.35 0 0)"/>
                                            <XAxis
                                                dataKey="time"
                                                stroke="oklch(0.70 0 0)"
                                                fontSize={12}
                                            />
                                            <YAxis
                                                stroke="oklch(0.70 0 0)"
                                                fontSize={12}
                                                tickFormatter={(value) => `${(value / 1000).toFixed(0)}k`}
                                            />
                                            <Tooltip
                                                contentStyle={{
                                                    backgroundColor: 'oklch(0.20 0 0)',
                                                    border: '1px solid oklch(0.35 0 0)',
                                                    borderRadius: '8px',
                                                }}
                                            />
                                            <Legend/>
                                            <Area
                                                type="monotone"
                                                dataKey="input"
                                                name="Input Tokens"
                                                stroke={CHART_COLORS.cyan}
                                                fill="oklch(0.80 0.15 210 / 0.3)"
                                            />
                                            <Area
                                                type="monotone"
                                                dataKey="output"
                                                name="Output Tokens"
                                                stroke={CHART_COLORS.green}
                                                fill="oklch(0.85 0.25 145 / 0.3)"
                                            />
                                        </AreaChart>
                                    </ResponsiveContainer>
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base">Model Usage</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <div className="h-64">
                                    <ResponsiveContainer width="100%" height="100%">
                                        <BarChart data={modelUsageData} layout="vertical">
                                            <CartesianGrid strokeDasharray="3 3" stroke="oklch(0.35 0 0)"/>
                                            <XAxis type="number" stroke="oklch(0.70 0 0)" fontSize={12}/>
                                            <YAxis
                                                type="category"
                                                dataKey="model"
                                                stroke="oklch(0.70 0 0)"
                                                fontSize={12}
                                                width={100}
                                            />
                                            <Tooltip
                                                contentStyle={{
                                                    backgroundColor: 'oklch(0.20 0 0)',
                                                    border: '1px solid oklch(0.35 0 0)',
                                                    borderRadius: '8px',
                                                }}
                                            />
                                            <Bar dataKey="requests" name="Requests">
                                                {modelUsageData.map((entry) => (
                                                    <Cell
                                                        key={entry.model}
                                                        fill={MODEL_COLORS[entry.model] ?? 'oklch(0.55 0 0)'}
                                                    />
                                                ))}
                                            </Bar>
                                        </BarChart>
                                    </ResponsiveContainer>
                                </div>
                            </CardContent>
                        </Card>
                    </div>

                    {/* Model breakdown table */}
                    <Card className="mt-4">
                        <CardHeader>
                            <CardTitle className="text-base">Model Cost Breakdown</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <table className="w-full">
                                <thead>
                                <tr className="border-b border-border">
                                    <th className="text-left py-2 text-sm font-medium text-muted-foreground">Model</th>
                                    <th className="text-right py-2 text-sm font-medium text-muted-foreground">Requests</th>
                                    <th className="text-right py-2 text-sm font-medium text-muted-foreground">Tokens</th>
                                    <th className="text-right py-2 text-sm font-medium text-muted-foreground">Cost</th>
                                </tr>
                                </thead>
                                <tbody>
                                {modelUsageData.map((model) => (
                                    <tr key={model.model} className="border-b border-border">
                                        <td className="py-3">
                                            <Badge variant="outline">{model.model}</Badge>
                                        </td>
                                        <td className="text-right font-mono">{model.requests.toLocaleString()}</td>
                                        <td className="text-right font-mono">{(model.tokens / 1000).toFixed(0)}k</td>
                                        <td className="text-right font-mono text-green-500">
                                            ${model.cost.toFixed(2)}
                                        </td>
                                    </tr>
                                ))}
                                </tbody>
                                <tfoot>
                                <tr>
                                    <td className="py-3 font-medium">Total</td>
                                    <td className="text-right font-mono font-medium">
                                        {modelUsageData.reduce((a, b) => a + b.requests, 0).toLocaleString()}
                                    </td>
                                    <td className="text-right font-mono font-medium">
                                        {(modelUsageData.reduce((a, b) => a + b.tokens, 0) / 1000).toFixed(0)}k
                                    </td>
                                    <td className="text-right font-mono font-medium text-green-500">
                                        ${modelUsageData.reduce((a, b) => a + b.cost, 0).toFixed(2)}
                                    </td>
                                </tr>
                                </tfoot>
                            </table>
                        </CardContent>
                    </Card>
                </TabsContent>
            </Tabs>
        </div>
    );
}
