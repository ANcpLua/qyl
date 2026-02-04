import {useEffect, useState} from 'react';
import {Activity, AlertTriangle, CheckCircle, Database, HardDrive, MemoryStick, Server, XCircle} from 'lucide-react';
import {cn} from '@/lib/utils';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';

interface ComponentHealth {
    name: string;
    status: 'healthy' | 'degraded' | 'unhealthy';
    message?: string;
    data?: Record<string, unknown>;
}

interface HealthUiResponse {
    status: 'healthy' | 'degraded' | 'unhealthy';
    components: ComponentHealth[];
    uptimeSeconds: number;
    version: string;
    lastIngestionTime?: string;
    checkedAt: string;
}

const REFRESH_INTERVAL = 30_000; // 30 seconds

function formatUptime(seconds: number): string {
    const days = Math.floor(seconds / 86400);
    const hours = Math.floor((seconds % 86400) / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    if (days > 0) {
        return `${days}d ${hours}h ${minutes}m`;
    }
    if (hours > 0) {
        return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
}

function formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

function formatRelativeTime(isoString: string): string {
    const date = new Date(isoString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffSeconds = Math.floor(diffMs / 1000);

    if (diffSeconds < 60) return `${diffSeconds}s ago`;
    if (diffSeconds < 3600) return `${Math.floor(diffSeconds / 60)}m ago`;
    if (diffSeconds < 86400) return `${Math.floor(diffSeconds / 3600)}h ago`;
    return `${Math.floor(diffSeconds / 86400)}d ago`;
}

function StatusIcon({status}: { status: string }) {
    switch (status) {
        case 'healthy':
            return <CheckCircle className="w-4 h-4 text-signal-green"/>;
        case 'degraded':
            return <AlertTriangle className="w-4 h-4 text-signal-yellow"/>;
        case 'unhealthy':
            return <XCircle className="w-4 h-4 text-signal-red"/>;
        default:
            return <Activity className="w-4 h-4 text-brutal-slate"/>;
    }
}

function ComponentIcon({name}: { name: string }) {
    switch (name) {
        case 'duckdb':
            return <Database className="w-4 h-4"/>;
        case 'disk':
            return <HardDrive className="w-4 h-4"/>;
        case 'memory':
            return <MemoryStick className="w-4 h-4"/>;
        case 'ingestion':
            return <Activity className="w-4 h-4"/>;
        default:
            return <Server className="w-4 h-4"/>;
    }
}

function ComponentCard({component}: { component: ComponentHealth }) {
    const statusColors = {
        healthy: 'border-signal-green/50 bg-signal-green/5',
        degraded: 'border-signal-yellow/50 bg-signal-yellow/5',
        unhealthy: 'border-signal-red/50 bg-signal-red/5',
    };

    return (
        <div className={cn(
            'border-2 p-3 space-y-2',
            statusColors[component.status]
        )}>
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                    <ComponentIcon name={component.name}/>
                    <span className="font-bold text-xs tracking-wider uppercase text-brutal-white">
                        {component.name}
                    </span>
                </div>
                <StatusIcon status={component.status}/>
            </div>

            {component.message && (
                <p className="text-xs text-brutal-slate">{component.message}</p>
            )}

            {component.data && (
                <div className="space-y-1 pt-1 border-t border-brutal-zinc/50">
                    {Object.entries(component.data).map(([key, value]) => (
                        <div key={key} className="flex justify-between text-xs">
                            <span className="text-brutal-slate">{formatDataKey(key)}</span>
                            <span className="font-mono text-brutal-white">{formatDataValue(key, value)}</span>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

function formatDataKey(key: string): string {
    return key
        .replace(/([A-Z])/g, ' $1')
        .replace(/^./, str => str.toUpperCase())
        .trim();
}

function formatDataValue(key: string, value: unknown): string {
    if (typeof value === 'number') {
        if (key.toLowerCase().includes('bytes')) {
            return formatBytes(value);
        }
        if (key.toLowerCase().includes('percent')) {
            return `${value}%`;
        }
        if (key.toLowerCase().includes('mb') || key.toLowerCase().includes('gb')) {
            return value.toFixed(2);
        }
        if (Number.isInteger(value)) {
            return value.toLocaleString();
        }
        return value.toFixed(2);
    }
    return String(value);
}

export function HealthIndicator() {
    const [health, setHealth] = useState<HealthUiResponse | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchHealth = async () => {
        try {
            const response = await fetch('/health/ui');
            if (!response.ok) {
                throw new Error(`Health check failed: ${response.status}`);
            }
            const data = await response.json();
            setHealth(data);
            setError(null);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Unknown error');
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        fetchHealth();
        const interval = setInterval(fetchHealth, REFRESH_INTERVAL);
        return () => clearInterval(interval);
    }, []);

    const statusColors = {
        healthy: 'bg-signal-green',
        degraded: 'bg-signal-yellow',
        unhealthy: 'bg-signal-red',
    };

    const statusBorderColors = {
        healthy: 'border-signal-green hover:bg-signal-green/10',
        degraded: 'border-signal-yellow hover:bg-signal-yellow/10',
        unhealthy: 'border-signal-red hover:bg-signal-red/10',
    };

    if (isLoading && !health) {
        return (
            <div
                className="flex items-center gap-2 px-2 py-1 border-2 border-brutal-zinc bg-brutal-dark cursor-wait">
                <div className="w-2 h-2 rounded-full bg-brutal-slate animate-pulse"/>
                <span className="text-xs font-bold tracking-wider text-brutal-slate">CHECKING...</span>
            </div>
        );
    }

    if (error && !health) {
        return (
            <div
                className="flex items-center gap-2 px-2 py-1 border-2 border-signal-red bg-brutal-dark cursor-help"
                title={error}
            >
                <div className="w-2 h-2 rounded-full bg-signal-red"/>
                <span className="text-xs font-bold tracking-wider text-signal-red">ERROR</span>
            </div>
        );
    }

    if (!health) return null;

    return (
        <Dialog>
            <DialogTrigger asChild>
                <button
                    className={cn(
                        'flex items-center gap-2 px-2 py-1 border-2 bg-brutal-dark transition-colors',
                        statusBorderColors[health.status]
                    )}
                >
                    <div className={cn('w-2 h-2 rounded-full', statusColors[health.status])}/>
                    <span className="text-xs font-bold tracking-wider text-brutal-white uppercase">
                        {health.status}
                    </span>
                </button>
            </DialogTrigger>

            <DialogContent
                className="bg-brutal-carbon border-2 border-brutal-zinc max-w-lg max-h-[80vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-3 text-brutal-white">
                        <Server className="w-5 h-5"/>
                        <span className="font-bold tracking-wider">SYSTEM HEALTH</span>
                        <div className={cn(
                            'ml-auto px-2 py-0.5 text-xs font-bold tracking-wider uppercase',
                            health.status === 'healthy' && 'bg-signal-green/20 text-signal-green',
                            health.status === 'degraded' && 'bg-signal-yellow/20 text-signal-yellow',
                            health.status === 'unhealthy' && 'bg-signal-red/20 text-signal-red'
                        )}>
                            {health.status}
                        </div>
                    </DialogTitle>
                    <DialogDescription className="text-brutal-slate">
                        Collector v{health.version} - Uptime: {formatUptime(health.uptimeSeconds)}
                    </DialogDescription>
                </DialogHeader>

                <div className="space-y-4 mt-4">
                    {/* Quick Stats */}
                    <div className="grid grid-cols-2 gap-2 text-xs">
                        <div className="p-2 bg-brutal-dark border border-brutal-zinc">
                            <div className="text-brutal-slate">Last Ingestion</div>
                            <div className="font-mono text-brutal-white">
                                {health.lastIngestionTime
                                    ? formatRelativeTime(health.lastIngestionTime)
                                    : 'No data yet'}
                            </div>
                        </div>
                        <div className="p-2 bg-brutal-dark border border-brutal-zinc">
                            <div className="text-brutal-slate">Checked At</div>
                            <div className="font-mono text-brutal-white">
                                {formatRelativeTime(health.checkedAt)}
                            </div>
                        </div>
                    </div>

                    {/* Components */}
                    <div className="space-y-2">
                        <h3 className="text-xs font-bold tracking-wider text-brutal-slate uppercase">
                            Components
                        </h3>
                        <div className="space-y-2">
                            {health.components.map((component) => (
                                <ComponentCard key={component.name} component={component}/>
                            ))}
                        </div>
                    </div>

                    {/* Footer */}
                    <div className="text-xs text-brutal-slate text-center pt-2 border-t border-brutal-zinc">
                        Auto-refreshes every 30 seconds
                    </div>
                </div>
            </DialogContent>
        </Dialog>
    );
}
