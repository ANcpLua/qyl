import {useCallback, useEffect, useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {useQuery} from '@tanstack/react-query';
import {
    ArrowLeft,
    ArrowRight,
    Check,
    CheckCircle2,
    Copy,
    ExternalLink,
    Github,
    Loader2,
    Plug,
    Rocket,
    SkipForward,
    Sparkles,
    Terminal,
    Unlink,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Badge} from '@/components/ui/badge';
import {toast} from 'sonner';

const STEPS = ['Welcome', 'GitHub', 'Connect', 'SDK Setup', 'Verify', 'Done'] as const;

function StepIndicator({current, steps}: { current: number; steps: readonly string[] }) {
    return (
        <div className="flex items-center gap-2">
            {steps.map((label, i) => (
                <div key={label} className="flex items-center gap-2">
                    <div
                        className={cn(
                            'w-8 h-8 flex items-center justify-center border-2 text-xs font-bold transition-all',
                            i < current
                                ? 'bg-signal-green border-signal-green text-brutal-black'
                                : i === current
                                    ? 'bg-signal-orange border-signal-orange text-brutal-black'
                                    : 'bg-brutal-dark border-brutal-zinc text-brutal-slate'
                        )}
                    >
                        {i < current ? <Check className="w-4 h-4"/> : i + 1}
                    </div>
                    {i < steps.length - 1 && (
                        <div
                            className={cn(
                                'w-8 h-0.5',
                                i < current ? 'bg-signal-green' : 'bg-brutal-zinc'
                            )}
                        />
                    )}
                </div>
            ))}
        </div>
    );
}

function CodeBlock({code, label}: { code: string; label?: string }) {
    const [copied, setCopied] = useState(false);

    const handleCopy = async () => {
        try {
            await navigator.clipboard.writeText(code);
            setCopied(true);
            toast.success(`${label ?? 'Code'} copied to clipboard`);
            setTimeout(() => setCopied(false), 1500);
        } catch {
            toast.error('Failed to copy to clipboard');
        }
    };

    return (
        <div className="relative group">
            <pre
                className="bg-brutal-carbon border-2 border-brutal-zinc p-4 text-sm font-mono text-brutal-white overflow-x-auto">
                {code}
            </pre>
            <Button
                variant="ghost"
                size="icon"
                className="absolute top-2 right-2 h-7 w-7 min-h-11 min-w-11 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 focus:opacity-100 transition-opacity text-brutal-slate hover:text-brutal-white"
                onClick={handleCopy}
                aria-label={copied ? 'Copied!' : `Copy ${label?.toLowerCase() ?? 'code'}`}
            >
                {copied ? <Check className="h-3.5 w-3.5 text-signal-green"/> : <Copy className="h-3.5 w-3.5"/>}
            </Button>
        </div>
    );
}

function WelcomeStep() {
    return (
        <div className="space-y-6 text-center max-w-lg mx-auto">
            <div
                className="w-16 h-16 mx-auto bg-signal-orange flex items-center justify-center border-2 border-brutal-black">
                <Rocket className="w-8 h-8 text-brutal-black"/>
            </div>
            <h2 className="text-2xl font-bold text-brutal-white tracking-wider">
                GET STARTED WITH QYL
            </h2>
            <p className="text-brutal-slate text-sm leading-relaxed">
                qyl is an AI observability platform that captures traces, logs, and metrics from your applications.
                In a few steps you'll connect your first service and start seeing telemetry data flow in real-time.
            </p>
            <div className="grid grid-cols-3 gap-4 pt-4">
                {[
                    {icon: Terminal, label: 'TRACES', desc: 'Distributed tracing'},
                    {icon: Sparkles, label: 'GENAI', desc: 'AI model telemetry'},
                    {icon: Plug, label: 'OTLP', desc: 'OpenTelemetry native'},
                ].map(({icon: Icon, label, desc}) => (
                    <div key={label} className="border-2 border-brutal-zinc p-4 bg-brutal-dark">
                        <Icon className="w-6 h-6 text-signal-orange mx-auto mb-2"/>
                        <div className="text-xs font-bold text-brutal-white tracking-wider">{label}</div>
                        <div className="text-[10px] text-brutal-slate mt-1">{desc}</div>
                    </div>
                ))}
            </div>
        </div>
    );
}

type GitHubStatus = { configured: boolean; user: { login: string; name: string; avatarUrl: string } | null; authMethod: string };

function GitHubStep({onSkip}: { onSkip: () => void }) {
    const [patToken, setPatToken] = useState('');
    const [saving, setSaving] = useState(false);
    const [deviceCode, setDeviceCode] = useState<{
        device_code: string; user_code: string; verification_uri: string; expires_in: number; interval: number
    } | null>(null);
    const [deviceStatus, setDeviceStatus] = useState<'idle' | 'polling' | 'expired'>('idle');
    const [disconnecting, setDisconnecting] = useState(false);

    const {data: ghStatus, isLoading, refetch} = useQuery({
        queryKey: ['github-status'],
        queryFn: async () => {
            const res = await fetch('/api/v1/github/status');
            if (!res.ok) return {configured: false, user: null, authMethod: 'none'};
            return res.json() as Promise<GitHubStatus>;
        },
    });

    const {data: deviceAvailable} = useQuery({
        queryKey: ['github-device-available'],
        queryFn: async () => {
            const res = await fetch('/api/v1/github/device/available');
            if (!res.ok) return {available: false};
            return res.json() as Promise<{ available: boolean }>;
        },
    });

    const handleSaveToken = async () => {
        if (!patToken.trim()) return;
        setSaving(true);
        try {
            const res = await fetch('/api/v1/github/token', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({token: patToken.trim()}),
            });
            if (res.ok) {
                toast.success('GitHub token saved');
                setPatToken('');
                await refetch();
            } else {
                const data = await res.json().catch(() => null);
                toast.error(data?.error ?? 'Invalid GitHub token');
            }
        } catch {
            toast.error('Failed to connect to server');
        } finally {
            setSaving(false);
        }
    };

    const handleStartDeviceFlow = async () => {
        try {
            const res = await fetch('/api/v1/github/device/start', {method: 'POST'});
            if (!res.ok) {
                toast.error('Failed to start device flow');
                return;
            }
            const data = await res.json();
            setDeviceCode(data);
            setDeviceStatus('polling');
        } catch {
            toast.error('Failed to connect to server');
        }
    };

    const handleDisconnect = async () => {
        setDisconnecting(true);
        try {
            await fetch('/api/v1/github/token', {method: 'DELETE'});
            toast.success('GitHub disconnected');
            await refetch();
        } catch {
            toast.error('Failed to disconnect');
        } finally {
            setDisconnecting(false);
        }
    };

    // Device flow polling
    useEffect(() => {
        if (deviceStatus !== 'polling' || !deviceCode) return;

        const interval = setInterval(async () => {
            try {
                const res = await fetch(`/api/v1/github/device/poll?deviceCode=${encodeURIComponent(deviceCode.device_code)}`);
                if (!res.ok) return;
                const data = await res.json();

                if (data.status === 'complete') {
                    setDeviceStatus('idle');
                    setDeviceCode(null);
                    toast.success(`Connected as ${data.user?.login}`);
                    await refetch();
                } else if (data.status === 'expired' || data.status === 'denied') {
                    setDeviceStatus('expired');
                    toast.error(data.error ?? 'Device flow expired');
                }
            } catch {
                // keep polling
            }
        }, (deviceCode.interval || 5) * 1000);

        // Auto-expire
        const timeout = setTimeout(() => {
            setDeviceStatus('expired');
        }, deviceCode.expires_in * 1000);

        return () => {
            clearInterval(interval);
            clearTimeout(timeout);
        };
    }, [deviceStatus, deviceCode, refetch]);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center py-12">
                <Loader2 className="w-8 h-8 animate-spin text-signal-orange"/>
            </div>
        );
    }

    // Connected state
    if (ghStatus?.configured && ghStatus.user) {
        return (
            <div className="space-y-6 max-w-lg mx-auto text-center">
                <div
                    className="w-16 h-16 mx-auto bg-signal-green flex items-center justify-center border-2 border-brutal-black overflow-hidden">
                    {ghStatus.user.avatarUrl ? (
                        <img src={ghStatus.user.avatarUrl} alt={ghStatus.user.login}
                             className="w-full h-full object-cover"/>
                    ) : (
                        <CheckCircle2 className="w-8 h-8 text-brutal-black"/>
                    )}
                </div>
                <h2 className="text-xl font-bold text-brutal-white tracking-wider">GITHUB CONNECTED</h2>
                <p className="text-signal-green font-bold tracking-wider">
                    Connected as {ghStatus.user.login}
                </p>
                {ghStatus.user.name && (
                    <p className="text-brutal-slate text-sm">{ghStatus.user.name}</p>
                )}
                <Badge variant="outline" className="text-brutal-slate">
                    {ghStatus.authMethod === 'device_flow' ? 'Device Flow' : ghStatus.authMethod === 'pat' ? 'Personal Token' : ghStatus.authMethod === 'env' ? 'Environment Variable' : ghStatus.authMethod}
                </Badge>
                {ghStatus.authMethod !== 'env' && (
                    <Button
                        variant="outline"
                        className="font-bold tracking-wider text-brutal-slate hover:text-signal-red hover:border-signal-red"
                        onClick={handleDisconnect}
                        disabled={disconnecting}
                    >
                        {disconnecting ? <Loader2 className="w-4 h-4 animate-spin mr-2"/> :
                            <Unlink className="w-4 h-4 mr-2"/>}
                        DISCONNECT
                    </Button>
                )}
            </div>
        );
    }

    // Not connected — show auth tabs
    return (
        <div className="space-y-6 max-w-xl mx-auto">
            <div className="flex items-center gap-3">
                <div
                    className="w-12 h-12 bg-brutal-dark flex items-center justify-center border-2 border-brutal-zinc">
                    <Github className="w-6 h-6 text-brutal-white"/>
                </div>
                <div>
                    <h2 className="text-xl font-bold text-brutal-white tracking-wider">CONNECT GITHUB</h2>
                    <p className="text-brutal-slate text-xs tracking-wider">OPTIONAL</p>
                </div>
            </div>
            <p className="text-brutal-slate text-sm leading-relaxed">
                Connecting GitHub enables repository discovery and Copilot integration.
                OTLP ingestion works without authentication — this step is optional.
            </p>

            <Tabs defaultValue={deviceAvailable?.available ? 'device' : 'pat'}>
                <TabsList className={cn('grid w-full', deviceAvailable?.available ? 'grid-cols-3' : 'grid-cols-2')}>
                    {deviceAvailable?.available && (
                        <TabsTrigger value="device" className="text-xs font-bold tracking-wider">DEVICE FLOW</TabsTrigger>
                    )}
                    <TabsTrigger value="pat" className="text-xs font-bold tracking-wider">PERSONAL TOKEN</TabsTrigger>
                    <TabsTrigger value="env" className="text-xs font-bold tracking-wider">ENV VARIABLE</TabsTrigger>
                </TabsList>

                {deviceAvailable?.available && (
                    <TabsContent value="device" className="space-y-4 mt-4">
                        <div className="border-2 border-brutal-zinc p-4 bg-brutal-dark space-y-4">
                            {deviceStatus === 'idle' && !deviceCode && (
                                <Button
                                    className="w-full bg-signal-green hover:bg-signal-green/80 text-brutal-black font-bold tracking-wider border-2 border-signal-green"
                                    onClick={handleStartDeviceFlow}
                                >
                                    <Github className="w-4 h-4 mr-2"/>
                                    START GITHUB LOGIN
                                </Button>
                            )}

                            {deviceStatus === 'polling' && deviceCode && (
                                <div className="space-y-4 text-center">
                                    <p className="text-brutal-slate text-sm">
                                        Enter this code on GitHub:
                                    </p>
                                    <div
                                        className="text-3xl font-mono font-bold text-signal-orange tracking-[0.3em] py-4 bg-brutal-carbon border-2 border-signal-orange">
                                        {deviceCode.user_code}
                                    </div>
                                    <a
                                        href={deviceCode.verification_uri}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="inline-flex items-center gap-2 text-signal-orange hover:underline text-sm font-bold"
                                    >
                                        Open GitHub <ExternalLink className="w-3.5 h-3.5"/>
                                    </a>
                                    <div className="flex items-center justify-center gap-2 text-brutal-slate text-xs">
                                        <Loader2 className="w-4 h-4 animate-spin"/>
                                        Waiting for authorization...
                                    </div>
                                </div>
                            )}

                            {deviceStatus === 'expired' && (
                                <div className="space-y-4 text-center">
                                    <p className="text-brutal-slate text-sm">Device code expired.</p>
                                    <Button
                                        variant="outline"
                                        className="font-bold tracking-wider"
                                        onClick={() => {
                                            setDeviceStatus('idle');
                                            setDeviceCode(null);
                                        }}
                                    >
                                        TRY AGAIN
                                    </Button>
                                </div>
                            )}
                        </div>
                    </TabsContent>
                )}

                <TabsContent value="pat" className="space-y-4 mt-4">
                    <div className="border-2 border-brutal-zinc p-4 bg-brutal-dark space-y-4">
                        <p className="text-[10px] text-brutal-slate">
                            Generate a personal access token at{' '}
                            <a
                                href="https://github.com/settings/tokens"
                                target="_blank"
                                rel="noopener noreferrer"
                                className="text-signal-orange hover:underline"
                            >
                                github.com/settings/tokens
                            </a>
                            {' '}with <span className="text-brutal-white font-mono">repo</span> scope.
                        </p>
                        <div className="flex gap-2">
                            <label htmlFor="pat-token-input" className="sr-only">GitHub Personal Access Token</label>
                            <input
                                id="pat-token-input"
                                type="password"
                                value={patToken}
                                onChange={(e) => setPatToken(e.target.value)}
                                placeholder="ghp_..."
                                aria-label="GitHub Personal Access Token"
                                className="flex-1 bg-brutal-carbon border-2 border-brutal-zinc px-3 py-2 text-sm font-mono text-brutal-white placeholder:text-brutal-slate/50 focus:border-signal-orange focus:outline-none"
                            />
                            <Button
                                className="bg-signal-green hover:bg-signal-green/80 text-brutal-black font-bold tracking-wider border-2 border-signal-green"
                                onClick={handleSaveToken}
                                disabled={!patToken.trim() || saving}
                            >
                                {saving ? <Loader2 className="w-4 h-4 animate-spin"/> : 'SAVE'}
                            </Button>
                        </div>
                    </div>
                </TabsContent>

                <TabsContent value="env" className="space-y-4 mt-4">
                    <div className="border-2 border-brutal-zinc p-4 bg-brutal-dark space-y-4">
                        <CodeBlock
                            code="QYL_GITHUB_TOKEN=ghp_your_token_here"
                            label="Environment variable"
                        />
                        <p className="text-[10px] text-brutal-slate">
                            Set this before starting the collector. Token persists across restarts.
                        </p>
                    </div>
                </TabsContent>
            </Tabs>

            <Button
                variant="outline"
                className="w-full font-bold tracking-wider text-brutal-slate hover:text-brutal-white"
                onClick={onSkip}
            >
                <SkipForward className="w-4 h-4 mr-2"/>
                SKIP — I'LL SET UP GITHUB LATER
            </Button>
        </div>
    );
}

