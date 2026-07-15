import {useEffect, useMemo, useState, type FormEvent} from 'react';
import {
    AlertTriangle,
    BarChart3,
    Calculator,
    CheckCircle2,
    CircleDashed,
    DatabaseZap,
    FlaskConical,
    ShieldCheck,
    XCircle,
} from 'lucide-react';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {Card, CardContent} from '@/components/ui/card';
import {Input} from '@/components/ui/input';
import {cn} from '@/lib/utils';
import {
    normalizeProjectScope,
    useEvaluateGenAiEtlAudit,
    useGenAiEtlAudit,
} from '@/hooks/use-cost-audit';
import type {
    GenAiEtlAuditCluster,
    GenAiEtlAuditEvaluationRequest,
    GenAiEtlCatalogTokenCostEstimate,
    GenAiEtlClusterEvaluation,
    GenAiEtlClusterScenario,
    GenAiEtlPromotionGateKind,
    GenAiEtlPromotionGateState,
    ModelCatalogSource,
    ProviderBillingAttribution,
    ProviderBillingSource,
} from '@/types';

const integer = new Intl.NumberFormat('en-US', {maximumFractionDigits: 0});
const decimal = new Intl.NumberFormat('en-US', {maximumFractionDigits: 2});

const gateOrder: readonly GenAiEtlPromotionGateKind[] = [
    'contract_stability',
    'offline_replay',
    'calibrated_confidence',
    'shadow_traffic',
    'limited_serving',
    'rollback_residual_policy',
];

const gateLabels: Record<GenAiEtlPromotionGateKind, string> = {
    contract_stability: 'Contract stability',
    offline_replay: 'Offline replay',
    calibrated_confidence: 'Calibrated confidence',
    shadow_traffic: 'Shadow traffic',
    limited_serving: 'Limited serving',
    rollback_residual_policy: 'Rollback + residual policy',
};

export function humanize(value: string): string {
    return value.replaceAll('_', ' ');
}

export function formatUsd(value: number | undefined): string {
    if (value === undefined) return 'Unavailable';
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 2,
        maximumFractionDigits: Math.abs(value) < 0.01 && value !== 0 ? 6 : 2,
    }).format(value);
}

function formatRatio(value: number | undefined): string {
    if (value === undefined) return 'Unavailable';
    return new Intl.NumberFormat('en-US', {
        style: 'percent',
        minimumFractionDigits: 0,
        maximumFractionDigits: 1,
    }).format(value);
}

function formatTimestamp(value: string | undefined): string {
    if (!value) return 'Never';
    return new Date(value).toLocaleString('en-US', {
        dateStyle: 'medium',
        timeStyle: 'short',
    });
}

function formatUtcDay(value: string): string {
    return new Date(value).toLocaleDateString('en-US', {dateStyle: 'medium', timeZone: 'UTC'});
}

function costAttributionCopy(attribution: ProviderBillingAttribution): string {
    switch (attribution) {
        case 'provider_model_period':
            return 'Provider-reported model/period cost; it is not a per-request price.';
        case 'provider_period':
            return 'Provider aggregate period cost; Qyl does not invent a model or trace allocation.';
        case 'unavailable':
            return 'Unavailable because provider cost cannot be defensibly attributed to this workflow.';
    }
}

function catalogEstimateCopy(estimate: GenAiEtlCatalogTokenCostEstimate): string {
    switch (estimate.status) {
        case 'calculated':
            return 'Catalog token estimate priced from a live catalog snapshot with full provenance; it is an estimate, not a provider bill.';
        case 'source_unavailable':
            return 'No catalog token estimate: no model catalog source is available.';
        case 'stale_source':
            return 'No catalog token estimate: the model catalog snapshot is stale and stale rates are never applied.';
        case 'missing_model_identity':
            return 'No catalog token estimate: the observed calls carry no defensible model identity.';
        case 'model_not_found':
            return 'No catalog token estimate: the observed model has no entry in the active catalog snapshot.';
        case 'ambiguous_model':
            return 'No catalog token estimate: the observed model matches multiple catalog entries and Qyl does not guess.';
        case 'incomplete_usage':
            return 'No catalog token estimate: observed token usage is incomplete and missing usage is never treated as zero.';
        case 'conditional_pricing_unresolvable':
            return 'No catalog token estimate: conditional catalog pricing cannot be resolved from the observed usage.';
        case 'unsupported_pricing':
            return 'No catalog token estimate: the catalog prices this model in a mode token estimation does not support.';
    }
}

