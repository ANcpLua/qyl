import {useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {
    AlertCircle,
    AlertTriangle,
    ChevronRight,
    Filter,
    Loader2,
    ShieldAlert,
    ShieldCheck,
    ShieldX
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import type {ErrorRow} from '@/hooks/use-errors';
import {useErrors, useErrorStats} from '@/hooks/use-errors';

const statusStyles: Record<string, string> = {
    new: 'bg-red-500/20 text-red-400 border-red-500/40',
    acknowledged: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    resolved: 'bg-green-500/20 text-green-400 border-green-500/40',
    ignored: 'bg-zinc-500/20 text-zinc-400 border-zinc-500/40',
};

function StatusBadge({status}: { status: string }) {
    return (
        <Badge variant="outline"
               className={cn('text-[10px] uppercase tracking-wider', statusStyles[status] ?? statusStyles.new)}>
            {status}
        </Badge>
    );
}

function formatTimestamp(iso?: string | null): string {
    if (!iso) return '\u2014';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
    });
}

function SkeletonRow() {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            <div className="w-36 h-4 bg-brutal-zinc rounded"/>
            <div className="flex-1 h-4 bg-brutal-zinc rounded"/>
            <div className="w-24 h-4 bg-brutal-zinc rounded"/>
            <div className="w-24 h-5 bg-brutal-zinc rounded"/>
            <div className="w-16 h-4 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
            <div className="w-24 h-4 bg-brutal-zinc rounded"/>
        </div>
    );
}

function ErrorRow({error, onClick}: { error: ErrorRow; onClick: () => void }) {
    return (
        <div
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            role="button"
            tabIndex={0}
            onClick={onClick}
            onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    onClick();
                }
            }}
        >
            <div className="w-36 min-w-0">
                <span className="text-sm font-bold text-brutal-white truncate block">
                    {error.errorType}
                </span>
            </div>

            <div className="flex-1 min-w-0">
                <span className="text-xs text-brutal-slate truncate block">
                    {error.message}
                </span>
            </div>

            <div className="w-24 min-w-0">
                <span className="text-xs text-brutal-slate truncate block font-mono">
                    {error.category}
                </span>
            </div>

            <div className="w-24">
                <StatusBadge status={error.status}/>
            </div>

            <div className="w-16 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {error.occurrenceCount.toLocaleString()}
                </span>
            </div>

            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatTimestamp(error.firstSeen)}
                </span>
            </div>

            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatTimestamp(error.lastSeen)}
                </span>
            </div>

            <div className="w-24 text-right">
                <span className="text-xs text-brutal-slate truncate block">
                    {error.assignedTo ?? '\u2014'}
                </span>
            </div>

            <ChevronRight
                className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