function ConnectStep() {
    return (
        <div className="space-y-6 max-w-xl mx-auto">
            <h2 className="text-xl font-bold text-brutal-white tracking-wider">CONFIGURE OTLP ENDPOINT</h2>
            <p className="text-brutal-slate text-sm">
                Point your OpenTelemetry SDK to the qyl collector. Set this environment variable in your application:
            </p>
            <CodeBlock
                code="OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:5100"
                label="Endpoint"
            />
            <div className="border-2 border-brutal-zinc p-4 bg-brutal-dark space-y-3">
                <div className="text-xs font-bold text-brutal-slate tracking-wider">PORTS</div>
                <div className="space-y-2 text-sm font-mono">
                    <div className="flex justify-between">
                        <span className="text-brutal-slate">HTTP / REST API</span>
                        <span className="text-brutal-white">:5100</span>
                    </div>
                    <div className="flex justify-between">
                        <span className="text-brutal-slate">gRPC OTLP</span>
                        <span className="text-brutal-white">:4317</span>
                    </div>
                </div>
            </div>
            <p className="text-[10px] text-brutal-slate tracking-wider">
                For gRPC transport, use <span
                className="text-brutal-white font-mono">OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317</span>
            </p>
        </div>
    );
}

function SdkSetupStep() {
    const [activeTab, setActiveTab] = useState<'.NET' | 'Python' | 'Go' | 'Node.js'>('.NET');

    const snippets: Record<string, string> = {
        '.NET': `// Program.cs
var builder = WebApplication.CreateBuilder(args);

// One-line instrumentation with qyl defaults
builder.AddQylServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();`,

        'Python': `# pip install opentelemetry-api opentelemetry-sdk \\
#   opentelemetry-exporter-otlp

from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import (
    OTLPSpanExporter,
)

provider = TracerProvider()
processor = BatchSpanProcessor(
    OTLPSpanExporter(endpoint="http://localhost:4317")
)
provider.add_span_processor(processor)
trace.set_tracer_provider(provider)`,

        'Go': `// go get go.opentelemetry.io/otel \\
//   go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc

import (
    "go.opentelemetry.io/otel"
    "go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
    sdktrace "go.opentelemetry.io/otel/sdk/trace"
)

exp, _ := otlptracegrpc.New(ctx,
    otlptracegrpc.WithEndpoint("localhost:4317"),
    otlptracegrpc.WithInsecure(),
)
tp := sdktrace.NewTracerProvider(
    sdktrace.WithBatcher(exp),
)
otel.SetTracerProvider(tp)`,

        'Node.js': `// npm install @opentelemetry/api @opentelemetry/sdk-node \\
//   @opentelemetry/exporter-trace-otlp-grpc

const { NodeSDK } = require("@opentelemetry/sdk-node");
const {
  OTLPTraceExporter,
} = require("@opentelemetry/exporter-trace-otlp-grpc");

const sdk = new NodeSDK({
  traceExporter: new OTLPTraceExporter({
    url: "http://localhost:4317",
  }),
});
sdk.start();`,
    };

    const tabs = Object.keys(snippets) as Array<keyof typeof snippets>;

    return (
        <div className="space-y-6 max-w-xl mx-auto">
            <h2 className="text-xl font-bold text-brutal-white tracking-wider">SDK SETUP</h2>
            <p className="text-brutal-slate text-sm">
                Add OpenTelemetry instrumentation to your application. Choose your language:
            </p>
            <div className="flex gap-1 border-b-2 border-brutal-zinc">
                {tabs.map((tab) => (
                    <button
                        key={tab}
                        className={cn(
                            'px-4 py-2 text-xs font-bold tracking-wider transition-all border-b-2 -mb-[2px]',
                            activeTab === tab
                                ? 'text-signal-orange border-signal-orange'
                                : 'text-brutal-slate border-transparent hover:text-brutal-white'
                        )}
                        onClick={() => setActiveTab(tab as typeof activeTab)}
                    >
                        {tab.toUpperCase()}
                    </button>
                ))}
            </div>
            <CodeBlock code={snippets[activeTab]} label={`${activeTab} snippet`}/>
        </div>
    );
}

