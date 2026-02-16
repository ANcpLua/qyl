/**
 * Core Web Vitals collection via the web-vitals library.
 * Reports LCP, FID, CLS, INP, TTFB as OTLP spans.
 */

import type {Metric} from 'web-vitals';
import {onCLS, onINP, onLCP, onTTFB} from 'web-vitals';
import type {OtlpSpan} from './types.js';
import {dateNowNano, generateSpanId, generateTraceId} from './context.js';
import type {Transport} from './transport.js';

/** Map web-vitals rating to a numeric score for attributes. */
function ratingToScore(rating: Metric['rating']): number {
    switch (rating) {
        case 'good':
            return 1;
        case 'needs-improvement':
            return 2;
        case 'poor':
            return 3;
    }
}

function metricToSpan(metric: Metric): OtlpSpan {
    const now = dateNowNano();
    return {
        traceId: generateTraceId(),
        spanId: generateSpanId(),
        name: `web-vital.${metric.name}`,
        kind: 1, // INTERNAL
        startTimeUnixNano: now,
        endTimeUnixNano: now,
        attributes: [
            {key: 'web_vital.name', value: {stringValue: metric.name}},
            {key: 'web_vital.value', value: {doubleValue: metric.value}},
            {key: 'web_vital.rating', value: {stringValue: metric.rating}},
            {key: 'web_vital.rating_score', value: {intValue: String(ratingToScore(metric.rating))}},
            {key: 'web_vital.delta', value: {doubleValue: metric.delta}},
            {key: 'web_vital.id', value: {stringValue: metric.id}},
            {key: 'web_vital.navigation_type', value: {stringValue: metric.navigationType}},
            {key: 'browser.url', value: {stringValue: location.href}},
        ],
    };
}

export function startWebVitals(transport: Transport): void {
    const report = (metric: Metric) => {
        transport.addSpan(metricToSpan(metric));
    };

    onLCP(report);
    onCLS(report);
    onINP(report);
    onTTFB(report);
}
