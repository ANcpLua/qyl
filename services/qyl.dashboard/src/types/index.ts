import {
    SpanStatusCodeValues,
    type AttributeValue,
    type SessionEntity,
    type Span,
    type SpanStatusCode,
} from '@ancplua/qyl-api-schema/types';

export type {
    CursorPageLogRecord,
    CursorPageSessionEntity,
    CursorPageSpan,
    CursorPageTrace,
    GenAiEtlAuditCluster,
    GenAiEtlAuditEvaluationRequest,
    GenAiEtlAuditEvaluationResponse,
    GenAiEtlAuditReport,
    GenAiEtlAuditSummary,
    GenAiEtlCandidateStatus,
    GenAiEtlCandidatePath,
    GenAiEtlCatalogTokenCostEstimate,
    GenAiEtlClusterEvaluation,
    GenAiEtlClusterScenario,
    GenAiEtlEvidenceSignal,
    GenAiEtlOutputContract,
    GenAiEtlPromotionGate,
    GenAiEtlPromotionGateKind,
    GenAiEtlPromotionGateState,
    GenAiEtlResidualPath,
    GenAiEtlTaskFamily,
    GenAiEtlValidationMetric,
    HealthReport,
    LogRecord as ContractLogRecord,
    LogStreamEvent,
    ModelCatalogSource,
    ProviderBillingAttribution,
    ProviderBillingSource,
    ProviderBillingSourceStatus,
    SessionEntity,
    Span,
    SpanStatusCode,
    Trace,
} from '@ancplua/qyl-api-schema/types';

export function nsToMs(ns: number): number {
    return ns / 1_000_000;
}

export function nanoToIso(nanos: number): string {
    return new Date(nanos / 1_000_000).toISOString();
}

export function getAttributesRecord(span: Span): Record<string, unknown> {
    if (!span.attributes) return {};
    const result: Record<string, unknown> = {};
    for (const attr of span.attributes) result[attr.key] = attr.value;
    return result;
}

export function getPrimaryService(session: SessionEntity): string {
    return session.services[0] ?? 'unknown';
}

export const STATUS_ERROR: SpanStatusCode = SpanStatusCodeValues.error;

export function getStatusLabel(code: SpanStatusCode): string {
    switch (code) {
        case SpanStatusCodeValues.unset:
            return 'unset';
        case SpanStatusCodeValues.ok:
            return 'ok';
        case SpanStatusCodeValues.error:
            return 'error';
    }
}

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

export interface LogViewRecord {
    timestamp: string;
    observedTimestamp: string;
    traceId?: string;
    spanId?: string;
    severityNumber: number;
    severityText: LogLevel;
    body: string;
    attributes: Record<string, AttributeValue>;
    serviceName: string;
    serviceVersion?: string;
}