function statusClass(status: ProviderBillingSource['status'] | ModelCatalogSource['status']): string {
    switch (status) {
        case 'current':
            return 'border-signal-green/50 bg-signal-green/10 text-signal-green';
        case 'pending':
            return 'border-signal-cyan/50 bg-signal-cyan/10 text-signal-cyan';
        case 'stale':
            return 'border-signal-yellow/50 bg-signal-yellow/10 text-signal-yellow';
        case 'sync_failed':
            return 'border-signal-red/50 bg-signal-red/10 text-signal-red';
        case 'unconfigured':
            return 'border-brutal-zinc bg-brutal-dark text-brutal-slate';
    }
}

function gateClass(state: GenAiEtlPromotionGateState | undefined): string {
    switch (state) {
        case 'passed':
            return 'border-signal-green/50 bg-signal-green/10 text-signal-green';
        case 'failed':
            return 'border-signal-red/50 bg-signal-red/10 text-signal-red';
        case 'blocked_missing_evidence':
            return 'border-signal-yellow/50 bg-signal-yellow/10 text-signal-yellow';
        case 'not_evaluated':
        case undefined:
            return 'border-brutal-zinc bg-brutal-dark text-brutal-slate';
    }
}

function SummaryTile({label, value, note, accent}: {
    label: string;
    value: string;
    note?: string;
    accent?: boolean;
}) {
    return (
        <Card className="border border-brutal-zinc/70 bg-brutal-carbon/92">
            <CardContent className="p-4">
                <div className="text-[11px] font-semibold uppercase tracking-[0.2em] text-brutal-slate">
                    {label}
                </div>
                <div className={cn(
                    'mt-2 font-mono text-2xl tracking-[-0.02em]',
                    accent ? 'text-signal-orange' : 'text-brutal-white',
                )}>
                    {value}
                </div>
                {note && <p className="mt-1 text-[11px] leading-4 text-brutal-slate">{note}</p>}
            </CardContent>
        </Card>
    );
}

function BillingSourceCard({source}: { source: ProviderBillingSource }) {
    const period = source.period_start && source.period_end
        ? `${formatUtcDay(source.period_start)} – ${formatUtcDay(source.period_end)}`
        : 'No reported period';

    return (
        <Card className="border border-brutal-zinc/70 bg-brutal-carbon/92">
            <CardContent className="p-4 space-y-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                        <div className="flex items-center gap-2">
                            <DatabaseZap className="h-4 w-4 text-signal-violet"/>
                            <h4 className="font-mono text-sm font-semibold uppercase text-brutal-white">
                                {source.provider}
                            </h4>
                        </div>
                        <p className="mt-1 text-[11px] uppercase tracking-[0.12em] text-brutal-slate">
                            Provider billing
                        </p>
                    </div>
                    <Badge className={cn('border text-[10px] uppercase tracking-[0.12em]', statusClass(source.status))}>
                        {humanize(source.status)}
                    </Badge>
                </div>

                <div className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-3">
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Reported cost</div>
                        <div className="mt-1 font-mono text-brutal-white">{formatUsd(source.reported_cost_usd)}</div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Attribution</div>
                        <div className="mt-1 text-brutal-white">{humanize(source.attribution)}</div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Period</div>
                        <div className="mt-1 text-brutal-white">{period}</div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Last synced</div>
                        <div className="mt-1 text-brutal-white">{formatTimestamp(source.last_success_at)}</div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Currency</div>
                        <div className="mt-1 font-mono text-brutal-white">{source.currency_code ?? 'Unavailable'}</div>
                    </div>
                </div>

                {source.model && (
                    <div className="text-xs text-brutal-slate">
                        Provider-reported model: <span className="font-mono text-brutal-white">{source.model}</span>
                    </div>
                )}
                {source.failure_category && (
                    <div className="border border-signal-red/35 bg-signal-red/5 px-3 py-2 text-xs text-signal-red">
                        Sync failure: {humanize(source.failure_category)}
                    </div>
                )}
                <p className="text-[11px] leading-4 text-brutal-slate">
                    {costAttributionCopy(source.attribution)}
                </p>
                <div className="truncate border-t border-brutal-zinc/50 pt-2 font-mono text-[10px] text-brutal-slate"
                     title={source.source_endpoint}>
                    SOURCE · {source.source_endpoint}
                </div>
            </CardContent>
        </Card>
    );
}

