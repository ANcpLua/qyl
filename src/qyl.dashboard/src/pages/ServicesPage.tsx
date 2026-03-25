import {useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {ArrowDown, ArrowUp, Globe} from 'lucide-react';
import {cn} from '@/lib/utils';
import {fetchJson} from '@/lib/api';
import {Badge} from '@/components/ui/badge';

interface ServiceInfo {
    serviceName: string;
    serviceType: string;
    latestVersion: string | null;
    firstSeen: string;
    lastSeen: string;
    lastErrorAt: string | null;
    spanCount: number;
    errorCount: number;
}

type SortField = keyof Pick<ServiceInfo, 'serviceName' | 'spanCount' | 'errorCount' | 'lastSeen'>;
type SortDir = 'asc' | 'desc';

function useServices() {
    return useQuery({
        queryKey: ['services'],
        queryFn: () => fetchJson<ServiceInfo[]>('/api/v1/services'),
        staleTime: 30_000,
    });
}

function formatTime(iso: string | null): string {
    if (!iso) return '\u2014';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
    });
}

export function ServicesPage() {
    const {data: services, isLoading} = useServices();
    const [sortField, setSortField] = useState<SortField>('lastSeen');
    const [sortDir, setSortDir] = useState<SortDir>('desc');

    const sorted = services?.slice().sort((a, b) => {
        const av = a[sortField], bv = b[sortField];
        if (av == null && bv == null) return 0;
        if (av == null) return 1;
        if (bv == null) return -1;
        const cmp = typeof av === 'number' ? av - (bv as number) : String(av).localeCompare(String(bv));
        return sortDir === 'asc' ? cmp : -cmp;
    });

    function toggleSort(field: SortField) {
        if (sortField === field) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
        else {
            setSortField(field);
            setSortDir('desc');
        }
    }

    function SortIcon({field}: { field: SortField }) {
        if (sortField !== field) return null;
        return sortDir === 'asc' ? <ArrowUp className="w-3 h-3"/> : <ArrowDown className="w-3 h-3"/>;
    }

    return (
        <div className="flex-1 p-6 space-y-6 overflow-auto">
            <h1 className="text-lg font-bold text-brutal-white tracking-wider uppercase">SERVICES</h1>

            <div className="border-3 border-brutal-zinc bg-brutal-carbon">
                <div className="overflow-x-auto">
                    <table className="w-full text-[11px]">
                        <thead>
                        <tr className="border-b border-brutal-zinc/70">
                            {([
                                ['serviceName', 'Service'],
                                ['spanCount', 'Spans'],
                                ['errorCount', 'Errors'],
                                ['lastSeen', 'Last Seen'],
                            ] as [SortField, string][]).map(([field, label]) => (
                                <th
                                    key={field}
                                    className="px-4 py-2 text-left font-bold text-brutal-slate tracking-widest uppercase cursor-pointer select-none hover:text-brutal-white"
                                    onClick={() => toggleSort(field)}
                                >
                                    <div className="flex items-center gap-1">
                                        {label}
                                        <SortIcon field={field}/>
                                    </div>
                                </th>
                            ))}
                            <th className="px-4 py-2 text-left font-bold text-brutal-slate tracking-widest uppercase">Version</th>
                            <th className="px-4 py-2 text-left font-bold text-brutal-slate tracking-widest uppercase">Status</th>
                        </tr>
                        </thead>
                        <tbody>
                        {sorted?.map(svc => {
                            const hasRecentError = svc.lastErrorAt && (Date.now() - new Date(svc.lastErrorAt).getTime()) < 3600_000;
                            return (
                                <tr key={svc.serviceName}
                                    className="border-b border-brutal-zinc/30 hover:bg-brutal-dark/60">
                                    <td className="px-4 py-2 font-semibold text-brutal-white">{svc.serviceName}</td>
                                    <td className="px-4 py-2 text-brutal-slate">{svc.spanCount.toLocaleString()}</td>
                                    <td className={cn('px-4 py-2', svc.errorCount > 0 ? 'text-signal-red font-semibold' : 'text-brutal-slate')}>
                                        {svc.errorCount.toLocaleString()}
                                    </td>
                                    <td className="px-4 py-2 text-brutal-slate">{formatTime(svc.lastSeen)}</td>
                                    <td className="px-4 py-2">
                                        {svc.latestVersion ? (
                                            <Badge variant="secondary"
                                                   className="text-[10px] bg-brutal-zinc/30 border-brutal-zinc">
                                                {svc.latestVersion}
                                            </Badge>
                                        ) : '\u2014'}
                                    </td>
                                    <td className="px-4 py-2">
                                        <Badge
                                            variant="secondary"
                                            className={cn(
                                                'text-[10px]',
                                                hasRecentError
                                                    ? 'bg-signal-red/20 text-signal-red border-signal-red/40'
                                                    : 'bg-signal-green/20 text-signal-green border-signal-green/40',
                                            )}
                                        >
                                            {hasRecentError ? 'DEGRADED' : 'HEALTHY'}
                                        </Badge>
                                    </td>
                                </tr>
                            );
                        })}
                        {!isLoading && (!sorted || sorted.length === 0) && (
                            <tr>
                                <td colSpan={6} className="px-4 py-12 text-center">
                                    <Globe className="w-8 h-8 mx-auto text-brutal-zinc mb-2"/>
                                    <div className="text-brutal-slate text-xs font-bold tracking-widest">NO SERVICES
                                        DETECTED
                                    </div>
                                    <div className="text-brutal-zinc text-[10px] mt-1">Services appear as telemetry
                                        arrives.
                                    </div>
                                </td>
                            </tr>
                        )}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
}
