import {useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {
    AlertCircle,
    ChevronRight,
    Filter,
    Loader2,
    ShieldAlert,
    ShieldCheck,
    ShieldQuestion,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {useIssues} from '@/hooks/use-issues';
import type {Issue} from '@/hooks/use-issues';

const statusStyles: Record<string, string> = {
    new: 'bg-red-500/20 text-red-400 border-red-500/40',
    acknowledged: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    resolved: 'bg-green-500/20 text-green-400 border-green-500/40',
    regressed: 'bg-purple-500/20 text-purple-400 border-purple-500/40',
    reopened: 'bg-orange-500/20 text-orange-400 border-orange-500/40',
};

function StatusBadge({status}: {status: string}) {
    return (
        <Badge variant="outline" className={cn('text-[10px] uppercase tracking-wider', statusStyles[status] ?? statusStyles.new)}>
            {status}
        </Badge>
    );
}

function formatTimestamp(iso?: string): string {
    if (!iso) return '—';
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
            <div className="w-40 h-4 bg-brutal-zinc rounded" />
            <div className="flex-1 h-4 bg-brutal-zinc rounded" />
            <div className="w-24 h-5 bg-brutal-zinc rounded" />
            <div className="w-16 h-4 bg-brutal-zinc rounded" />
            <div className="w-28 h-4 bg-brutal-zinc rounded" />
            <div className="w-28 h-4 bg-brutal-zinc rounded" />
            <div className="w-24 h-4 bg-brutal-zinc rounded" />
        </div>
    );
}

function IssueRow({issue, onClick}: {issue: Issue; onClick: () => void}) {
    return (
        <div
            className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
            onClick={onClick}
        >
            <div className="w-40 min-w-0">
                <span className="text-sm font-bold text-brutal-white truncate block">
                    {issue.error_type}
                </span>
            </div>

            <div className="flex-1 min-w-0">
                <span className="text-xs text-brutal-slate truncate block">
                    {issue.message}
                </span>
            </div>

            <div className="w-28">
                <StatusBadge status={issue.status} />
            </div>

            <div className="w-16 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {issue.event_count.toLocaleString()}
                </span>
            </div>

            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatTimestamp(issue.first_seen)}
                </span>
            </div>

            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatTimestamp(issue.last_seen)}
                </span>
            </div>

            <div className="w-24 text-right">
                <span className="text-xs text-brutal-slate truncate block">
                    {issue.owner ?? '—'}
                </span>
            </div>

            <ChevronRight className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0" />
        </div>
    );
}

export function IssuesPage() {
    const navigate = useNavigate();
    const [errorTypeFilter, setErrorTypeFilter] = useState('');
    const [statusFilter, setStatusFilter] = useState<string>('');

    const {data: issues, isLoading, error} = useIssues({
        errorType: errorTypeFilter || undefined,
        status: statusFilter && statusFilter !== 'all' ? statusFilter : undefined,
    });

    const totalIssues = issues?.length ?? 0;
    const newCount = issues?.filter((i) => i.status === 'new').length ?? 0;
    const acknowledgedCount = issues?.filter((i) => i.status === 'acknowledged').length ?? 0;
    const resolvedCount = issues?.filter((i) => i.status === 'resolved').length ?? 0;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500" />
                        <p className="text-red-400">Failed to load issues</p>
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
            <div className="grid grid-cols-3 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <ShieldAlert className="w-4 h-4 text-red-400" />
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">NEW</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate" />
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-red-400">{newCount}</div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <ShieldQuestion className="w-4 h-4 text-amber-400" />
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">ACKNOWLEDGED</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate" />
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-amber-400">{acknowledgedCount}</div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <ShieldCheck className="w-4 h-4 text-green-400" />
                            <span className="text-xs font-bold text-brutal-slate tracking-wider">RESOLVED</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate" />
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-green-400">{resolvedCount}</div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Filters */}
            <div className="flex items-center gap-4">
                <div className="relative flex-1 max-w-sm">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate" />
                    <Input
                        placeholder="Filter by error type…"
                        value={errorTypeFilter}
                        onChange={(e) => setErrorTypeFilter(e.target.value)}
                        className="pl-9"
                        aria-label="Filter by error type"
                    />
                </div>

                <Select value={statusFilter} onValueChange={setStatusFilter}>
                    <SelectTrigger className="w-40" aria-label="Filter by status">
                        <SelectValue placeholder="All statuses" />
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="all">All statuses</SelectItem>
                        <SelectItem value="new">New</SelectItem>
                        <SelectItem value="acknowledged">Acknowledged</SelectItem>
                        <SelectItem value="resolved">Resolved</SelectItem>
                        <SelectItem value="regressed">Regressed</SelectItem>
                        <SelectItem value="reopened">Reopened</SelectItem>
                    </SelectContent>
                </Select>

                <div className="text-xs font-bold text-brutal-slate tracking-wider">
                    {totalIssues} ISSUE{totalIssues !== 1 ? 'S' : ''}
                </div>
            </div>

            {/* Table */}
            <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                {/* Header */}
                <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="w-40">ERROR TYPE</div>
                    <div className="flex-1">MESSAGE</div>
                    <div className="w-28">STATUS</div>
                    <div className="w-16 text-right">EVENTS</div>
                    <div className="w-28 text-right">FIRST SEEN</div>
                    <div className="w-28 text-right">LAST SEEN</div>
                    <div className="w-24 text-right">OWNER</div>
                    <div className="w-4" />
                </div>

                {/* Body */}
                {isLoading ? (
                    <>
                        <SkeletonRow />
                        <SkeletonRow />
                        <SkeletonRow />
                        <SkeletonRow />
                        <SkeletonRow />
                    </>
                ) : !issues || issues.length === 0 ? (
                    <div className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-brutal-zinc" />
                        <p className="text-brutal-slate text-sm">No issues found</p>
                        <p className="text-brutal-zinc text-xs mt-1">Issues will appear as errors are grouped from your telemetry</p>
                    </div>
                ) : (
                    issues.map((issue) => (
                        <IssueRow
                            key={issue.issue_id}
                            issue={issue}
                            onClick={() => navigate(`/issues/${issue.issue_id}`)}
                        />
                    ))
                )}
            </div>
        </div>
    );
}
