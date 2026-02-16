/**
 * OTLP HTTP JSON transport with sendBeacon fallback for page unload.
 * Batches spans and logs, flushes on interval or when batch is full.
 */

import type {
    ExportLogsServiceRequest,
    ExportTraceServiceRequest,
    OtlpLogRecord,
    OtlpResource,
    OtlpSpan,
    ResolvedConfig,
} from './types.js';

const SDK_NAME = 'qyl-browser';
const SDK_VERSION = '0.1.0';

export class Transport {
    private spans: OtlpSpan[] = [];
    private logs: OtlpLogRecord[] = [];
    private timer: ReturnType<typeof setInterval> | null = null;
    private resource: OtlpResource;
    private config: ResolvedConfig;
    private shutdownCalled = false;

    constructor(config: ResolvedConfig) {
        this.config = config;
        this.resource = {
            attributes: [
                {key: 'service.name', value: {stringValue: config.serviceName}},
                {key: 'telemetry.sdk.name', value: {stringValue: SDK_NAME}},
                {key: 'telemetry.sdk.version', value: {stringValue: SDK_VERSION}},
                {key: 'telemetry.sdk.language', value: {stringValue: 'webjs'}},
                ...(config.serviceVersion
                    ? [{key: 'service.version', value: {stringValue: config.serviceVersion}}]
                    : []),
            ],
        };

        this.timer = setInterval(() => this.flush(), config.flushInterval);

        // Flush on page unload using sendBeacon
        if (typeof document !== 'undefined') {
            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'hidden') {
                    this.flushBeacon();
                }
            });
        }
    }

    addSpan(span: OtlpSpan): void {
        if (this.shutdownCalled) return;
        this.spans.push(span);
        if (this.spans.length >= this.config.batchSize) {
            void this.flushSpans();
        }
    }

    addLog(log: OtlpLogRecord): void {
        if (this.shutdownCalled) return;
        this.logs.push(log);
        if (this.logs.length >= this.config.batchSize) {
            void this.flushLogs();
        }
    }

    async flush(): Promise<void> {
        await Promise.all([this.flushSpans(), this.flushLogs()]);
    }

    async shutdown(): Promise<void> {
        this.shutdownCalled = true;
        if (this.timer) {
            clearInterval(this.timer);
            this.timer = null;
        }
        await this.flush();
    }

    private async flushSpans(): Promise<void> {
        if (this.spans.length === 0) return;
        const batch = this.spans.splice(0);
        const payload: ExportTraceServiceRequest = {
            resourceSpans: [{
                resource: this.resource,
                scopeSpans: [{
                    scope: {name: SDK_NAME, version: SDK_VERSION},
                    spans: batch,
                }],
            }],
        };
        await this.send(`${this.config.endpoint}/v1/traces`, payload);
    }

    private async flushLogs(): Promise<void> {
        if (this.logs.length === 0) return;
        const batch = this.logs.splice(0);
        const payload: ExportLogsServiceRequest = {
            resourceLogs: [{
                resource: this.resource,
                scopeLogs: [{
                    scope: {name: SDK_NAME, version: SDK_VERSION},
                    logRecords: batch,
                }],
            }],
        };
        await this.send(`${this.config.endpoint}/v1/logs`, payload);
    }

    private async send(url: string, body: unknown): Promise<void> {
        try {
            await fetch(url, {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify(body),
                keepalive: true,
            });
        } catch {
            // Silently drop — browser SDK should never throw to the application
        }
    }

    /** Use sendBeacon for page unload (more reliable than fetch). */
    private flushBeacon(): void {
        if (this.spans.length > 0) {
            const batch = this.spans.splice(0);
            const payload: ExportTraceServiceRequest = {
                resourceSpans: [{
                    resource: this.resource,
                    scopeSpans: [{
                        scope: {name: SDK_NAME, version: SDK_VERSION},
                        spans: batch,
                    }],
                }],
            };
            navigator.sendBeacon(
                `${this.config.endpoint}/v1/traces`,
                new Blob([JSON.stringify(payload)], {type: 'application/json'}),
            );
        }

        if (this.logs.length > 0) {
            const batch = this.logs.splice(0);
            const payload: ExportLogsServiceRequest = {
                resourceLogs: [{
                    resource: this.resource,
                    scopeLogs: [{
                        scope: {name: SDK_NAME, version: SDK_VERSION},
                        logRecords: batch,
                    }],
                }],
            };
            navigator.sendBeacon(
                `${this.config.endpoint}/v1/logs`,
                new Blob([JSON.stringify(payload)], {type: 'application/json'}),
            );
        }
    }
}
