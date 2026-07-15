import {useEffect, useState} from 'react';
import {cn} from '@/lib/utils';
import {HealthStatusValues} from '@ancplua/qyl-api-schema/types';
import {parseHealthReport} from '@/lib/contract-validation';

type ProbeState = 'checking' | 'healthy' | 'unhealthy';

const REFRESH_INTERVAL_MS = 30_000;

export function HealthIndicator() {
    const [state, setState] = useState<ProbeState>('checking');

    useEffect(() => {
        const controller = new AbortController();

        const probe = async () => {
            try {
                const response = await fetch('/health', {
                    cache: 'no-store',
                    signal: controller.signal,
                });
                const mediaType = response.headers.get('content-type')?.split(';', 1)[0].trim().toLowerCase();
                if (mediaType !== 'application/json') {
                    throw new Error(`Expected application/json, got ${mediaType ?? 'no content type'}`);
                }
                const report = parseHealthReport(await response.json() as unknown);
                setState(response.ok && report.status !== HealthStatusValues.unhealthy
                    ? 'healthy'
                    : 'unhealthy');
            } catch (error) {
                if (!(error instanceof DOMException && error.name === 'AbortError')) {
                    setState('unhealthy');
                }
            }
        };

        void probe();
        const interval = window.setInterval(() => void probe(), REFRESH_INTERVAL_MS);

        return () => {
            controller.abort();
            window.clearInterval(interval);
        };
    }, []);

    return (
        <div
            role="status"
            title={state === 'healthy' ? 'Collector ready' : state === 'checking' ? 'Checking collector' : 'Collector unavailable'}
            className={cn(
                'flex items-center gap-2 border px-2 py-1 text-[11px] font-semibold tracking-[0.08em]',
                state === 'healthy' && 'border-signal-green bg-signal-green/10 text-signal-green',
                state === 'unhealthy' && 'border-signal-red bg-signal-red/10 text-signal-red',
                state === 'checking' && 'border-brutal-zinc bg-brutal-dark text-brutal-slate',
            )}
        >
            <span className={cn(
                'h-2 w-2 rounded-full',
                state === 'healthy' && 'bg-signal-green',
                state === 'unhealthy' && 'bg-signal-red',
                state === 'checking' && 'animate-pulse bg-brutal-slate',
            )}/>
            {state === 'checking' ? 'CHECKING' : state === 'healthy' ? 'READY' : 'UNAVAILABLE'}
        </div>
    );
}
