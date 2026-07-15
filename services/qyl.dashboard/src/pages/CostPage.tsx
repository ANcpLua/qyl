import {useMemo} from 'react';
import {Gauge} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {OnboardingHint} from '@/components/OnboardingHint';
import {EtlAuditView} from '@/components/cost/EtlAuditView';
import {useSessions} from '@/hooks/use-telemetry';
import type {SessionEntity} from '@/types';

// Observed GenAI usage, sourced exclusively from the real session surface
// (/api/v1/sessions → genai_usage). Provider billing totals remain source-level and
// are never allocated to these session counters.

interface SessionUsageRow {
    sessionId: string;
    state: string;
    requests: number;
    inputTokens: number;
    outputTokens: number;
    models: string[];
    providers: string[];
}

function toUsageRow(session: SessionEntity): SessionUsageRow | null {
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
    };
}

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

function UsageOverview() {
    const {data: sessions = [], isLoading} = useSessions();

    const rows = useMemo(
        () => sessions
            .map(toUsageRow)
            .filter((row): row is SessionUsageRow => row !== null)
            .sort((a, b) => b.requests - a.requests
                || (b.inputTokens + b.outputTokens) - (a.inputTokens + a.outputTokens)),
        [sessions]
    );

    const totals = useMemo(() => {
        const providers = new Set<string>();
        const models = new Set<string>();
        let requests = 0;
        let inputTokens = 0;
        let outputTokens = 0;

        for (const row of rows) {
            requests += row.requests;
            inputTokens += row.inputTokens;
            outputTokens += row.outputTokens;
            row.providers.forEach((p) => providers.add(p));
            row.models.forEach((m) => models.add(m));
        }

        return {requests, inputTokens, outputTokens, providers, models};
    }, [rows]);

    return (
        <div className="space-y-4">
            <div className="flex items-center gap-2">
                <Gauge className="w-4 h-4 text-signal-orange"/>
                <h2 className="text-sm font-semibold tracking-[0.14em] text-brutal-white">GENAI USAGE</h2>
                <span className="text-[11px] text-brutal-slate tracking-[0.08em]">
                    OBSERVED OTLP TOKEN COUNTERS · COST UNPRICED
                </span>
            </div>

            <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
                <SummaryCard label="GenAI requests" value={count(totals.requests)} accent/>
                <SummaryCard label="Input tokens" value={count(totals.inputTokens)}/>
                <SummaryCard label="Output tokens" value={count(totals.outputTokens)}/>
                <SummaryCard label="Models" value={count(totals.models.size)}/>
            </div>

            <div className="border border-signal-orange/35 bg-signal-orange/5 px-4 py-3 text-xs leading-5 text-brutal-slate">
                Session token counters are intentionally unpriced. Provider billing totals remain source-level in the ETL
                audit and are never allocated to sessions or traces; missing cost is never treated as zero.
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
                        <OnboardingHint
                            icon={Gauge}
                            title="No GenAI usage recorded yet"
                            description="Sessions appear once spans carry GenAI request and token usage attributes."
                        />
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

export function CostPage() {
    return (
        <div className="p-4">
            <Tabs defaultValue="overview">
                <TabsList aria-label="Cost views"
                          className="mb-4 h-auto border border-brutal-zinc/70 bg-brutal-carbon/92 p-1">
                    <TabsTrigger value="overview"
                                 className="px-4 py-2 text-xs uppercase tracking-[0.12em] data-[active]:bg-signal-orange data-[active]:text-brutal-black">
                        Usage overview
                    </TabsTrigger>
                    <TabsTrigger value="etl-audit"
                                 className="px-4 py-2 text-xs uppercase tracking-[0.12em] data-[active]:bg-signal-orange data-[active]:text-brutal-black">
                        ETL audit
                    </TabsTrigger>
                </TabsList>
                <TabsContent value="overview" className="mt-0">
                    <UsageOverview/>
                </TabsContent>
                <TabsContent value="etl-audit" className="mt-0">
                    <EtlAuditView/>
                </TabsContent>
            </Tabs>
        </div>
    );
}
