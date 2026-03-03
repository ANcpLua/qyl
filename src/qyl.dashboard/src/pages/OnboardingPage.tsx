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
    Loader2,
    Plug,
    Rocket,
    SkipForward,
    Sparkles,
    Terminal,
    Unlink,
} from 'lucide-react';

function GitHubIcon({className}: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"
             strokeLinejoin="round" className={className}>
            <path
                d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.08-1.25-.27-2.48-1-3.5.28-1.15.28-2.35 0-3.5 0 0-1 0-3 1.5-2.64-.5-5.36-.5-8 0C6 2 5 2 5 2c-.3 1.15-.3 2.35 0 3.5A5.403 5.403 0 0 0 4 9c0 3.5 3 5.5 6 5.5-.39.49-.68 1.05-.85 1.65-.17.6-.22 1.23-.15 1.85v4"/>
            <path d="M9 18c-4.51 2-5-2-7-2"/>
        </svg>
    );
}
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Badge} from '@/components/ui/badge';
import {toast} from 'sonner';

const STEPS = ['Welcome', 'GitHub', 'Connect', 'SDK Setup', 'Verify', 'Done'] as const;

function StepIndicator({current, steps}: { current: number; steps: readonly string[] }) {
    return (
        <div className="flex items-center justify-end gap-1.5 md:gap-2 flex-wrap">
            {steps.map((label, i) => (
                <div key={label} className="flex items-center gap-1.5 md:gap-2">
                    <div
                        className={cn(
                            'w-7 h-7 md:w-8 md:h-8 flex items-center justify-center border-2 text-[11px] font-bold transition-colors',
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
                                'w-5 md:w-8 h-0.5',
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
                    <GitHubIcon className="w-6 h-6 text-brutal-white"/>
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
                                    <GitHubIcon className="w-4 h-4 mr-2"/>
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
                                className="flex-1 bg-brutal-carbon border-2 border-brutal-zinc px-3 py-2 text-sm font-mono text-brutal-white placeholder:text-brutal-slate/50 focus:border-signal-orange outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2"
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
    const {data: meta} = useQuery({
        queryKey: ['meta'],
        queryFn: async () => {
            const res = await fetch('/api/v1/meta');
            if (!res.ok) return null;
            return res.json() as Promise<{ ports: { http: number; grpc: number; otlpHttp: number } }>;
        },
        staleTime: 1000 * 60 * 5,
    });

    const isLocal = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
    const httpPort = meta?.ports?.otlpHttp || 4318;
    const grpcPort = meta?.ports?.grpc || 4317;
    const dashPort = meta?.ports?.http || 5100;

    const otlpEndpoint = isLocal ? `http://localhost:${httpPort}` : window.location.origin;
    const grpcEndpoint = isLocal ? `http://localhost:${grpcPort}` : window.location.origin;

    return (
        <div className="space-y-6 max-w-xl mx-auto">
            <h2 className="text-xl font-bold text-brutal-white tracking-wider">CONFIGURE OTLP ENDPOINT</h2>

            <div className="border-2 border-signal-green/30 bg-signal-green/5 p-4 space-y-2">
                <div className="text-xs font-bold text-signal-green tracking-wider">ALREADY USING OPENTELEMETRY?</div>
                <p className="text-brutal-slate text-sm">
                    Set this env var and you're done. Works with any OTel SDK in any language.
                </p>
                <CodeBlock
                    code={`OTEL_EXPORTER_OTLP_ENDPOINT=${otlpEndpoint}`}
                    label="OTLP Endpoint"
                />
                {isLocal && (
                    <p className="text-[10px] text-brutal-slate">
                        Most SDKs default to gRPC. For HTTP, also set{' '}
                        <span className="text-brutal-white font-mono">OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf</span>
                    </p>
                )}
                {!isLocal && (
                    <p className="text-[10px] text-brutal-slate">
                        Set <span className="text-brutal-white font-mono">OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf</span> for
                        HTTP transport (recommended for cloud deployments).
                    </p>
                )}
            </div>

            {isLocal ? (
                <div className="border-2 border-brutal-zinc p-4 bg-brutal-dark space-y-3">
                    <div className="text-xs font-bold text-brutal-slate tracking-wider">PORTS</div>
                    <div className="space-y-2 text-sm font-mono">
                        <div className="flex justify-between">
                            <span className="text-brutal-slate">Dashboard</span>
                            <span className="text-brutal-white">:{dashPort}</span>
                        </div>
                        <div className="flex justify-between">
                            <span className="text-brutal-slate">OTLP HTTP</span>
                            <span className="text-signal-green">:{httpPort}</span>
                        </div>
                        <div className="flex justify-between">
                            <span className="text-brutal-slate">OTLP gRPC</span>
                            <span className="text-signal-green">:{grpcPort}</span>
                        </div>
                    </div>
                </div>
            ) : (
                <div className="border-2 border-brutal-zinc p-4 bg-brutal-dark space-y-3">
                    <div className="text-xs font-bold text-brutal-slate tracking-wider">ENDPOINT</div>
                    <code className="text-sm text-signal-green font-mono">{window.location.origin}</code>
                    <p className="text-[10px] text-brutal-slate">
                        All OTLP traffic routes through this URL. Use HTTP/protobuf protocol.
                    </p>
                </div>
            )}

            {isLocal && (
                <p className="text-[10px] text-brutal-slate tracking-wider">
                    For gRPC transport, use{' '}
                    <span className="text-brutal-white font-mono">
                        OTEL_EXPORTER_OTLP_ENDPOINT={grpcEndpoint}
                    </span>
                </p>
            )}
        </div>
    );
}

function SdkSetupStep() {
    const [activeTab, setActiveTab] = useState<'.NET' | 'Python' | 'Go' | 'Node.js'>('.NET');

    const isLocal = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
    const endpoint = isLocal ? 'http://localhost:4318' : window.location.origin;
    const grpcEndpoint = isLocal ? 'http://localhost:4317' : window.location.origin;
    const grpcHost = isLocal ? 'localhost:4317' : window.location.host;

    const snippets: Record<string, string> = {
        '.NET': `// Web application
var builder = WebApplication.CreateBuilder(args);
builder.AddQylServiceDefaults();
var app = builder.Build();
app.MapQylEndpoints();
app.Run();

// --- OR ---

// Worker / Console application
var builder = Host.CreateApplicationBuilder(args);
builder.AddQylServiceDefaults();
var app = builder.Build();
await app.RunAsync();`,

        'Python': `# Option 1: Environment variable (recommended)
# OTEL_EXPORTER_OTLP_ENDPOINT=${endpoint}
# python your_app.py

# Option 2: Programmatic
# pip install opentelemetry-sdk opentelemetry-exporter-otlp
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.${isLocal ? 'grpc' : 'http'}.trace_exporter import (
    OTLPSpanExporter,
)

provider = TracerProvider()
processor = BatchSpanProcessor(
    OTLPSpanExporter(endpoint="${isLocal ? grpcEndpoint : endpoint}")
)
provider.add_span_processor(processor)
trace.set_tracer_provider(provider)`,

        'Go': `// Option 1: Environment variable (recommended)
// OTEL_EXPORTER_OTLP_ENDPOINT=${endpoint} go run .

// Option 2: Programmatic
// go get go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptrace${isLocal ? 'grpc' : 'http'}
import (
    "go.opentelemetry.io/otel"
    "go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptrace${isLocal ? 'grpc' : 'http'}"
    sdktrace "go.opentelemetry.io/otel/sdk/trace"
)

exp, _ := otlptrace${isLocal ? 'grpc' : 'http'}.New(ctx,
    otlptrace${isLocal ? 'grpc' : 'http'}.WithEndpoint("${grpcHost}"),${isLocal ? '\n    otlptracegrpc.WithInsecure(),' : ''}
)
tp := sdktrace.NewTracerProvider(
    sdktrace.WithBatcher(exp),
)
otel.SetTracerProvider(tp)`,

        'Node.js': `// Option 1: Environment variable (recommended)
// OTEL_EXPORTER_OTLP_ENDPOINT=${endpoint} node app.js

// Option 2: Programmatic
// npm install @opentelemetry/sdk-node @opentelemetry/exporter-trace-otlp-${isLocal ? 'grpc' : 'proto'}
const { NodeSDK } = require("@opentelemetry/sdk-node");
const {
  OTLPTraceExporter,
} = require("@opentelemetry/exporter-trace-otlp-${isLocal ? 'grpc' : 'proto'}");

const sdk = new NodeSDK({
  traceExporter: new OTLPTraceExporter({
    url: "${isLocal ? grpcEndpoint : endpoint}",
  }),
});
sdk.start();`,
    };

    const tabs = Object.keys(snippets) as Array<keyof typeof snippets>;

    return (
        <div className="space-y-6 max-w-xl mx-auto">
            <h2 className="text-xl font-bold text-brutal-white tracking-wider">SDK SETUP</h2>

            <div className="border-2 border-signal-orange/30 bg-signal-orange/5 p-4 space-y-2">
                <div className="text-xs font-bold text-signal-orange tracking-wider">ALREADY USING OPENTELEMETRY?</div>
                <p className="text-brutal-slate text-sm">
                    Just set <span className="text-brutal-white font-mono">OTEL_EXPORTER_OTLP_ENDPOINT={endpoint}</span> and
                    skip this step. The snippets below are for new projects.
                </p>
            </div>

            <p className="text-brutal-slate text-sm">
                Choose your language for instrumentation setup:
            </p>
            <div className="flex gap-1 border-b-2 border-brutal-zinc">
                {tabs.map((tab) => (
                    <button
                        key={tab}
                        className={cn(
                            'px-4 py-2 text-xs font-bold tracking-wider transition-colors border-b-2 -mb-[2px]',
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
    const [attempts, setAttempts] = useState(0);

    const {data: meta} = useQuery({
        queryKey: ['meta'],
        queryFn: async () => {
            const res = await fetch('/api/v1/meta');
            if (!res.ok) return null;
            return res.json() as Promise<{ ports: { http: number; grpc: number; otlpHttp: number } }>;
        },
        staleTime: 1000 * 60 * 5,
    });
    const isLocal = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
    const otlpHttpPort = meta?.ports?.otlpHttp || 4318;
    const expectedEndpoint = isLocal ? `http://localhost:${otlpHttpPort}` : `${window.location.origin}/v1/traces`;

    const startPolling = useCallback(() => {
        setStatus('polling');
        setElapsed(0);
        setAttempts((a) => a + 1);
    }, []);

    useEffect(() => {
        if (status === 'success') onVerified(true);
    }, [status, onVerified]);

    useEffect(() => {
        if (status !== 'polling') return;

        const timer = setInterval(() => {
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
                const [tracesRes, logsRes] = await Promise.all([
                    fetch('/api/v1/traces?limit=1').catch(() => null),
                    fetch('/api/v1/logs?limit=1').catch(() => null),
                ]);
                const traces = tracesRes?.ok ? await tracesRes.json() : null;
                const logs = logsRes?.ok ? await logsRes.json() : null;
                if ((traces?.items?.length > 0 || traces?.total > 0) ||
                    (logs?.items?.length > 0 || logs?.total > 0)) {
                    setStatus('success');
                }
            } catch {
                // keep polling
            }
        }, 3000);

        return () => {
            clearInterval(timer);
            clearInterval(poll);
        };
    }, [status]);

    return (
        <div className="space-y-5 max-w-xl mx-auto text-center">
            <div
                className={cn(
                    'w-16 h-16 mx-auto flex items-center justify-center border-2 border-brutal-black transition-colors',
                    status === 'success' ? 'bg-signal-green' :
                        status === 'timeout' ? 'bg-brutal-dark border-signal-orange' :
                            status === 'polling' ? 'bg-signal-orange' :
                                'bg-brutal-dark border-brutal-zinc'
                )}
            >
                {status === 'success' ? (
                    <CheckCircle2 className="w-8 h-8 text-brutal-black"/>
                ) : status === 'polling' ? (
                    <Loader2 className="w-8 h-8 text-brutal-black animate-spin"/>
                ) : status === 'timeout' ? (
                    <Unlink className="w-8 h-8 text-signal-orange"/>
                ) : (
                    <Plug className="w-8 h-8 text-brutal-slate"/>
                )}
            </div>

            <div className="space-y-2">
                <h2 className="text-2xl font-bold text-brutal-white tracking-wide">
                    {status === 'success' ? 'DATA RECEIVED' :
                        status === 'timeout' ? 'NO DATA YET' :
                            status === 'polling' ? 'LISTENING FOR TELEMETRY' :
                                'VERIFY CONNECTION'}
                </h2>
                <p className="text-brutal-slate text-sm leading-relaxed">
                    {status === 'success'
                        ? 'Telemetry is flowing into qyl. You are ready to proceed.'
                        : status === 'timeout'
                            ? 'No telemetry was received within 30 seconds.'
                            : status === 'polling'
                                ? `Checking for traces and logs... (${elapsed}s)`
                                : 'Run your instrumented application, then verify telemetry flow.'}
                </p>
            </div>

            {(status === 'polling' || status === 'timeout') && (
                <div className="w-full bg-brutal-dark/70 border border-brutal-zinc h-2">
                    <div
                        className={cn(
                            'h-full transition-[width] duration-500',
                            status === 'timeout' ? 'bg-signal-orange/45' : 'bg-signal-orange'
                        )}
                        style={{width: `${Math.min((elapsed / 30) * 100, 100)}%`}}
                    />
                </div>
            )}

            {status === 'success' && (
                <div className="w-full bg-brutal-dark/70 border border-signal-green h-2">
                    <div className="h-full bg-signal-green w-full"/>
                </div>
            )}

            {status === 'idle' && (
                <Button
                    className="bg-signal-green hover:bg-signal-green/80 text-brutal-black font-bold tracking-wider border-2 border-signal-green"
                    onClick={startPolling}
                >
                    <Plug className="w-4 h-4 mr-2"/>
                    CHECK FOR DATA
                </Button>
            )}

            {status === 'timeout' && (
                <div className="flex items-center justify-center gap-3">
                    <Button
                        className="bg-signal-orange hover:bg-signal-orange/80 text-brutal-black font-bold tracking-wider border-2 border-signal-orange"
                        onClick={startPolling}
                    >
                        <Loader2 className="w-4 h-4 mr-2"/>
                        TRY AGAIN
                    </Button>
                    {attempts > 1 && (
                        <span className="text-[10px] text-brutal-slate">Attempt {attempts}</span>
                    )}
                </div>
            )}

            {status === 'timeout' && (
                <div className="border border-signal-orange/35 bg-gradient-to-b from-signal-orange/10 to-signal-orange/2 p-4 text-left space-y-3">
                    <div className="text-[10px] font-bold text-signal-orange tracking-wider">TROUBLESHOOTING</div>
                    <ul className="text-xs text-brutal-slate space-y-2">
                        <li className="flex items-start gap-2">
                            <span className="text-signal-orange mt-0.5">1.</span>
                            <span>Verify your app sets <span className="text-brutal-white font-mono">OTEL_EXPORTER_OTLP_ENDPOINT={expectedEndpoint}</span></span>
                        </li>
                        <li className="flex items-start gap-2">
                            <span className="text-signal-orange mt-0.5">2.</span>
                            <span>Confirm your app is running and producing telemetry</span>
                        </li>
                        <li className="flex items-start gap-2">
                            <span className="text-signal-orange mt-0.5">3.</span>
                            <span>Check that no firewall or proxy blocks OTLP traffic to this host</span>
                        </li>
                    </ul>
                </div>
            )}

            {status === 'idle' && (
                <div className="border border-brutal-zinc p-4 bg-brutal-dark/85 text-left">
                    <div className="text-[10px] font-bold text-brutal-slate tracking-wider mb-2">EXPECTED ENDPOINT</div>
                    <code className="text-xs text-signal-green font-mono">{expectedEndpoint}</code>
                    <p className="text-[10px] text-brutal-slate mt-2">
                        qyl checks for incoming traces and logs every 3 seconds for up to 30 seconds.
                    </p>
                </div>
            )}
        </div>
    );
}

function DoneStep({verified}: { verified: boolean }) {
    const navigate = useNavigate();

    const links = [
        {to: '/traces', label: 'VIEW TRACES', desc: 'Explore distributed traces'},
        {to: '/agents', label: 'VIEW AGENTS', desc: 'Monitor AI agent runs'},
        {to: '/genai', label: 'VIEW GENAI', desc: 'GenAI model telemetry'},
    ];

    return (
        <div className="space-y-6 max-w-lg mx-auto text-center">
            <div
                className={cn(
                    'w-16 h-16 mx-auto flex items-center justify-center border-2 border-brutal-black',
                    verified ? 'bg-signal-green' : 'bg-signal-orange'
                )}>
                {verified
                    ? <CheckCircle2 className="w-8 h-8 text-brutal-black"/>
                    : <Rocket className="w-8 h-8 text-brutal-black"/>
                }
            </div>
            <h2 className="text-2xl font-bold text-brutal-white tracking-wider">
                {verified ? "YOU'RE ALL SET" : 'SETUP COMPLETE'}
            </h2>
            <p className="text-brutal-slate text-sm">
                {verified
                    ? 'qyl is now receiving telemetry from your application. Explore your data:'
                    : 'Verification was skipped — send telemetry when ready. Explore qyl:'}
            </p>
            <div className="grid grid-cols-3 gap-4 pt-2">
                {links.map(({to, label, desc}) => (
                    <button
                        key={to}
                        onClick={() => navigate(to)}
                        className="border-2 border-brutal-zinc p-4 bg-brutal-dark hover:border-signal-orange hover:bg-brutal-dark/50 transition-colors text-left"
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
                return <DoneStep verified={verifyStatus}/>;
        }
    };

    return (
        <div className="p-4 sm:p-6 md:p-8 space-y-8 max-w-5xl mx-auto">
            {/* Header */}
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div>
                    <h1 className="text-xs font-bold text-brutal-slate tracking-[0.3em] uppercase">ONBOARDING</h1>
                    <div className="text-lg font-bold text-brutal-white tracking-wider mt-1">
                        {stepName.toUpperCase()}
                    </div>
                </div>
                <StepIndicator current={currentStep} steps={STEPS}/>
            </div>

            {/* Step content */}
            <Card className="border-2 border-brutal-zinc/80 bg-brutal-carbon/95 shadow-[0_18px_42px_-28px_rgba(0,0,0,0.8)]">
                <CardContent className="py-8 px-5 sm:px-8 md:px-10">
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