function VerifyStep({onVerified}: { onVerified: (ok: boolean) => void }) {
    const [status, setStatus] = useState<'idle' | 'polling' | 'success' | 'timeout'>('idle');
    const [elapsed, setElapsed] = useState(0);

    const startPolling = useCallback(() => {
        setStatus('polling');
        setElapsed(0);
    }, []);

    useEffect(() => {
        if (status === 'success') onVerified(true);
    }, [status, onVerified]);

    useEffect(() => {
        if (status !== 'polling') return;

        const interval = setInterval(() => {
            setElapsed((prev) => {
                if (prev >= 30) {
                    setStatus('timeout');
                    return prev;
                }
                return prev + 3;
            });
        }, 3000);

        const poll = setInterval(async () => {
            try {
                const res = await fetch('/api/v1/traces?limit=1');
                if (res.ok) {
                    const data = await res.json();
                    if (data?.items?.length > 0 || data?.total > 0) {
                        setStatus('success');
                    }
                }
            } catch {
                // keep polling
            }
        }, 3000);

        return () => {
            clearInterval(interval);
            clearInterval(poll);
        };
    }, [status]);

    return (
        <div className="space-y-6 max-w-lg mx-auto text-center">
            <h2 className="text-xl font-bold text-brutal-white tracking-wider">VERIFY CONNECTION</h2>
            <p className="text-brutal-slate text-sm">
                Run your instrumented application, then click below to check if telemetry data is flowing.
            </p>

            {status === 'idle' && (
                <Button
                    className="bg-signal-green hover:bg-signal-green/80 text-brutal-black font-bold tracking-wider border-2 border-signal-green"
                    onClick={startPolling}
                >
                    CHECK FOR DATA
                </Button>
            )}

            {status === 'polling' && (
                <div className="space-y-4">
                    <Loader2 className="w-10 h-10 mx-auto animate-spin text-signal-orange"/>
                    <p className="text-brutal-slate text-sm">
                        Waiting for spans… ({elapsed}s)
                    </p>
                    <div className="w-full bg-brutal-dark border-2 border-brutal-zinc h-2">
                        <div
                            className="h-full bg-signal-orange transition-all"
                            style={{width: `${Math.min((elapsed / 30) * 100, 100)}%`}}
                        />
                    </div>
                </div>
            )}

            {status === 'success' && (
                <div className="space-y-4">
                    <CheckCircle2 className="w-12 h-12 mx-auto text-signal-green"/>
                    <p className="text-signal-green font-bold tracking-wider">DATA RECEIVED</p>
                    <p className="text-brutal-slate text-sm">
                        Telemetry is flowing into qyl. You're all set!
                    </p>
                </div>
            )}

            {status === 'timeout' && (
                <div className="space-y-4">
                    <p className="text-brutal-slate text-sm">
                        No data received yet. Make sure your application is running and configured correctly.
                    </p>
                    <Button
                        variant="outline"
                        className="font-bold tracking-wider"
                        onClick={startPolling}
                    >
                        TRY AGAIN
                    </Button>
                </div>
            )}

            <div className="border-2 border-brutal-zinc p-4 bg-brutal-dark text-left">
                <div className="text-[10px] font-bold text-brutal-slate tracking-wider mb-2">TROUBLESHOOTING</div>
                <ul className="text-xs text-brutal-slate space-y-1 list-disc list-inside">
                    <li>Ensure the collector is running on port 5100</li>
                    <li>Check your OTEL_EXPORTER_OTLP_ENDPOINT variable</li>
                    <li>Verify no firewall is blocking the connection</li>
                </ul>
            </div>
        </div>
    );
}

