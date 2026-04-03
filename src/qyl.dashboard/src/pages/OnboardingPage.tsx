import {useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {
    ArrowRight,
    Bot,
    CheckCircle2,
    Copy,
    Database,
    ExternalLink,
    GitBranch,
    Radio,
    Search,
    Server,
    ShieldCheck,
    Sparkles,
    Terminal,
    type LucideIcon,
    Workflow,
} from 'lucide-react';
import {Link} from 'react-router-dom';
import {toast} from 'sonner';
import {Button} from '@/components/ui/button';
import {resolveOnboardingConnection, useCollectorMeta} from '@/lib/onboarding';
import {cn} from '@/lib/utils';

type GitHubStatus = {
    configured: boolean;
    user: {
        login: string;
        name: string;
        avatarUrl: string;
    } | null;
    authMethod: string;
};

type ScopeGroup = {
    title: string;
    description: string;
    level: 'inspect' | 'triage' | 'analyze';
    icon: LucideIcon;
};

type LaunchStep = {
    title: string;
    description: string;
};

const scopeGroups: ScopeGroup[] = [
    {
        title: 'Inspect traces and issues',
        description: 'Search traces, grouped errors, spans, sessions, and live telemetry facts.',
        level: 'inspect',
        icon: Search,
    },
    {
        title: 'Run Loom investigations',
        description: 'Delegate RCA and fix suggestions without mixing analysis into raw evidence.',
        level: 'analyze',
        icon: Sparkles,
    },
    {
        title: 'Control triage state',
        description: 'Annotate and triage incidents after the facts are clear.',
        level: 'triage',
        icon: ShieldCheck,
    },
    {
        title: 'Keep the collector separate',
        description: 'qyl.mcp stays HTTP-only and separate from the collector data plane.',
        level: 'inspect',
        icon: Server,
    },
];

const proofChecks = [
    'OTLP HTTP ingest active',
    'DuckDB query path ready',
    'facts / analysis / actions separated',
];

const launchSteps: LaunchStep[] = [
    {
        title: 'Run qyl',
        description: 'One Docker image exposes dashboard, REST, SSE, and OTLP ingestion.',
    },
    {
        title: 'Aim your SDK',
        description: 'Point any OpenTelemetry exporter at qyl over HTTP or gRPC.',
    },
    {
        title: 'Inspect and automate',
        description: 'Use the operator surface first, then add qyl.mcp and Loom when you want agents involved.',
    },
];

async function copyValue(value: string, label: string) {
    try {
        await navigator.clipboard.writeText(value);
        toast.success(`${label} copied to clipboard`);
    } catch {
        toast.error(`Failed to copy ${label.toLowerCase()}`);
    }
}

function Eyebrow({children}: { children: string }) {
    return (
        <div className="text-[11px] font-semibold uppercase tracking-[0.28em] text-brutal-slate">
            {children}
        </div>
    );
}

function SectionTitle({children}: { children: string }) {
    return (
        <h2 className="max-w-[12ch] text-4xl font-semibold tracking-[-0.04em] text-brutal-white sm:text-5xl">
            {children}
        </h2>
    );
}

function QylWordmark() {
    return (
        <div className="flex items-center gap-4">
            <div className="relative flex h-13 w-13 items-center justify-center rounded-full border border-signal-violet/40 bg-signal-violet/12 shadow-[0_0_40px_rgba(147,51,234,0.18)]">
                <div className="absolute inset-1 rounded-full border border-signal-violet/20"/>
                <Terminal className="h-6 w-6 text-signal-violet"/>
            </div>
            <div>
                <div className="text-[13px] font-semibold uppercase tracking-[0.3em] text-brutal-slate">
                    qyl
                </div>
                <div className="text-xl font-semibold tracking-[-0.03em] text-brutal-white sm:text-2xl">
                    AI observability
                </div>
            </div>
        </div>
    );
}

function StatusRow({
    label,
    value,
    active,
}: {
    label: string;
    value: string;
    active: boolean;
}) {
    return (
        <div className="flex items-start justify-between gap-4 px-4 py-3 sm:px-5">
            <div>
                <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                    {label}
                </div>
                <div className="mt-1 text-sm text-brutal-white sm:text-[15px]">{value}</div>
            </div>
            <div
                className={cn(
                    'mt-1 inline-flex items-center gap-2 rounded-full border px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.18em]',
                    active
                        ? 'border-signal-violet/40 bg-signal-violet/12 text-signal-violet'
                        : 'border-white/10 bg-white/5 text-brutal-slate'
                )}
            >
                <Radio className={cn('h-3 w-3', active && 'animate-pulse-live')}/>
                {active ? 'Live' : 'Pending'}
            </div>
        </div>
    );
}

function ScopeRow({scope}: { scope: ScopeGroup }) {
    const Icon = scope.icon;

    return (
        <div className="grid gap-3 px-5 py-5 sm:grid-cols-[auto_1fr_auto] sm:items-start">
            <div className="flex h-11 w-11 items-center justify-center rounded-full border border-signal-violet/30 bg-signal-violet/10 text-signal-violet">
                <Icon className="h-5 w-5"/>
            </div>
            <div>
                <div className="text-lg font-medium tracking-[-0.02em] text-brutal-white">
                    {scope.title}
                </div>
                <p className="mt-2 max-w-[48ch] text-sm leading-6 text-brutal-slate">
                    {scope.description}
                </p>
            </div>
            <div className="justify-self-start sm:justify-self-end">
                <span className="inline-flex rounded-full border border-white/10 bg-white/5 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.22em] text-brutal-slate">
                    {scope.level}
                </span>
            </div>
        </div>
    );
}

function HeroScene({
    mcpSurface,
    transportLabel,
    githubLabel,
}: {
    mcpSurface: string;
    transportLabel: string;
    githubLabel: string;
}) {
    return (
        <div
            className="relative h-[360px] sm:h-[480px] lg:h-[620px]"
            style={{animation: 'data-stream 720ms ease-out 120ms both'}}
        >
            <div className="absolute inset-0 rounded-[38px] bg-[radial-gradient(circle_at_32%_22%,rgba(126,34,206,0.18),transparent_30%),radial-gradient(circle_at_84%_24%,rgba(245,158,11,0.12),transparent_22%),radial-gradient(circle_at_58%_84%,rgba(34,211,238,0.12),transparent_26%)]"/>
            <div className="absolute inset-x-[14%] inset-y-[10%] rounded-[999px] border border-signal-violet/12"/>
            <div className="absolute inset-x-[8%] inset-y-[4%] rounded-[999px] border border-signal-violet/8"/>
            <div
                className="absolute right-[10%] top-[8%] h-38 w-38 rounded-full border border-signal-violet/20 blur-[1px]"
                style={{animation: 'pulse-live 6s ease-in-out infinite'}}
            />
            <div className="absolute left-[6%] top-[26%] w-[58%] rounded-[28px] border border-white/10 bg-[#090d17]/88 p-5 shadow-[0_24px_80px_rgba(0,0,0,0.42)] backdrop-blur-xl scan-lines sm:p-6">
                <div className="flex items-center justify-between gap-4">
                    <div>
                        <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                            qyl.collector
                        </div>
                        <div className="mt-1 text-sm font-medium text-brutal-white sm:text-base">
                            Live ingest and query path
                        </div>
                    </div>
                    <div className="rounded-full border border-signal-cyan/30 bg-signal-cyan/10 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-signal-cyan">
                        Side channel
                    </div>
                </div>
                <div className="mt-5 space-y-3 font-mono text-[12px] text-brutal-slate sm:text-[13px]">
                    <div className="flex items-center gap-3">
                        <span className="text-signal-violet">$</span>
                        <span className="text-brutal-white">POST /v1/traces</span>
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="text-signal-violet">$</span>
                        <span>{transportLabel}</span>
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="text-signal-violet">$</span>
                        <span>DuckDB store keeps spans, logs, metrics, issues, and cost</span>
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="text-signal-violet">$</span>
                        <span>SSE surfaces stream live facts without changing app control flow</span>
                    </div>
                </div>
                <div className="mt-5 flex gap-2">
                    {Array.from({length: 6}).map((_, index) => (
                        <div
                            key={index}
                            className={cn(
                                'h-1.5 flex-1 rounded-full',
                                index < 5 ? 'bg-signal-violet/80' : 'bg-white/12'
                            )}
                        />
                    ))}
                </div>
                <div className="mt-3 text-[11px] font-semibold uppercase tracking-[0.18em] text-brutal-slate">
                    Validation surface
                </div>
            </div>

            <div
                className="absolute right-0 top-0 w-[62%] rounded-[32px] border border-white/10 bg-[#111127]/90 p-5 shadow-[0_30px_100px_rgba(0,0,0,0.5)] backdrop-blur-xl sm:p-7"
                style={{animation: 'data-stream 780ms ease-out 260ms both'}}
            >
                <div className="text-center">
                    <div className="mx-auto mb-4 flex h-11 w-11 items-center justify-center rounded-full border border-signal-violet/30 bg-signal-violet/12 text-signal-violet">
                        <Workflow className="h-5 w-5"/>
                    </div>
                    <div className="text-2xl font-semibold tracking-[-0.03em] text-brutal-white">
                        Your agent is requesting access to qyl
                    </div>
                </div>

                <div className="mt-6 space-y-3">
                    {scopeGroups.slice(0, 3).map((scope) => {
                        const Icon = scope.icon;
                        return (
                            <div
                                key={scope.title}
                                className="flex items-start gap-3 rounded-[22px] border border-white/8 bg-white/4 px-4 py-3"
                            >
                                <div className="mt-0.5 text-signal-violet">
                                    <CheckCircle2 className="h-4 w-4"/>
                                </div>
                                <div className="min-w-0 flex-1">
                                    <div className="flex items-center gap-2 text-sm font-medium text-brutal-white">
                                        <Icon className="h-4 w-4 text-signal-violet"/>
                                        {scope.title}
                                    </div>
                                    <div className="mt-1 text-sm leading-6 text-brutal-slate">
                                        {scope.description}
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>

                <div className="mt-6 rounded-[24px] border border-amber-300/45 bg-amber-200/6 p-4">
                    <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-amber-100/80">
                        Destination
                    </div>
                    <code className="mt-3 block overflow-x-auto rounded-[14px] bg-black/25 px-3 py-2 font-mono text-sm text-amber-50">
                        {mcpSurface}
                    </code>
                    <p className="mt-3 text-sm leading-6 text-amber-50/80">
                        qyl.mcp stays separate from qyl.collector. Facts flow through the collector; agent access is scoped through the MCP surface.
                    </p>
                </div>

                <div className="mt-6 flex items-center justify-between gap-4">
                    <div className="text-sm text-brutal-slate">{githubLabel}</div>
                    <div className="flex gap-3">
                        <button
                            type="button"
                            className="rounded-full border border-white/10 px-4 py-2 text-sm text-brutal-slate transition-colors hover:border-white/20 hover:text-brutal-white"
                        >
                            Cancel
                        </button>
                        <button
                            type="button"
                            className="rounded-full bg-signal-violet px-5 py-2 text-sm font-semibold text-brutal-white transition-colors hover:bg-signal-violet/90"
                        >
                            Approve
                        </button>
                    </div>
                </div>
            </div>

            <div
                className="absolute bottom-0 right-[6%] w-[34%] rounded-[24px] border border-white/10 bg-[#0b1222]/88 p-4 shadow-[0_20px_60px_rgba(0,0,0,0.38)] backdrop-blur-xl"
                style={{animation: 'data-stream 780ms ease-out 420ms both'}}
            >
                <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                    Proof
                </div>
                <div className="mt-4 space-y-3">
                    {proofChecks.map((check) => (
                        <div key={check} className="flex items-start gap-3 text-sm leading-6 text-brutal-white">
                            <div className="mt-1 h-2.5 w-2.5 rounded-full bg-signal-cyan animate-pulse-live"/>
                            <span>{check}</span>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}

export function OnboardingPage() {
    const {data: meta, isSuccess: hasCollectorMeta} = useCollectorMeta();
    const [protocol, setProtocol] = useState<'http' | 'grpc'>('http');
    const connection = resolveOnboardingConnection(meta, window.location);

    const {data: githubStatus} = useQuery({
        queryKey: ['github-status'],
        queryFn: async () => {
            const response = await fetch('/api/v1/github/status');
            if (!response.ok) {
                return {configured: false, user: null, authMethod: 'none'} satisfies GitHubStatus;
            }

            return response.json() as Promise<GitHubStatus>;
        },
        staleTime: 1000 * 60 * 5,
    });

    const selectedProtocol = protocol === 'grpc' && connection.grpcEnabled
        ? {
            label: 'OTLP / gRPC',
            endpoint: connection.grpcEndpoint ?? `http://localhost:${connection.grpcPort}`,
            note: 'Low-friction agent and service export when you want the standard OTLP gRPC channel.',
            env: `OTEL_EXPORTER_OTLP_PROTOCOL=grpc\nOTEL_EXPORTER_OTLP_ENDPOINT=${connection.grpcEndpoint ?? `http://localhost:${connection.grpcPort}`}`,
        }
        : {
            label: 'OTLP / HTTP',
            endpoint: connection.otlpHttpTraceUrl,
            note: 'The simplest first-run path. Point your HTTP exporter directly at qyl and start sending traces.',
            env: `OTEL_EXPORTER_OTLP_ENDPOINT=${connection.otlpHttpEndpoint}\nOTEL_EXPORTER_OTLP_TRACES_ENDPOINT=${connection.otlpHttpTraceUrl}`,
        };

    const collectorStatus = hasCollectorMeta
        ? `${window.location.origin} serving dashboard + OTLP`
        : 'Using default ports until collector metadata responds';
    const githubSummary = githubStatus?.configured
        ? `Linked as ${githubStatus.user?.login ?? 'GitHub account'}`
        : 'GitHub automation not linked yet';
    const transportSummary = connection.grpcEnabled
        ? `HTTP ${connection.otlpHttpPort} and gRPC ${connection.grpcPort}`
        : `HTTP ${connection.otlpHttpPort} enabled`;

    return (
        <div className="min-h-screen bg-brutal-black text-brutal-white">
            <div className="relative isolate overflow-hidden bg-grid-overlay">
                <div
                    className="pointer-events-none absolute inset-0"
                    style={{
                        backgroundImage: [
                            'radial-gradient(circle at 16% 10%, rgba(139, 92, 246, 0.18), transparent 30%)',
                            'radial-gradient(circle at 84% 18%, rgba(245, 158, 11, 0.14), transparent 22%)',
                            'radial-gradient(circle at 78% 74%, rgba(34, 211, 238, 0.10), transparent 24%)',
                            'linear-gradient(180deg, rgba(5, 7, 16, 0.28) 0%, rgba(3, 5, 12, 0.92) 58%, rgba(2, 4, 10, 1) 100%)',
                        ].join(','),
                    }}
                />

                <header className="fixed inset-x-0 top-0 z-20">
                    <div className="border-b border-white/6 bg-brutal-black/72 backdrop-blur-xl">
                        <div className="mx-auto flex max-w-[1480px] items-center justify-between gap-4 px-6 py-4 lg:px-10 xl:px-16">
                            <div className="flex items-center gap-3">
                                <div className="flex h-9 w-9 items-center justify-center rounded-full border border-signal-violet/35 bg-signal-violet/12 text-signal-violet">
                                    <Terminal className="h-4 w-4"/>
                                </div>
                                <div>
                                    <div className="text-sm font-semibold tracking-[-0.02em] text-brutal-white">
                                        qyl
                                    </div>
                                    <div className="text-[11px] uppercase tracking-[0.24em] text-brutal-slate">
                                        observability
                                    </div>
                                </div>
                            </div>

                            <nav className="hidden items-center gap-7 text-sm text-brutal-slate lg:flex">
                                <a href="#surface" className="transition-colors hover:text-brutal-white">Surface</a>
                                <a href="#mcp" className="transition-colors hover:text-brutal-white">MCP</a>
                                <a href="#otlp" className="transition-colors hover:text-brutal-white">OTLP</a>
                                <a href="#launch" className="transition-colors hover:text-brutal-white">Launch</a>
                            </nav>

                            <div className="flex items-center gap-3">
                                <Button
                                    variant="outline"
                                    className="h-10 rounded-full border-white/10 bg-white/0 px-4 text-brutal-white hover:bg-white/6"
                                    render={<Link to="/settings"/>}
                                >
                                    Settings
                                </Button>
                                <Button
                                    variant="outline"
                                    className="h-10 rounded-full border-white/10 bg-white/0 px-4 text-brutal-white hover:bg-white/6"
                                    render={
                                        <a
                                            href="https://github.com/ANcpLua/qyl"
                                            rel="noreferrer"
                                            target="_blank"
                                        />
                                    }
                                >
                                    GitHub
                                    <ExternalLink className="h-4 w-4"/>
                                </Button>
                            </div>
                        </div>
                    </div>
                </header>

                <main className="pt-[73px]">
                    <section
                        id="surface"
                        className="relative flex min-h-[calc(100svh-73px)] items-center border-b border-white/6"
                    >
                        <div className="mx-auto grid w-full max-w-[1480px] gap-16 px-6 py-14 lg:grid-cols-[minmax(0,35rem)_1fr] lg:px-10 xl:px-16">
                            <div
                                className="max-w-[36rem]"
                                style={{animation: 'data-stream 720ms ease-out both'}}
                            >
                                <Eyebrow>Operator launch surface</Eyebrow>
                                <div className="mt-8">
                                    <QylWordmark/>
                                </div>

                                <h1 className="mt-8 max-w-[10ch] text-5xl font-semibold leading-[0.92] tracking-[-0.05em] text-brutal-white sm:text-6xl lg:text-7xl">
                                    See what happened.
                                    <span className="block text-signal-violet">See what it cost.</span>
                                </h1>

                                <p className="mt-6 max-w-[36rem] text-base leading-7 text-brutal-slate sm:text-lg">
                                    OTLP-native observability for traces, logs, metrics, GenAI cost, and agent-native investigation.
                                    qyl stays off your control path, stores telemetry in DuckDB, and gives operators a clean place to inspect facts before they automate anything.
                                </p>

                                <div className="mt-9 flex flex-wrap gap-3">
                                    <Button
                                        className="h-11 rounded-full bg-signal-violet px-6 text-brutal-white hover:bg-signal-violet/90"
                                        render={<Link to="/traces"/>}
                                    >
                                        Open traces
                                        <ArrowRight className="h-4 w-4"/>
                                    </Button>
                                    <Button
                                        variant="outline"
                                        className="h-11 rounded-full border-white/10 bg-white/0 px-5 text-brutal-white hover:bg-white/6"
                                        render={<Link to="/issues"/>}
                                    >
                                        Inspect issues
                                    </Button>
                                </div>

                                <div className="mt-9 overflow-hidden rounded-[22px] border border-white/10 bg-white/5 shadow-[0_18px_60px_rgba(0,0,0,0.35)] backdrop-blur-xl scan-lines">
                                    <div className="flex items-center justify-between gap-4 px-4 py-3 sm:px-5">
                                        <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                            Send telemetry
                                        </div>
                                        <button
                                            type="button"
                                            className="inline-flex items-center gap-2 text-sm text-brutal-slate transition-colors hover:text-brutal-white"
                                            onClick={() => copyValue(selectedProtocol.env, selectedProtocol.label)}
                                        >
                                            <Copy className="h-4 w-4"/>
                                            Copy env
                                        </button>
                                    </div>
                                    <div className="border-t border-white/8 px-4 py-4 sm:px-5">
                                        <code className="block overflow-x-auto font-mono text-sm text-brutal-white">
                                            {selectedProtocol.env}
                                        </code>
                                    </div>
                                </div>

                                <div className="mt-8 overflow-hidden rounded-[22px] border border-white/10 bg-white/5 backdrop-blur-xl">
                                    <StatusRow
                                        active={hasCollectorMeta}
                                        label="Collector"
                                        value={collectorStatus}
                                    />
                                    <div className="border-t border-white/8">
                                        <StatusRow
                                            active={connection.grpcEnabled}
                                            label="Transport"
                                            value={transportSummary}
                                        />
                                    </div>
                                    <div className="border-t border-white/8">
                                        <StatusRow
                                            active={Boolean(githubStatus?.configured)}
                                            label="GitHub"
                                            value={githubSummary}
                                        />
                                    </div>
                                </div>
                            </div>

                            <HeroScene
                                githubLabel={githubSummary}
                                mcpSurface="qyl.mcp /mcp   streamable-http   separate-service"
                                transportLabel={transportSummary}
                            />
                        </div>
                    </section>

                    <section id="mcp" className="border-b border-white/6">
                        <div className="mx-auto max-w-[1480px] px-6 py-20 lg:px-10 xl:px-16">
                            <div className="grid gap-14 lg:grid-cols-[minmax(0,26rem)_1fr]">
                                <div className="lg:sticky lg:top-24 lg:self-start">
                                    <Eyebrow>MCP surface</Eyebrow>
                                    <div className="mt-5">
                                        <SectionTitle>Give your agent scoped access to telemetry.</SectionTitle>
                                    </div>
                                    <p className="mt-6 max-w-[34rem] text-base leading-7 text-brutal-slate">
                                        qyl.mcp stays separate from the collector and exposes the telemetry surface over stdio or Streamable HTTP.
                                        The contract keeps raw facts, AI analysis, and proposed actions explicitly separated so operators can trust what the agent is looking at.
                                    </p>
                                </div>

                                <div className="space-y-6">
                                    <div className="overflow-hidden rounded-[30px] border border-white/10 bg-white/4 shadow-[0_24px_80px_rgba(0,0,0,0.28)] backdrop-blur-xl">
                                        {scopeGroups.map((scope, index) => (
                                            <div
                                                key={scope.title}
                                                className={cn(index > 0 && 'border-t border-white/8')}
                                            >
                                                <ScopeRow scope={scope}/>
                                            </div>
                                        ))}
                                    </div>

                                    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
                                        <div className="overflow-hidden rounded-[30px] border border-white/10 bg-[#0c1221]/84 p-6 shadow-[0_24px_80px_rgba(0,0,0,0.28)] backdrop-blur-xl">
                                            <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                Provenance contract
                                            </div>
                                            <div className="mt-6 grid gap-5 md:grid-cols-3">
                                                <div className="border-l border-signal-cyan/30 pl-4">
                                                    <div className="text-sm font-semibold uppercase tracking-[0.18em] text-signal-cyan">
                                                        Facts
                                                    </div>
                                                    <p className="mt-2 text-sm leading-6 text-brutal-slate">
                                                        Traces, logs, metrics, costs, services, and issue records from qyl.collector.
                                                    </p>
                                                </div>
                                                <div className="border-l border-signal-violet/30 pl-4">
                                                    <div className="text-sm font-semibold uppercase tracking-[0.18em] text-signal-violet">
                                                        Analysis
                                                    </div>
                                                    <p className="mt-2 text-sm leading-6 text-brutal-slate">
                                                        Loom reasoning and pattern evaluation layered on top without rewriting the evidence.
                                                    </p>
                                                </div>
                                                <div className="border-l border-amber-300/35 pl-4">
                                                    <div className="text-sm font-semibold uppercase tracking-[0.18em] text-amber-100/90">
                                                        Actions
                                                    </div>
                                                    <p className="mt-2 text-sm leading-6 text-brutal-slate">
                                                        Triage updates, annotations, or follow-up steps kept separate from telemetry facts.
                                                    </p>
                                                </div>
                                            </div>
                                        </div>

                                        <div className="overflow-hidden rounded-[30px] border border-white/10 bg-white/4 p-6 backdrop-blur-xl">
                                            <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                Transport notes
                                            </div>
                                            <div className="mt-5 space-y-4 text-sm leading-6 text-brutal-white">
                                                <div className="rounded-[20px] border border-white/8 bg-white/4 px-4 py-3">
                                                    <div className="font-semibold text-brutal-white">Local mode</div>
                                                    <div className="mt-1 text-brutal-slate">stdio transport for local agent workflows.</div>
                                                </div>
                                                <div className="rounded-[20px] border border-white/8 bg-white/4 px-4 py-3">
                                                    <div className="font-semibold text-brutal-white">Remote mode</div>
                                                    <div className="mt-1 text-brutal-slate">Streamable HTTP at <code className="font-mono text-brutal-white">/mcp</code>.</div>
                                                </div>
                                                <div className="rounded-[20px] border border-white/8 bg-white/4 px-4 py-3">
                                                    <div className="font-semibold text-brutal-white">Collector boundary</div>
                                                    <div className="mt-1 text-brutal-slate">MCP stays HTTP-only; no project reference to the collector runtime.</div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </section>

                    <section id="otlp" className="border-b border-white/6">
                        <div className="mx-auto max-w-[1480px] px-6 py-20 lg:px-10 xl:px-16">
                            <div className="grid gap-14 lg:grid-cols-[minmax(0,26rem)_1fr]">
                                <div className="lg:sticky lg:top-24 lg:self-start">
                                    <Eyebrow>OTLP ingest</Eyebrow>
                                    <div className="mt-5">
                                        <SectionTitle>Point any OpenTelemetry pipeline at qyl.</SectionTitle>
                                    </div>
                                    <p className="mt-6 max-w-[34rem] text-base leading-7 text-brutal-slate">
                                        qyl stays .NET-first without locking the rest of your stack to a custom SDK. Bring a standard OTel exporter, use the qyl line if you want a one-line .NET setup, and start collecting facts immediately.
                                    </p>

                                    <div className="mt-10 space-y-8">
                                        {launchSteps.map((step, index) => (
                                            <div key={step.title} className="flex gap-4">
                                                <div className="flex h-10 w-10 items-center justify-center rounded-full border border-signal-violet/30 bg-signal-violet/10 text-sm font-semibold text-signal-violet">
                                                    {index + 1}
                                                </div>
                                                <div>
                                                    <div className="text-lg font-medium tracking-[-0.02em] text-brutal-white">
                                                        {step.title}
                                                    </div>
                                                    <p className="mt-2 max-w-[32rem] text-sm leading-6 text-brutal-slate">
                                                        {step.description}
                                                    </p>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                </div>

                                <div className="space-y-6">
                                    <div className="overflow-hidden rounded-[30px] border border-white/10 bg-[#0c1221]/84 shadow-[0_24px_80px_rgba(0,0,0,0.28)] backdrop-blur-xl">
                                        <div className="flex flex-wrap items-center justify-between gap-4 px-6 py-5">
                                            <div>
                                                <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                    Transport selector
                                                </div>
                                                <div className="mt-2 text-lg font-medium tracking-[-0.02em] text-brutal-white">
                                                    {selectedProtocol.label}
                                                </div>
                                            </div>
                                            <div className="inline-flex rounded-full border border-white/10 bg-white/4 p-1">
                                                <button
                                                    type="button"
                                                    className={cn(
                                                        'rounded-full px-4 py-2 text-sm transition-colors',
                                                        protocol === 'http'
                                                            ? 'bg-signal-violet text-brutal-white'
                                                            : 'text-brutal-slate hover:text-brutal-white'
                                                    )}
                                                    onClick={() => setProtocol('http')}
                                                >
                                                    HTTP
                                                </button>
                                                <button
                                                    type="button"
                                                    className={cn(
                                                        'rounded-full px-4 py-2 text-sm transition-colors',
                                                        protocol === 'grpc'
                                                            ? 'bg-signal-violet text-brutal-white'
                                                            : 'text-brutal-slate hover:text-brutal-white',
                                                        !connection.grpcEnabled && 'cursor-not-allowed opacity-45'
                                                    )}
                                                    disabled={!connection.grpcEnabled}
                                                    onClick={() => setProtocol('grpc')}
                                                >
                                                    gRPC
                                                </button>
                                            </div>
                                        </div>

                                        <div className="border-t border-white/8 px-6 py-6">
                                            <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_15rem]">
                                                <div className="overflow-hidden rounded-[22px] border border-white/10 bg-black/20">
                                                    <div className="flex items-center justify-between gap-4 px-4 py-3">
                                                        <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                            Export target
                                                        </div>
                                                        <button
                                                            type="button"
                                                            className="inline-flex items-center gap-2 text-sm text-brutal-slate transition-colors hover:text-brutal-white"
                                                            onClick={() => copyValue(selectedProtocol.env, selectedProtocol.label)}
                                                        >
                                                            <Copy className="h-4 w-4"/>
                                                            Copy
                                                        </button>
                                                    </div>
                                                    <div className="border-t border-white/8 px-4 py-4">
                                                        <code className="block overflow-x-auto font-mono text-sm text-brutal-white">
                                                            {selectedProtocol.env}
                                                        </code>
                                                    </div>
                                                </div>

                                                <div className="rounded-[22px] border border-white/10 bg-white/4 p-4">
                                                    <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                        Endpoint
                                                    </div>
                                                    <div className="mt-3 break-all font-mono text-sm text-brutal-white">
                                                        {selectedProtocol.endpoint}
                                                    </div>
                                                    <p className="mt-4 text-sm leading-6 text-brutal-slate">
                                                        {selectedProtocol.note}
                                                    </p>
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
                                        <div className="overflow-hidden rounded-[30px] border border-white/10 bg-white/4 backdrop-blur-xl">
                                            <div className="flex items-center justify-between gap-4 px-6 py-5">
                                                <div>
                                                    <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                        qyl .NET shortcut
                                                    </div>
                                                    <div className="mt-2 text-lg font-medium tracking-[-0.02em] text-brutal-white">
                                                        One line for the qyl SDK
                                                    </div>
                                                </div>
                                                <button
                                                    type="button"
                                                    className="inline-flex items-center gap-2 text-sm text-brutal-slate transition-colors hover:text-brutal-white"
                                                    onClick={() => copyValue('builder.AddQyl();', 'Qyl SDK line')}
                                                >
                                                    <Copy className="h-4 w-4"/>
                                                    Copy
                                                </button>
                                            </div>
                                            <div className="border-t border-white/8 px-6 py-6">
                                                <code className="block overflow-x-auto font-mono text-base text-brutal-white">
                                                    builder.AddQyl();
                                                </code>
                                                <p className="mt-4 max-w-[56ch] text-sm leading-6 text-brutal-slate">
                                                    qyl is .NET-first. The source generators emit compile-time interceptors, wire OTLP export, and keep runtime instrumentation clean.
                                                </p>
                                            </div>
                                        </div>

                                        <div className="overflow-hidden rounded-[30px] border border-white/10 bg-white/4 p-6 backdrop-blur-xl">
                                            <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                Live ports
                                            </div>
                                            <div className="mt-5 space-y-4">
                                                <div className="flex items-center justify-between gap-4 text-sm">
                                                    <span className="text-brutal-slate">Dashboard</span>
                                                    <span className="font-mono text-brutal-white">{connection.dashboardPort}</span>
                                                </div>
                                                <div className="flex items-center justify-between gap-4 text-sm">
                                                    <span className="text-brutal-slate">OTLP HTTP</span>
                                                    <span className="font-mono text-brutal-white">{connection.otlpHttpPort}</span>
                                                </div>
                                                <div className="flex items-center justify-between gap-4 text-sm">
                                                    <span className="text-brutal-slate">OTLP gRPC</span>
                                                    <span className="font-mono text-brutal-white">
                                                        {connection.grpcEnabled ? connection.grpcPort : 'disabled'}
                                                    </span>
                                                </div>
                                                <div className="flex items-center justify-between gap-4 text-sm">
                                                    <span className="text-brutal-slate">Origin</span>
                                                    <span className="font-mono text-brutal-white">{window.location.origin}</span>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </section>

                    <section id="launch">
                        <div className="mx-auto max-w-[1480px] px-6 py-20 lg:px-10 xl:px-16">
                            <div className="grid gap-14 lg:grid-cols-[minmax(0,30rem)_1fr]">
                                <div className="relative overflow-hidden rounded-[30px] border border-white/10 bg-white/4 p-6 shadow-[0_24px_80px_rgba(0,0,0,0.28)] backdrop-blur-xl sm:p-8">
                                    <div className="absolute inset-0 bg-[radial-gradient(circle_at_18%_18%,rgba(139,92,246,0.10),transparent_34%),linear-gradient(180deg,rgba(255,255,255,0.03),rgba(255,255,255,0))]"/>
                                    <div className="relative">
                                        <Eyebrow>Launch qyl</Eyebrow>
                                        <div className="mt-5">
                                            <SectionTitle>One image to launch. One surface to inspect.</SectionTitle>
                                        </div>
                                        <p className="mt-6 max-w-[34rem] text-base leading-7 text-brutal-slate">
                                            Start with the collector and dashboard. Add qyl.mcp when you want natural-language telemetry access. Add Loom when you want multi-step AI investigation and autofix. The product layers stay separate, but the operator story stays coherent.
                                        </p>
                                    </div>
                                </div>

                                <div className="space-y-6">
                                    <div className="overflow-hidden rounded-[30px] border border-white/10 bg-[#0c1221]/84 shadow-[0_24px_80px_rgba(0,0,0,0.28)] backdrop-blur-xl scan-lines">
                                        <div className="flex items-center justify-between gap-4 px-6 py-5">
                                            <div>
                                                <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brutal-slate">
                                                    Launch command
                                                </div>
                                                <div className="mt-2 text-lg font-medium tracking-[-0.02em] text-brutal-white">
                                                    Single image, single process
                                                </div>
                                            </div>
                                            <button
                                                type="button"
                                                className="inline-flex items-center gap-2 text-sm text-brutal-slate transition-colors hover:text-brutal-white"
                                                onClick={() => copyValue('docker run -p 5100:5100 -p 4317:4317 -p 4318:4318 ghcr.io/ancplua/qyl', 'Launch command')}
                                            >
                                                <Copy className="h-4 w-4"/>
                                                Copy
                                            </button>
                                        </div>
                                        <div className="border-t border-white/8 px-6 py-6">
                                            <code className="block overflow-x-auto font-mono text-sm text-brutal-white">
                                                docker run -p 5100:5100 -p 4317:4317 -p 4318:4318 ghcr.io/ancplua/qyl
                                            </code>
                                        </div>
                                    </div>

                                    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_18rem]">
                                        <div className="overflow-hidden rounded-[30px] border border-white/10 bg-white/4 backdrop-blur-xl">
                                            <StatusRow
                                                active={hasCollectorMeta}
                                                label="Live origin"
                                                value={window.location.origin}
                                            />
                                            <div className="border-t border-white/8">
                                                <StatusRow
                                                    active
                                                    label="Product split"
                                                    value="collector + dashboard first, mcp + loom layered after"
                                                />
                                            </div>
                                            <div className="border-t border-white/8">
                                                <StatusRow
                                                    active={Boolean(githubStatus?.configured)}
                                                    label="Automation"
                                                    value={githubSummary}
                                                />
                                            </div>
                                        </div>

                                        <div className="flex flex-col gap-3">
                                            <Button
                                                className="h-11 rounded-full bg-signal-violet px-6 text-brutal-white hover:bg-signal-violet/90"
                                                render={<Link to="/traces"/>}
                                            >
                                                Open traces
                                                <GitBranch className="h-4 w-4"/>
                                            </Button>
                                            <Button
                                                variant="outline"
                                                className="h-11 rounded-full border-white/10 bg-white/0 px-6 text-brutal-white hover:bg-white/6"
                                                render={<Link to="/services"/>}
                                            >
                                                Service map
                                                <Database className="h-4 w-4"/>
                                            </Button>
                                            <Button
                                                variant="outline"
                                                className="h-11 rounded-full border-white/10 bg-white/0 px-6 text-brutal-white hover:bg-white/6"
                                                render={<Link to="/settings"/>}
                                            >
                                                Configure keys
                                                <Bot className="h-4 w-4"/>
                                            </Button>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </section>
                </main>
            </div>
        </div>
    );
}
