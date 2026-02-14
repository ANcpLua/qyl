import {useState, useEffect, useCallback} from 'react';
import {useNavigate} from 'react-router-dom';
import {
    ArrowLeft,
    ArrowRight,
    Check,
    CheckCircle2,
    Copy,
    Loader2,
    Plug,
    Rocket,
    Sparkles,
    Terminal,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {toast} from 'sonner';

const STEPS = ['Welcome', 'Connect', 'SDK Setup', 'Verify', 'Done'] as const;
type Step = (typeof STEPS)[number];

function StepIndicator({current, steps}: {current: number; steps: readonly string[]}) {
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

function CodeBlock({code, label}: {code: string; label?: string}) {
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
            <pre className="bg-slate-900 border-2 border-brutal-zinc p-4 text-sm font-mono text-brutal-white overflow-x-auto">
                {code}
            </pre>
            <Button
                variant="ghost"
                size="icon"
                className="absolute top-2 right-2 h-7 w-7 opacity-0 group-hover:opacity-100 transition-opacity text-brutal-slate hover:text-brutal-white"
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
            <div className="w-16 h-16 mx-auto bg-signal-orange flex items-center justify-center border-2 border-brutal-black">
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
                For gRPC transport, use <span className="text-brutal-white font-mono">OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317</span>
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

function VerifyStep() {
    const [status, setStatus] = useState<'idle' | 'polling' | 'success' | 'timeout'>('idle');
    const [elapsed, setElapsed] = useState(0);

    const startPolling = useCallback(() => {
        setStatus('polling');
        setElapsed(0);
    }, []);

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
                const res = await fetch('/api/v1/spans?limit=1');
                if (res.ok) {
                    const data = await res.json();
                    if (data && ((Array.isArray(data) && data.length > 0) || (data.items && data.items.length > 0))) {
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
            <div className="w-16 h-16 mx-auto bg-signal-green flex items-center justify-center border-2 border-brutal-black">
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

const stepComponents: Record<Step, () => React.JSX.Element> = {
    'Welcome': WelcomeStep,
    'Connect': ConnectStep,
    'SDK Setup': SdkSetupStep,
    'Verify': VerifyStep,
    'Done': DoneStep,
};

export function OnboardingPage() {
    const [currentStep, setCurrentStep] = useState(0);
    const StepComponent = stepComponents[STEPS[currentStep]];
    const isFirst = currentStep === 0;
    const isLast = currentStep === STEPS.length - 1;

    return (
        <div className="p-6 space-y-8 max-w-3xl mx-auto">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-xs font-bold text-brutal-slate tracking-[0.3em] uppercase">ONBOARDING</h1>
                    <div className="text-lg font-bold text-brutal-white tracking-wider mt-1">
                        {STEPS[currentStep].toUpperCase()}
                    </div>
                </div>
                <StepIndicator current={currentStep} steps={STEPS}/>
            </div>

            {/* Step content */}
            <Card>
                <CardContent className="py-10 px-8">
                    <StepComponent/>
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
                {!isLast && (
                    <Button
                        className="bg-signal-green hover:bg-signal-green/80 text-brutal-black font-bold tracking-wider border-2 border-signal-green"
                        onClick={() => setCurrentStep((s) => s + 1)}
                    >
                        {currentStep === STEPS.length - 2 ? 'FINISH' : 'NEXT'}
                        <ArrowRight className="w-4 h-4 ml-2"/>
                    </Button>
                )}
            </div>
        </div>
    );
}