function CatalogSourceCard({source}: { source: ModelCatalogSource }) {
    return (
        <Card className="border border-brutal-zinc/70 bg-brutal-carbon/92">
            <CardContent className="p-4 space-y-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                        <div className="flex items-center gap-2">
                            <DatabaseZap className="h-4 w-4 text-signal-cyan"/>
                            <h4 className="font-mono text-sm font-semibold uppercase text-brutal-white">
                                {source.source_id}
                            </h4>
                        </div>
                        <p className="mt-1 text-[11px] uppercase tracking-[0.12em] text-brutal-slate">
                            OpenRouter model catalog
                        </p>
                    </div>
                    <Badge className={cn('border text-[10px] uppercase tracking-[0.12em]', statusClass(source.status))}>
                        {humanize(source.status)}
                    </Badge>
                </div>

                <div className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-3">
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Price semantics</div>
                        <div className="mt-1 text-brutal-white">
                            {source.price_semantics ? humanize(source.price_semantics) : 'Unavailable'}
                        </div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Models priced</div>
                        <div className="mt-1 font-mono text-brutal-white">
                            {source.model_count === undefined ? 'Unavailable' : integer.format(source.model_count)}
                        </div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Snapshot</div>
                        <div className="mt-1 truncate font-mono text-brutal-white" title={source.active_snapshot_id}>
                            {source.active_snapshot_id ?? 'None'}
                        </div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Retrieved</div>
                        <div className="mt-1 text-brutal-white">{formatTimestamp(source.retrieved_at)}</div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Last verified</div>
                        <div className="mt-1 text-brutal-white">{formatTimestamp(source.last_verified_at)}</div>
                    </div>
                    <div>
                        <div className="text-[10px] uppercase tracking-[0.14em] text-brutal-slate">Last attempt</div>
                        <div className="mt-1 text-brutal-white">{formatTimestamp(source.last_attempt_at)}</div>
                    </div>
                </div>

                {source.failure_category && (
                    <div className="border border-signal-red/35 bg-signal-red/5 px-3 py-2 text-xs text-signal-red">
                        Sync failure: {humanize(source.failure_category)}
                    </div>
                )}
                <div className="truncate border-t border-brutal-zinc/50 pt-2 font-mono text-[10px] text-brutal-slate"
                     title={source.source_endpoint}>
                    SOURCE · {source.source_endpoint}
                </div>
            </CardContent>
        </Card>
    );
}

function GateIcon({state}: { state: GenAiEtlPromotionGateState | undefined }) {
    switch (state) {
        case 'passed':
            return <CheckCircle2 className="h-3.5 w-3.5"/>;
        case 'failed':
            return <XCircle className="h-3.5 w-3.5"/>;
        case 'blocked_missing_evidence':
            return <AlertTriangle className="h-3.5 w-3.5"/>;
        case 'not_evaluated':
        case undefined:
            return <CircleDashed className="h-3.5 w-3.5"/>;
    }
}