export function ErrorsOutagesPage() {
    const navigate = useNavigate();
    const [statusFilter, setStatusFilter] = useState<string>('');
    const [categoryFilter, setCategoryFilter] = useState('');
    const [serviceFilter, setServiceFilter] = useState('');

    const {data: errors, isLoading, error} = useErrors({
        status: statusFilter && statusFilter !== 'all' ? statusFilter : undefined,
        category: categoryFilter || undefined,
        serviceName: serviceFilter || undefined,
    });

    const {data: stats, isLoading: statsLoading} = useErrorStats();

    const totalErrors = stats?.totalCount ?? 0;
    const topCategories = stats?.byCategory.slice(0, 3) ?? [];

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load errors</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {error instanceof Error ? error.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return (
        <div className="p-6 space-y-6">
            {/* Stats cards */}
            <div className="grid grid-cols-4 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <ShieldAlert className="w-4 h-4 text-red-400"/>
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">TOTAL ERRORS</span>
                        </div>
                        {statsLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-red-400">{totalErrors}</div>
                        )}
                    </CardContent>
                </Card>

                {topCategories.map((cat) => (
                    <Card key={cat.category}>
                        <CardContent className="pt-4">
                            <div className="flex items-center gap-2">
                                {cat.category.toLowerCase().includes('error') ? (
                                    <ShieldX className="w-4 h-4 text-amber-400"/>
                                ) : cat.category.toLowerCase().includes('exception') ? (
                                    <AlertTriangle className="w-4 h-4 text-orange-400"/>
                                ) : (
                                    <ShieldCheck className="w-4 h-4 text-blue-400"/>
                                )}
                                <span className="text-xs font-bold text-brutal-slate tracking-wider uppercase truncate">
                                    {cat.category}
                                </span>
                            </div>
                            {statsLoading ? (
                                <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                            ) : (
                                <div className="text-2xl font-bold mt-1 text-brutal-white">{cat.count}</div>
                            )}
                        </CardContent>
                    </Card>
                ))}

                {/* Fill remaining cards with placeholders when fewer than 3 categories */}
                {topCategories.length < 3 &&
                    Array.from({length: 3 - topCategories.length}).map((_, i) => (
                        <Card key={`placeholder-${i}`}>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <ShieldCheck className="w-4 h-4 text-brutal-zinc"/>
                                    <span className="text-xs font-bold text-brutal-zinc tracking-wider">&mdash;</span>
                                </div>
                                <div className="text-2xl font-bold mt-1 text-brutal-zinc">0</div>
                            </CardContent>
                        </Card>
                    ))
                }
            </div>

            {/* Filters */}
            <div className="flex items-center gap-4">
                <Select value={statusFilter} onValueChange={setStatusFilter}>
                    <SelectTrigger className="w-40" aria-label="Filter by status">
                        <SelectValue placeholder="All statuses"/>
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="all">All statuses</SelectItem>
                        <SelectItem value="new">New</SelectItem>
                        <SelectItem value="acknowledged">Acknowledged</SelectItem>
                        <SelectItem value="resolved">Resolved</SelectItem>
                        <SelectItem value="ignored">Ignored</SelectItem>
                    </SelectContent>
                </Select>

                <div className="relative flex-1 max-w-xs">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate"/>
                    <Input
                        placeholder="Filter by category..."
                        value={categoryFilter}
                        onChange={(e) => setCategoryFilter(e.target.value)}
                        className="pl-9"
                        aria-label="Filter by category"
                    />
                </div>

                <div className="relative flex-1 max-w-xs">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate"/>
                    <Input
                        placeholder="Filter by service..."
                        value={serviceFilter}
                        onChange={(e) => setServiceFilter(e.target.value)}
                        className="pl-9"
                        aria-label="Filter by service name"
                    />
                </div>

                <div className="text-xs font-bold text-brutal-slate tracking-wider">
                    {errors?.length ?? 0} ERROR{(errors?.length ?? 0) !== 1 ? 'S' : ''}
                </div>
            </div>

            {/* Table */}
            <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                {/* Header */}
                <div
                    className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-36">ERROR TYPE</div>
                    <div className="flex-1">MESSAGE</div>
                    <div className="w-24">CATEGORY</div>
                    <div className="w-24">STATUS</div>
                    <div className="w-16 text-right">COUNT</div>
                    <div className="w-28 text-right">FIRST SEEN</div>
                    <div className="w-28 text-right">LAST SEEN</div>
                    <div className="w-24 text-right">ASSIGNED TO</div>
                    <div className="w-4"/>
                </div>

                {/* Body */}
                {isLoading ? (
                    <>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                    </>
                ) : !errors || errors.length === 0 ? (
                    <div className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">No errors found</p>
                        <p className="text-brutal-zinc text-xs mt-1">
                            Errors will appear as they are ingested from your telemetry pipeline
                        </p>
                    </div>
                ) : (
                    errors.map((err) => (
                        <ErrorRow
                            key={err.errorId}
                            error={err}
                            onClick={() => navigate(`/errors/${err.errorId}`)}
                        />
                    ))
                )}
            </div>
        </div>
    );
}
