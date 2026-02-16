/**
 * Core SDK initialization. Resolves config, sets up transport, starts collectors.
 */

import type {QylConfig, QylSdk, ResolvedConfig} from './types.js';
import {Transport} from './transport.js';
import {startWebVitals} from './web-vitals.js';
import {startErrorCapture, stopErrorCapture} from './errors.js';
import {startNavigationCapture, stopNavigationCapture} from './navigation.js';
import {startResourceCapture, stopResourceCapture} from './resources.js';
import {startInteractionCapture, stopInteractionCapture} from './interactions.js';
import {patchFetch, unpatchFetch} from './context.js';

function resolveConfig(config: QylConfig): ResolvedConfig {
    return {
        endpoint: config.endpoint.replace(/\/+$/, ''), // strip trailing slashes
        serviceName: config.serviceName ?? (typeof location !== 'undefined' ? location.hostname : 'unknown'),
        serviceVersion: config.serviceVersion ?? '',
        sampleRate: config.sampleRate ?? 1.0,
        captureWebVitals: config.captureWebVitals ?? true,
        captureErrors: config.captureErrors ?? true,
        captureNavigations: config.captureNavigations ?? true,
        captureResources: config.captureResources ?? false,
        captureInteractions: config.captureInteractions ?? false,
        propagateTraceContext: config.propagateTraceContext ?? true,
        batchSize: config.batchSize ?? 10,
        flushInterval: config.flushInterval ?? 5000,
    };
}

/** Check if this request should be sampled. */
function shouldSample(rate: number): boolean {
    return rate >= 1.0 || Math.random() < rate;
}

/**
 * Initialize the qyl browser SDK.
 * Call once at application startup.
 */
export function init(config: QylConfig): QylSdk {
    const resolved = resolveConfig(config);

    if (!shouldSample(resolved.sampleRate)) {
        // Return a no-op SDK if not sampled
        return {
            flush: async () => {
            },
            shutdown: async () => {
            },
            addSpan: () => {
            },
            addLog: () => {
            },
            config: resolved,
        };
    }

    const transport = new Transport(resolved);

    if (resolved.captureWebVitals) startWebVitals(transport);
    if (resolved.captureErrors) startErrorCapture(transport);
    if (resolved.captureNavigations) startNavigationCapture(transport);
    if (resolved.captureResources) startResourceCapture(transport);
    if (resolved.captureInteractions) startInteractionCapture(transport);
    if (resolved.propagateTraceContext) patchFetch(resolved.endpoint);

    const sdk: QylSdk = {
        flush: () => transport.flush(),
        shutdown: async () => {
            stopErrorCapture();
            stopNavigationCapture();
            stopResourceCapture();
            stopInteractionCapture();
            unpatchFetch();
            await transport.shutdown();
        },
        addSpan: (span) => transport.addSpan(span),
        addLog: (log) => transport.addLog(log),
        config: resolved,
    };

    return sdk;
}
