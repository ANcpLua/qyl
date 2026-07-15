import {
    SpanStatusCodeValues,
    type AttributeValue,
    type Span,
    type SpanStatusCode,
} from '@ancplua/qyl-api-schema/types';

export type {
    GenAiEtlAuditCluster,
    GenAiEtlAuditEvaluationRequest,
    GenAiEtlAuditEvaluationResponse,
    GenAiEtlAuditReport,
    GenAiEtlCatalogTokenCostEstimate,
    GenAiEtlClusterEvaluation,
    GenAiEtlClusterScenario,
    GenAiEtlPromotionGateKind,
    GenAiEtlPromotionGateState,
    ModelCatalogSource,
    ProviderBillingAttribution,
    ProviderBillingSource,
    SessionEntity,
    Span,
} from '@ancplua/qyl-api-schema/types';

export function nsToMs(ns: number): number {
    return ns / 1_000_000;
}

export function nanoToIso(nanos: number): string {
    return new Date(nanos / 1_000_000).toISOString();
}

export function getAttributesRecord(span: Span): Record<string, AttributeValue> {
    if (!span.attributes) return {};
    const result: Record<string, AttributeValue> = {};
    for (const attr of span.attributes) result[attr.key] = attr.value;
    return result;
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
