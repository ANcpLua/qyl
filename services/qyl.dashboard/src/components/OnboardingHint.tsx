import type {LucideIcon} from 'lucide-react';
import {CopyButton} from '@/components/ui/copy-button';

const OTLP_ENDPOINT = 'http://localhost:4318';

const DOTNET_SNIPPET = `dotnet add package Qyl.OpenTelemetry.AutoInstrumentation.Hosting

export OTEL_EXPORTER_OTLP_ENDPOINT=${OTLP_ENDPOINT}
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
export OTEL_SERVICE_NAME=<your-service>`;

interface OnboardingHintProps {
    icon: LucideIcon;
    title: string;
    description: string;
}

export function OnboardingHint({icon: Icon, title, description}: OnboardingHintProps) {
    return (
        <div className="py-12 text-center text-brutal-slate">
            <Icon className="w-12 h-12 mx-auto mb-4 opacity-50"/>
            <p>{title}</p>
            <p className="text-sm">{description}</p>

            <div className="mx-auto mt-6 max-w-lg text-left text-sm">
                <p className="text-[11px] uppercase tracking-[0.18em] text-brutal-slate">
                    Send telemetry to
                </p>
                <p className="mt-1 font-mono text-brutal-white">
                    {OTLP_ENDPOINT}
                    <span className="ml-2 text-brutal-slate">(http/protobuf)</span>
                </p>

                <div className="group relative mt-3 border border-brutal-zinc/70 bg-brutal-dark/85">
                    <pre className="overflow-x-auto p-3 font-mono text-xs text-brutal-white">
                        {DOTNET_SNIPPET}
                    </pre>
                    <CopyButton
                        value={DOTNET_SNIPPET}
                        label="Setup snippet"
                        className="absolute top-1 right-1"
                    />
                </div>
            </div>
        </div>
    );
}