function WorkflowClusterCard({cluster, rank, selected, onSelect}: {
    cluster: GenAiEtlAuditCluster;
    rank: number;
    selected: boolean;
    onSelect: () => void;
}) {
    const gateByKind = new Map(cluster.promotion_gates.map(gate => [gate.gate, gate]));
    const passedGates = cluster.promotion_gates.filter(gate => gate.state === 'passed').length;
    const estimate = cluster.catalog_token_estimate.status === 'calculated'
        ? cluster.catalog_token_estimate
        : undefined;

    return (
        <article className={cn(
            'border bg-brutal-carbon/92 transition-colors',
            selected ? 'border-signal-orange' : 'border-brutal-zinc/70',
        )}>
            <div className="p-4 space-y-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                            <span className="font-mono text-xs text-signal-orange">#{rank}</span>
                            <h4 className="break-all font-mono text-sm font-semibold text-brutal-white">
                                {cluster.workflow_key}
                            </h4>
                            <Badge className="border border-signal-violet/40 bg-signal-violet/10 text-[10px] uppercase tracking-[0.1em] text-signal-violet">
                                {humanize(cluster.task_family)}
                            </Badge>
                        </div>
                        <p className="mt-1 text-xs text-brutal-slate">
                            {cluster.service_name}
                            {cluster.span_name ? ` · ${cluster.span_name}` : ''}
                            {cluster.operation_name ? ` · ${humanize(cluster.operation_name)}` : ''}
                        </p>
                        {(cluster.provider || cluster.model) && (
                            <p className="mt-1 font-mono text-[11px] text-brutal-slate">
                                {[cluster.provider, cluster.model].filter(Boolean).join(' / ')}
                            </p>
                        )}
                    </div>
                    <Button type="button" size="sm" variant={selected ? 'default' : 'outline'}
                            aria-pressed={selected} onClick={onSelect}>
                        <Calculator className="h-3.5 w-3.5"/>
                        {selected ? 'Selected for scenario' : 'Calculate scenario'}
                    </Button>
                </div>

                <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
                    <ClusterDatum label="Calls" value={integer.format(cluster.call_count)}/>
                    <ClusterDatum label="Catalog token cost"
                                  value={formatUsd(estimate?.estimated_catalog_token_cost_usd)}
                                  unavailable={estimate === undefined}/>
                    <ClusterDatum label="Est. $ / call"
                                  value={formatUsd(estimate?.estimated_catalog_token_cost_per_call_usd)}
                                  unavailable={estimate === undefined}/>
                    <ClusterDatum label="Input tokens" value={integer.format(cluster.input_tokens)}/>
                    <ClusterDatum label="Output tokens" value={integer.format(cluster.output_tokens)}/>
                    <ClusterDatum label="P95 latency" value={`${decimal.format(cluster.p95_latency_ms)} ms`}/>
                </div>

                <div className="grid gap-3 border-y border-brutal-zinc/50 py-3 sm:grid-cols-2 lg:grid-cols-4">
                    <ClusterDatum label="Output contract" value={humanize(cluster.output_contract)}/>
                    <ClusterDatum label="Candidate" value={humanize(cluster.candidate_path)}/>
                    <ClusterDatum label="Validation metrics"
                                  value={cluster.validation_metrics.map(humanize).join(', ') || 'Unavailable'}
                                  unavailable={cluster.validation_metrics.length === 0}/>
                    <ClusterDatum label="Residual path" value={humanize(cluster.residual_path)}/>
                </div>

                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                    <ClusterDatum label="Measurable coverage" value={formatRatio(cluster.measurable_coverage)}
                                  unavailable={cluster.measurable_coverage === undefined}/>
                    <ClusterDatum label="Safe deferral" value={formatRatio(cluster.safe_deferral_coverage)}
                                  unavailable={cluster.safe_deferral_coverage === undefined}/>
                    <ClusterDatum label="Error rate" value={formatRatio(cluster.error_rate)}/>
                    <ClusterDatum label="Average latency" value={`${decimal.format(cluster.average_latency_ms)} ms`}/>
                </div>

                <div className="grid gap-4 lg:grid-cols-2">
                    <div>
                        <h5 className="text-[10px] font-semibold uppercase tracking-[0.16em] text-brutal-slate">
                            Evidence present
                        </h5>
                        <div className="mt-2 flex flex-wrap gap-1.5">
                            {cluster.evidence_signals.length === 0 ? (
                                <span className="text-xs text-brutal-slate">No evidence signals reported</span>
                            ) : cluster.evidence_signals.map(signal => (
                                <Badge key={signal}
                                       className="border border-signal-green/35 bg-signal-green/5 text-[10px] text-signal-green">
                                    {humanize(signal)}
                                </Badge>
                            ))}
                        </div>
                    </div>
                    <div>
                        <h5 className="text-[10px] font-semibold uppercase tracking-[0.16em] text-brutal-slate">
                            Evidence gaps
                        </h5>
                        <div className="mt-2 flex flex-wrap gap-1.5">
                            {cluster.missing_evidence.length === 0 ? (
                                <span className="text-xs text-signal-green">No missing evidence reported</span>
                            ) : cluster.missing_evidence.map(signal => (
                                <Badge key={signal}
                                       className="border border-signal-yellow/35 bg-signal-yellow/5 text-[10px] text-signal-yellow">
                                    {humanize(signal)}
                                </Badge>
                            ))}
                        </div>
                    </div>
                </div>

                <div>
                    <div className="flex items-center justify-between gap-3">
                        <h5 className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-[0.16em] text-brutal-slate">
                            <ShieldCheck className="h-3.5 w-3.5"/> Promotion gates
                        </h5>
                        <span className="font-mono text-[10px] text-brutal-slate">{passedGates}/6 passed</span>
                    </div>
                    <div className="mt-2 grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                        {gateOrder.map(kind => {
                            const gate = gateByKind.get(kind);
                            return (
                                <div key={kind} className={cn('border px-2.5 py-2', gateClass(gate?.state))}>
                                    <div className="flex items-center gap-1.5 text-[11px] font-semibold">
                                        <GateIcon state={gate?.state}/>
                                        {gateLabels[kind]}
                                    </div>
                                    <p className="mt-1 text-[10px] leading-4 opacity-80">
                                        {gate ? humanize(gate.state) : 'not reported'}
                                        {gate?.reason ? ` · ${gate.reason}` : ''}
                                    </p>
                                </div>
                            );
                        })}
                    </div>
                </div>

                <div className="border border-brutal-zinc/60 bg-brutal-dark/45 px-3 py-2 text-[11px] leading-4 text-brutal-slate">
                    {catalogEstimateCopy(cluster.catalog_token_estimate)}
                </div>
            </div>
        </article>
    );
}

function ClusterDatum({label, value, unavailable}: { label: string; value: string; unavailable?: boolean }) {
    return (
        <div>
            <div className="text-[10px] uppercase tracking-[0.13em] text-brutal-slate">{label}</div>
            <div className={cn('mt-1 text-xs', unavailable ? 'text-signal-yellow' : 'font-mono text-brutal-white')}>
                {value}
            </div>
        </div>
    );
}

interface ScenarioValues {
    coverage: string;
    frontier: string;
    alternative: string;
    maintenance: string;
    error: string;
}