function DoneStep() {
    const navigate = useNavigate();

    const links = [
        {to: '/traces', label: 'VIEW TRACES', desc: 'Explore distributed traces'},
        {to: '/agents', label: 'VIEW AGENTS', desc: 'Monitor AI agent runs'},
        {to: '/genai', label: 'VIEW GENAI', desc: 'GenAI model telemetry'},
    ];

    return (
        <div className="space-y-6 max-w-lg mx-auto text-center">
            <div
                className="w-16 h-16 mx-auto bg-signal-green flex items-center justify-center border-2 border-brutal-black">
                <CheckCircle2 className="w-8 h-8 text-brutal-black"/>
            </div>
            <h2 className="text-2xl font-bold text-brutal-white tracking-wider">YOU'RE ALL SET</h2>
            <p className="text-brutal-slate text-sm">
                qyl is now receiving telemetry from your application. Explore your data:
            </p>
            <div className="grid grid-cols-3 gap-4 pt-2">
                {links.map(({to, label, desc}) => (
                    <button
                        key={to}
                        onClick={() => navigate(to)}
                        className="border-2 border-brutal-zinc p-4 bg-brutal-dark hover:border-signal-orange hover:bg-brutal-dark/50 transition-all text-left"
                    >
                        <div className="text-xs font-bold text-brutal-white tracking-wider">{label}</div>
                        <div className="text-[10px] text-brutal-slate mt-1">{desc}</div>
                    </button>
                ))}
            </div>
        </div>
    );
}

