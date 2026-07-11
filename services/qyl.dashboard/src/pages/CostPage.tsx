import {useMemo} from 'react';
import {CircleDollarSign} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {useSessions} from '@/hooks/use-telemetry';
import type {SessionEntity} from '@/types';

// GenAI cost, sourced exclusively from the real session surface (/api/v1/sessions →
// genai_usage). The collector aggregates per-session request counts, token totals and
// estimated cost through its pricing pipeline; anything beyond that (per-model cost split,
// budgets, time series) has no backing endpoint and deliberately does not appear here.

interface SessionCostRow {
    sessionId: string;
    state: string;
    requests: number;
    inputTokens: number;
    outputTokens: number;
    models: string[];
    providers: string[];
    costUsd: number | undefined;
}

function toCostRow(session: SessionEntity): SessionCostRow | null {
    const usage = session.genai_usage;
    if (!usage) return null;
    return {
        sessionId: session['session.id'],
        state: session.state,
        requests: usage.request_count,
        inputTokens: usage.total_input_tokens,
        outputTokens: usage.total_output_tokens,
        models: usage.models_used,
        providers: usage.providers_used,
        costUsd: usage.estimated_cost_usd,
    };
}

const usd = (value: number) =>
    value.toLocaleString('en-US', {style: 'currency', currency: 'USD', maximumFractionDigits: 4});

const count = (value: number) => value.toLocaleString('en-US');

function SummaryCard({label, value, accent}: { label: string; value: string; accent?: boolean }) {
    return (
        <Card className="border border-brutal-zinc/70 bg-brutal-carbon/92">
            <CardContent className="p-4">
                <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                    {label}
                </div>
                <div className={cn(
                    'mt-2 font-mono text-2xl tracking-[-0.02em]',
                    accent ? 'text-signal-orange' : 'text-brutal-white'
                )}>
                    {value}
                </div>
            </CardContent>
        </Card>
    );
}

export function CostPage() {
    const {data: sessions = [], isLoading} = useSessions();

    const rows = useMemo(
        () => sessions
            .map(toCostRow)
            .filter((row): row is SessionCostRow => row !== null)
            .sort((a, b) => (b.costUsd ?? 0) - (a.costUsd ?? 0)),
        [sessions]
    );

    const totals = useMemo(() => {
        const providers = new Set<string>();
        const models = new Set<string>();
        let requests = 0;
        let inputTokens = 0;
        let outputTokens = 0;
        let costUsd = 0;
        let hasCost = false;

        for (const row of rows) {
            requests += row.requests;
            inputTokens += row.inputTokens;
            outputTokens += row.outputTokens;
            row.providers.forEach((p) => providers.add(p));
            row.models.forEach((m) => models.add(m));
            if (row.costUsd !== undefined) {
                costUsd += row.costUsd;
                hasCost = true;
            }
        }

        return {requests, inputTokens, outputTokens, costUsd, hasCost, providers, models};
    }, [rows]);

    return (
        <div className="p-4 space-y-4">
            <div className="flex items-center gap-2">
                <CircleDollarSign className="w-4 h-4 text-signal-orange"/>
                <h2 className="text-sm font-semibold tracking-[0.14em] text-brutal-white">GENAI COST</h2>
                <span className="text-[11px] text-brutal-slate tracking-[0.08em]">
                    FROM SESSION USAGE · ESTIMATED VIA MODEL PRICING
                </span>
            </div>

            <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
                <SummaryCard
                    label="Estimated cost"
                    value={totals.hasCost ? usd(totals.costUsd) : '—'}
                    accent
                />
                <SummaryCard label="GenAI requests" value={count(totals.requests)}/>
                <SummaryCard label="Input tokens" value={count(totals.inputTokens)}/>
                <SummaryCard label="Output tokens" value={count(totals.outputTokens)}/>
            </div>

            {(totals.providers.size > 0 || totals.models.size > 0) && (
                <div className="flex flex-wrap items-center gap-1.5">
                    {[...totals.providers].sort().map((provider) => (
                        <Badge key={`p-${provider}`}
                               className="bg-signal-violet/15 text-signal-violet border border-signal-violet/40 text-[11px] tracking-[0.08em]">
                            {provider.toUpperCase()}
                        </Badge>
                    ))}
                    {[...totals.models].sort().map((model) => (
                        <Badge key={`m-${model}`}
                               className="bg-brutal-dark/85 text-brutal-slate border border-brutal-zinc text-[11px] tracking-[0.08em]">
                            {model}
                        </Badge>
                    ))}
                </div>
            )}

            <Card className="border border-brutal-zinc/70 bg-brutal-carbon/92">
                <CardContent className="p-0">
                    {isLoading ? (
                        <div className="p-6 text-sm text-brutal-slate">Loading sessions…</div>
                    ) : rows.length === 0 ? (
                        <div className="p-6 text-sm text-brutal-slate">
                            No GenAI usage recorded yet. Sessions gain cost data once spans carry
                            gen_ai token usage and a pricing profile matches the model.
                        </div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full text-left text-xs">
                                <thead>
                                <tr className="border-b border-brutal-zinc/70 text-[11px] uppercase tracking-[0.18em] text-brutal-slate">
                                    <th className="px-4 py-2.5 font-semibold">Session</th>
                                    <th className="px-4 py-2.5 font-semibold">State</th>
                                    <th className="px-4 py-2.5 font-semibold text-right">Requests</th>
                                    <th className="px-4 py-2.5 font-semibold text-right">Tokens in</th>
                                    <th className="px-4 py-2.5 font-semibold text-right">Tokens out</th>
                                    <th className="px-4 py-2.5 font-semibold">Providers</th>
                                    <th className="px-4 py-2.5 font-semibold text-right">Est. cost</th>
                                </tr>
                                </thead>
                                <tbody>
                                {rows.map((row) => (
                                    <tr key={row.sessionId}
                                        className="border-b border-brutal-zinc/40 hover:bg-brutal-dark/60">
                                        <td className="px-4 py-2 font-mono text-brutal-white">{row.sessionId}</td>
                                        <td className="px-4 py-2 uppercase text-brutal-slate">{row.state}</td>
                                        <td className="px-4 py-2 text-right font-mono text-brutal-white">{count(row.requests)}</td>
                                        <td className="px-4 py-2 text-right font-mono text-brutal-white">{count(row.inputTokens)}</td>
                                        <td className="px-4 py-2 text-right font-mono text-brutal-white">{count(row.outputTokens)}</td>
                                        <td className="px-4 py-2 text-brutal-slate">{row.providers.join(', ')}</td>
                                        <td className="px-4 py-2 text-right font-mono text-signal-orange">
                                            {row.costUsd !== undefined ? usd(row.costUsd) : '—'}
                                        </td>
                                    </tr>
                                ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