export function scenarioAssumptionKey(
    clusterId: string,
    callCount: number,
    periodStart: string,
    periodEnd: string,
    projectScope: string | undefined,
    values: ScenarioValues,
): string {
    return JSON.stringify([
        clusterId,
        callCount,
        periodStart,
        periodEnd,
        normalizeProjectScope(projectScope) ?? null,
        values.coverage,
        values.frontier,
        values.alternative,
        values.maintenance,
        values.error,
    ]);
}

export function buildScenarioRequest(
    clusterId: string,
    values: ScenarioValues,
): { request?: GenAiEtlAuditEvaluationRequest; error?: string } {
    const required = [
        ['Coverage', values.coverage],
        ['Alternative cost per call', values.alternative],
        ['Maintenance cost', values.maintenance],
        ['Error cost', values.error],
    ] as const;

    for (const [label, raw] of required) {
        if (raw.trim() === '') return {error: `${label} is required.`};
    }

    const coverage = Number(values.coverage);
    // Frontier is optional: a blank value lets the backend fall back to the catalog token estimate.
    const frontier = values.frontier.trim() === '' ? undefined : Number(values.frontier);
    const alternative = Number(values.alternative);
    const maintenance = Number(values.maintenance);
    const errorCost = Number(values.error);

    if (!Number.isFinite(coverage) || coverage < 0 || coverage > 1) {
        return {error: 'Coverage must be a number from 0 to 1.'};
    }
    const costs = [
        ['Frontier cost per call', frontier],
        ['Alternative cost per call', alternative],
        ['Maintenance cost', maintenance],
        ['Error cost', errorCost],
    ] as const;
    for (const [label, value] of costs) {
        if (value === undefined) continue;
        if (!Number.isFinite(value) || value < 0) {
            return {error: `${label} must be a non-negative number.`};
        }
    }

    const scenario: GenAiEtlClusterScenario = {
        cluster_id: clusterId,
        coverage,
        alternative_cost_per_call_usd: alternative,
        period_maintenance_cost_usd: maintenance,
        period_error_cost_usd: errorCost,
    };
    if (frontier !== undefined) scenario.frontier_cost_per_call_usd = frontier;

    return {request: {scenarios: [scenario]}};
}

function ScenarioCalculator({cluster, periodStart, periodEnd, projectScope}: {
    cluster: GenAiEtlAuditCluster;
    periodStart: string;
    periodEnd: string;
    projectScope?: string;
}) {
    const [coverage, setCoverage] = useState('');
    const [frontier, setFrontier] = useState('');
    const [alternative, setAlternative] = useState('');
    const [maintenance, setMaintenance] = useState('');
    const [errorCost, setErrorCost] = useState('');
    const [validationError, setValidationError] = useState<string>();
    const [submittedAssumptionKey, setSubmittedAssumptionKey] = useState<string>();
    const evaluation = useEvaluateGenAiEtlAudit();

    const values: ScenarioValues = {coverage, frontier, alternative, maintenance, error: errorCost};
    const currentAssumptionKey = scenarioAssumptionKey(
        cluster.cluster_id,
        cluster.call_count,
        periodStart,
        periodEnd,
        projectScope,
        values,
    );

    function changeAssumption(setter: (value: string) => void, value: string) {
        evaluation.reset();
        setValidationError(undefined);
        setSubmittedAssumptionKey(undefined);
        setter(value);
    }

    function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const built = buildScenarioRequest(cluster.cluster_id, values);
        if (!built.request) {
            setValidationError(built.error);
            return;
        }
        setValidationError(undefined);
        setSubmittedAssumptionKey(currentAssumptionKey);
        evaluation.reset();
        evaluation.mutate({request: built.request, startTime: periodStart, endTime: periodEnd, projectScope});
    }

    const result = evaluation.isPending || submittedAssumptionKey !== currentAssumptionKey
        ? undefined
        : evaluation.data?.results.find(item => item.cluster_id === cluster.cluster_id);

    return (
        <Card className="border border-signal-orange/50 bg-brutal-carbon/95">
            <CardContent className="p-4 lg:p-5">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                        <h3 className="flex items-center gap-2 text-sm font-semibold tracking-[0.12em] text-brutal-white">
                            <Calculator className="h-4 w-4 text-signal-orange"/> REPLACEMENT SCENARIO
                        </h3>
                        <p className="mt-1 text-xs text-brutal-slate">
                            {cluster.workflow_key} · {integer.format(cluster.call_count)} observed calls
                        </p>
                    </div>
                    <Badge className="border border-brutal-zinc bg-brutal-dark text-[10px] uppercase tracking-[0.12em] text-brutal-slate">
                        Scenario or catalog frontier
                    </Badge>
                </div>

                <p id="scenario-help" className="mt-3 max-w-4xl text-xs leading-5 text-brutal-slate">
                    Net replaceable value = calls × coverage × (frontier cost − alternative cost) − maintenance − error cost.
                    Provider billing totals remain source-level and are never converted into this per-call baseline. Leave
                    frontier blank to fall back to this cluster's catalog token estimate.
                </p>

                <form className="mt-4 space-y-4" onSubmit={submit} noValidate>
                    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
                        <ScenarioField id="scenario-coverage" label="Coverage (0–1)" value={coverage}
                                       onChange={value => changeAssumption(setCoverage, value)} min="0" max="1" step="0.01" required/>
                        <ScenarioField id="scenario-frontier" label="Frontier $ / call" value={frontier}
                                       onChange={value => changeAssumption(setFrontier, value)} min="0" step="any"
                                       placeholder="Blank uses catalog estimate"/>
                        <ScenarioField id="scenario-alternative" label="Alternative $ / call" value={alternative}
                                       onChange={value => changeAssumption(setAlternative, value)} min="0" step="any" required/>
                        <ScenarioField id="scenario-maintenance" label="Period maintenance $" value={maintenance}
                                       onChange={value => changeAssumption(setMaintenance, value)} min="0" step="any" required/>
                        <ScenarioField id="scenario-error" label="Period error $" value={errorCost}
                                       onChange={value => changeAssumption(setErrorCost, value)} min="0" step="any" required/>
                    </div>

                    {(validationError || evaluation.isError) && (
                        <div role="alert" className="border border-signal-red/45 bg-signal-red/5 px-3 py-2 text-xs text-signal-red">
                            {validationError ?? (evaluation.error instanceof Error
                                ? evaluation.error.message
                                : 'The scenario could not be calculated.')}
                        </div>
                    )}

                    <div className="flex flex-wrap items-center gap-3">
                        <Button type="submit" disabled={evaluation.isPending}>
                            <FlaskConical className="h-4 w-4"/>
                            {evaluation.isPending ? 'Calculating…' : 'Calculate scenario'}
                        </Button>
                        <span className="text-[11px] text-brutal-slate">
                            A blank frontier uses the catalog token estimate; provider billing is never allocated per call.
                        </span>
                    </div>
                </form>

                {result && <EvaluationResultPanel result={result}/>} 
            </CardContent>
        </Card>
    );
}