export function OnboardingPage() {
    const [currentStep, setCurrentStep] = useState(0);
    const [verifyStatus, setVerifyStatus] = useState(false);
    const isFirst = currentStep === 0;
    const isLast = currentStep === STEPS.length - 1;
    const stepName = STEPS[currentStep];

    const handleSkipGitHub = () => setCurrentStep((s) => s + 1);

    const isVerifyStep = stepName === 'Verify';
    const canAdvance = !isVerifyStep || verifyStatus;

    const renderStep = () => {
        switch (stepName) {
            case 'Welcome':
                return <WelcomeStep/>;
            case 'GitHub':
                return <GitHubStep onSkip={handleSkipGitHub}/>;
            case 'Connect':
                return <ConnectStep/>;
            case 'SDK Setup':
                return <SdkSetupStep/>;
            case 'Verify':
                return <VerifyStep onVerified={setVerifyStatus}/>;
            case 'Done':
                return <DoneStep/>;
        }
    };

    return (
        <div className="p-6 space-y-8 max-w-3xl mx-auto">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-xs font-bold text-brutal-slate tracking-[0.3em] uppercase">ONBOARDING</h1>
                    <div className="text-lg font-bold text-brutal-white tracking-wider mt-1">
                        {stepName.toUpperCase()}
                    </div>
                </div>
                <StepIndicator current={currentStep} steps={STEPS}/>
            </div>

            {/* Step content */}
            <Card>
                <CardContent className="py-10 px-8">
                    {renderStep()}
                </CardContent>
            </Card>

            {/* Navigation */}
            <div className="flex justify-between">
                <Button
                    variant="outline"
                    className="font-bold tracking-wider"
                    onClick={() => setCurrentStep((s) => s - 1)}
                    disabled={isFirst}
                >
                    <ArrowLeft className="w-4 h-4 mr-2"/>
                    BACK
                </Button>
                <div className="flex gap-2">
                    {isVerifyStep && !verifyStatus && (
                        <Button
                            variant="outline"
                            className="font-bold tracking-wider text-brutal-slate hover:text-brutal-white"
                            onClick={() => setCurrentStep((s) => s + 1)}
                        >
                            <SkipForward className="w-4 h-4 mr-2"/>
                            SKIP
                        </Button>
                    )}
                    {!isLast && (
                        <Button
                            className="bg-signal-green hover:bg-signal-green/80 text-brutal-black font-bold tracking-wider border-2 border-signal-green"
                            onClick={() => setCurrentStep((s) => s + 1)}
                            disabled={isVerifyStep && !canAdvance}
                        >
                            {currentStep === STEPS.length - 2 ? 'FINISH' : 'NEXT'}
                            <ArrowRight className="w-4 h-4 ml-2"/>
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
}
