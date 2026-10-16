import {useEffect, useMemo, useState} from 'react';
import {useInfiniteQuery, useQueryClient} from '@tanstack/react-query';
import {consumeSse, fetchJson, parseLogSsePayload} from '@/lib/api';
import {parseLogPage} from '@/lib/contract-validation';

interface LogFilters {
    query: string;
    service: string;
    severityMin?: number;
}

function filterParams(filters: LogFilters, streaming = false): URLSearchParams {
    const params = new URLSearchParams();
    if (filters.query) params.set('query', filters.query);
    if (filters.service) params.set('service_name', filters.service);
    if (filters.severityMin !== undefined) {
        params.set(streaming ? 'min_severity' : 'severity_min', String(filters.severityMin));
    }
    return params;
}

export function useLogHistory(filters: LogFilters, live: boolean) {
    const client = useQueryClient();
    const queryKey = useMemo(() => ['telemetry', 'logs', filters] as const, [filters]);
    const [isConnected, setIsConnected] = useState(false);
    const [streamError, setStreamError] = useState<string>();
    const history = useInfiniteQuery({
        queryKey,
        initialPageParam: undefined as string | undefined,
        queryFn: async ({pageParam, signal}) => {
            const params = filterParams(filters);
            params.set('limit', '250');
            if (pageParam) params.set('cursor', pageParam);
            const page = await fetchJson(`/api/v1/logs?${params}`, parseLogPage, {signal});
            if (page.has_more && (!page.next_cursor || page.next_cursor === pageParam)) {
                throw new Error('Collector returned an invalid log continuation cursor.');
            }
            return page;
        },
        getNextPageParam: (lastPage, _pages, _lastParam, params) =>
            lastPage.has_more && !params.includes(lastPage.next_cursor) ? lastPage.next_cursor : undefined,
        retry: false,
    });
    const lastPage = history.data?.pages.at(-1);
    const cursorError = lastPage?.has_more && history.data?.pageParams.includes(lastPage.next_cursor)
        ? 'Collector returned a repeated log continuation cursor.'
        : undefined;

    useEffect(() => {
        setIsConnected(false);
        setStreamError(undefined);
        if (!live) return;

        let stopped = false;
        let controller = new AbortController();
        let refreshTimer: ReturnType<typeof setTimeout> | undefined;
        let reconnectTimer: ReturnType<typeof setTimeout> | undefined;
        let lastEventId: string | undefined;
        const connect = async () => {
            controller = new AbortController();
            try {
                await consumeSse(
                    `/api/v1/stream/logs?${filterParams(filters, true)}`,
                    controller.signal,
                    () => {
                        setIsConnected(true);
                        setStreamError(undefined);
                    },
                    frame => {
                        if (parseLogSsePayload(frame).type !== 'log') return;
                        lastEventId = frame.id;
                        // The stream notifies; paginated queries remain the source of displayed records.
                        if (refreshTimer === undefined) {
                            refreshTimer = setTimeout(() => {
                                refreshTimer = undefined;
                                void client.invalidateQueries({queryKey, exact: true});
                            }, 250);
                        }
                    },
                    lastEventId,
                );
                throw new Error('Log stream closed.');
            } catch (error) {
                if (stopped || controller.signal.aborted) return;
                setIsConnected(false);
                setStreamError(error instanceof Error ? error.message : String(error));
                reconnectTimer = setTimeout(() => { void connect(); }, 3000);
            }
        };
        void connect();
        return () => {
            stopped = true;
            controller.abort();
            clearTimeout(refreshTimer);
            clearTimeout(reconnectTimer);
        };
    }, [client, filters, live, queryKey]);

    return {...history, isConnected, streamError, cursorError};
}