function ScenarioField({id, label, value, onChange, ...input}: {
    id: string;
    label: string;
    value: string;
    onChange: (value: string) => void;
    min?: string;
    max?: string;
    step?: string;
    placeholder?: string;
    required?: boolean;
}) {
    return (
        <div>
            <label htmlFor={id} className="text-[10px] font-semibold uppercase tracking-[0.13em] text-brutal-slate">
                {label}
            </label>
            <Input id={id} type="number" inputMode="decimal" value={value}
                   onChange={event => onChange(event.target.value)} aria-describedby="scenario-help"
                   className="mt-1 font-mono" {...input}/>
        </div>
    );
}

export function EvaluationResultPanel({result}: { result: GenAiEtlClusterEvaluation }) {
    if (result.status === 'missing_frontier_cost') {
        return (
            <div role="status" aria-live="polite"
                 className="mt-5 border border-signal-yellow/50 bg-signal-yellow/5 p-4 text-sm text-signal-yellow">
                <div className="flex items-center gap-2 font-semibold">
                    <AlertTriangle className="h-4 w-4"/> Missing frontier cost
                </div>
                <p className="mt-2 text-xs leading-5">
                    No scenario frontier was supplied and this cluster has no catalog token estimate to fall back to.
                    Enter an explicit frontier cost per call to calculate the scenario; missing cost is not treated as zero.
                </p>
            </div>
        );
    }

    return (
        <output aria-live="polite" className="mt-5 block border-t border-brutal-zinc/60 pt-4">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <ResultDatum label="Gross replaceable value" value={formatUsd(result.gross_replaceable_value_usd)}
                             signed={result.gross_replaceable_value_usd}/>
                <ResultDatum label="Net replaceable value" value={formatUsd(result.net_replaceable_value_usd)}
                             signed={result.net_replaceable_value_usd}/>
                <ResultDatum label="Served calls" value={decimal.format(result.served_call_count)}/>
                <ResultDatum label="Residual calls" value={decimal.format(result.residual_call_count)}/>
            </div>
            <div className="mt-3 grid gap-3 text-xs sm:grid-cols-2 lg:grid-cols-4">
                <ClusterDatum label="Frontier $ / call" value={formatUsd(result.frontier_cost_per_call_usd)}/>
                <ClusterDatum label="Frontier basis" value={humanize(result.frontier_cost_basis)}/>
                <ClusterDatum label="Alternative $ / call" value={formatUsd(result.alternative_cost_per_call_usd)}/>
                <ClusterDatum label="Current period cost" value={formatUsd(result.current_period_cost_usd)}/>
            </div>
            {result.frontier_cost_basis === 'catalog_token_estimate' && (
                <p className="mt-3 font-mono text-[10px] text-brutal-slate">
                    CATALOG FRONTIER · {result.catalog_provenance.price_model_id} ·
                    snapshot {result.catalog_provenance.snapshot_id}
                </p>
            )}
            <p className="mt-3 text-[11px] leading-4 text-brutal-slate">
                Negative values are retained. Negative gross means the covered alternative spend exceeds the covered frontier
                spend; negative net means gross savings do not cover the supplied maintenance and error costs.
            </p>
        </output>
    );
}

function ResultDatum({label, value, signed}: { label: string; value: string; signed?: number }) {
    return (
        <div className="border border-brutal-zinc/60 bg-brutal-dark/45 p-3">
            <div className="text-[10px] uppercase tracking-[0.13em] text-brutal-slate">{label}</div>
            <div className={cn(
                'mt-1 font-mono text-lg',
                signed === undefined || signed === 0
                    ? 'text-brutal-white'
                    : signed > 0 ? 'text-signal-green' : 'text-signal-red',
            )}>
                {value}
            </div>
        </div>
    );
}

function ProjectScopeControl({value, activeScope, onChange, onApply}: {
    value: string;
    activeScope?: string;
    onChange: (value: string) => void;
    onApply: () => void;
}) {
    const requestedScope = normalizeProjectScope(value);
    const unchanged = requestedScope === activeScope;

    return (
        <form className="border border-brutal-zinc/70 bg-brutal-carbon/92 p-3" onSubmit={event => {
            event.preventDefault();
            onApply();
        }}>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                <div className="min-w-0 flex-1">
                    <label htmlFor="cost-project-scope"
                           className="text-[10px] font-semibold uppercase tracking-[0.14em] text-brutal-slate">
                        Qyl project scope (optional)
                    </label>
                    <Input id="cost-project-scope" type="text" value={value} maxLength={128}
                           autoComplete="off" placeholder="Omit X-Qyl-Project"
                           onChange={event => onChange(event.target.value)} className="mt-1 font-mono"/>
                </div>
                <Button type="submit" variant="outline" disabled={unchanged}>Apply project</Button>
            </div>
            <p className="mt-2 text-[11px] leading-4 text-brutal-slate">
                Empty omits the project header. Enter the same explicit scope used by <code>QYL_COST_PROJECT_ID</code> when applicable.
                {activeScope ? ` Active scope: ${activeScope}.` : ' No explicit project header is active.'}
            </p>
        </form>
    );
}

export function EtlAuditView() {
    const [projectInput, setProjectInput] = useState('');
    const [projectScope, setProjectScope] = useState<string>();
    const audit = useGenAiEtlAudit(25, projectScope);
    const [selectedClusterId, setSelectedClusterId] = useState<string>();

    useEffect(() => {
        const clusters = audit.data?.clusters;
        if (!clusters) return;
        if (!clusters.some(cluster => cluster.cluster_id === selectedClusterId))
            setSelectedClusterId(clusters[0]?.cluster_id);
    }, [audit.data, selectedClusterId]);

    const selectedCluster = useMemo(
        () => audit.data?.clusters.find(cluster => cluster.cluster_id === selectedClusterId),
        [audit.data, selectedClusterId],
    );

    const scopeControl = (
        <ProjectScopeControl value={projectInput} activeScope={projectScope}
                             onChange={setProjectInput}
                             onApply={() => {
                                 setSelectedClusterId(undefined);
                                 setProjectScope(normalizeProjectScope(projectInput));
                             }}/>
    );

    if (audit.isLoading) {
        return (
            <div className="space-y-4">
                {scopeControl}
                <div className="border border-brutal-zinc/70 bg-brutal-carbon/92 p-8 text-sm text-brutal-slate">
                    Loading ETL audit…
                </div>
            </div>
        );
    }

    if (audit.isError || !audit.data) {
        return (
            <div className="space-y-4">
                {scopeControl}
                <div role="alert" className="border border-signal-red/45 bg-signal-red/5 p-5 text-sm text-signal-red">
                    ETL audit unavailable: {audit.error instanceof Error ? audit.error.message : 'the report could not be loaded'}
                </div>
            </div>
        );
    }

    const {summary} = audit.data;

    return (
        <div className="space-y-4">
            {scopeControl}
            <div className="border border-signal-orange/40 bg-signal-orange/5 px-4 py-3">
                <div className="flex items-start gap-2">
                    <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-signal-orange"/>
                    <div className="text-xs leading-5 text-brutal-slate">
                        <p className="font-semibold text-brutal-white">Source-level billing, catalog-priced estimates.</p>
                        <p>
                            Provider billing feeds report aggregate or provider/model period totals—not exact per-trace
                            prices—and stay source-level because observed traces cannot prove complete provider usage.
                            Workflow figures are catalog token estimates priced from live catalog snapshots with full
                            provenance; missing cost is never treated as zero.
                        </p>
                    </div>
                </div>
            </div>

            <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
                <SummaryTile label="Observed calls" value={integer.format(summary.total_calls)} accent/>
                <SummaryTile label="Est. catalog token spend"
                             value={formatUsd(summary.estimated_catalog_token_cost_usd)}
                             note="Priced from catalog snapshots, not provider billing"/>
                <SummaryTile label="Priced call coverage"
                             value={formatRatio(summary.catalog_token_priced_call_coverage)}
                             note="Calls with a calculated catalog token estimate"/>
                <SummaryTile label="Economic concentration"
                             value={formatRatio(summary.estimated_token_economic_concentration)}
                             note="Estimated-token concentration in ranked workflows"/>
            </div>

            <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
                <SummaryTile label="Input tokens" value={integer.format(summary.total_input_tokens)}/>
                <SummaryTile label="Output tokens" value={integer.format(summary.total_output_tokens)}/>
                <SummaryTile label="Measurable coverage" value={formatRatio(summary.measurable_coverage)}/>
                <SummaryTile label="Safe deferral" value={formatRatio(summary.safe_deferral_coverage)}/>
                <SummaryTile label="Candidate ETL spend share"
                             value={formatRatio(summary.candidate_etl_estimated_token_spend_share)}/>
            </div>

            <section aria-labelledby="cost-source-heading">
                <div className="mb-2 flex items-center gap-2">
                    <DatabaseZap className="h-4 w-4 text-signal-violet"/>
                    <h3 id="cost-source-heading" className="text-xs font-semibold uppercase tracking-[0.16em] text-brutal-white">
                        Cost source provenance
                    </h3>
                </div>
                {audit.data.billing_sources.length === 0 ? (
                    <div className="border border-brutal-zinc/70 bg-brutal-carbon/92 p-4 text-xs text-brutal-slate">
                        No provider billing source state was returned. Provider-reported spend remains unavailable.
                    </div>
                ) : (
                    <div className="grid gap-3 lg:grid-cols-2">
                        {audit.data.billing_sources.map((source, index) => (
                            <BillingSourceCard key={`${source.provider}-${source.model ?? 'all'}-${index}`} source={source}/>
                        ))}
                    </div>
                )}
                <div className="mt-3">
                    {audit.data.catalog_sources.length === 0 ? (
                        <div className="border border-brutal-zinc/70 bg-brutal-carbon/92 p-4 text-xs text-brutal-slate">
                            No model catalog source state was returned. Catalog token estimates remain unavailable.
                        </div>
                    ) : (
                        <div className="grid gap-3 lg:grid-cols-2">
                            {audit.data.catalog_sources.map(source => (
                                <CatalogSourceCard key={source.source_id} source={source}/>
                            ))}
                        </div>
                    )}
                </div>
            </section>

            <section aria-labelledby="workflow-ranking-heading" className="space-y-3">
                <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
                    <div>
                        <div className="flex items-center gap-2">
                            <BarChart3 className="h-4 w-4 text-signal-orange"/>
                            <h3 id="workflow-ranking-heading"
                                className="text-xs font-semibold uppercase tracking-[0.16em] text-brutal-white">
                                Ranked workflow clusters
                            </h3>
                        </div>
                        <p className="mt-1 text-[11px] text-brutal-slate">
                            Task and replacement paths are hypotheses; evidence gaps and all six promotion gates remain visible.
                        </p>
                    </div>
                    <div className="font-mono text-[10px] text-brutal-slate">
                        COMPLETED UTC DAYS · {formatUtcDay(audit.data.period_start)} – {formatUtcDay(audit.data.period_end)}
                    </div>
                </div>

                {audit.data.clusters.length === 0 ? (
                    <div className="border border-brutal-zinc/70 bg-brutal-carbon/92 p-6 text-sm text-brutal-slate">
                        No GenAI workflow clusters were observed in this period.
                    </div>
                ) : audit.data.clusters.map((cluster, index) => (
                    <WorkflowClusterCard key={cluster.cluster_id} cluster={cluster} rank={index + 1}
                                         selected={cluster.cluster_id === selectedClusterId}
                                         onSelect={() => setSelectedClusterId(cluster.cluster_id)}/>
                ))}
            </section>

            {selectedCluster && (
                <ScenarioCalculator
                    key={`${projectScope ?? ''}:${audit.data.period_start}:${audit.data.period_end}:${selectedCluster.cluster_id}:${selectedCluster.call_count}`}
                    cluster={selectedCluster} periodStart={audit.data.period_start} periodEnd={audit.data.period_end}
                    projectScope={projectScope}/>
            )}
        </div>
    );
}
