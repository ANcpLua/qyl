import type {
    CursorPageMetricDescriptor,
    CursorPageSessionEntity,
    CursorPageSpan,
    CursorPageTrace,
    MetricQueryResult,
    HealthReport,
    HeartbeatEvent,
    LogStreamEvent,
    ProblemDetails,
} from '@ancplua/qyl-api-schema/types';
import {contractSchema} from '@ancplua/qyl-api-schema/zod';
import {z} from 'zod';

const healthReportSchema = contractSchema('Operations.health_ready.Response.200');
const sessionPageSchema = contractSchema('Operations.SessionsApi_list.Response.200');
const tracePageSchema = contractSchema('Operations.TracesApi_list.Response.200');
const sessionTracePageSchema = contractSchema('Operations.SessionsApi_getTraces.Response.200');
const spanPageSchema = contractSchema('Operations.TracesApi_getSpans.Response.200');
const metricPageSchema = contractSchema('Operations.MetricsApi_list.Response.200');
const metricQuerySchema = contractSchema('Operations.MetricsApi_query.Response.200');
const logStreamEventSchema = contractSchema('Streaming.LogStreamEvent');
const heartbeatEventSchema = contractSchema('Streaming.HeartbeatEvent');
const problemDetailsSchema = contractSchema('Common.Errors.ProblemDetails');

export function parseContract<T>(schema: z.ZodType<T>, value: unknown, context: string): T {
    const result = schema.safeParse(value);
    if (result.success) return result.data;
    throw new Error(`Collector contract mismatch for ${context}: ${z.prettifyError(result.error)}`);
}

export const parseHealthReport = (value: unknown): HealthReport =>
    parseContract(healthReportSchema, value, '/health');

export const parseSessionPage = (value: unknown): CursorPageSessionEntity =>
    parseContract(sessionPageSchema, value, '/api/v1/sessions');

export const parseTracePage = (value: unknown): CursorPageTrace =>
    parseContract(tracePageSchema, value, '/api/v1/traces');

export const parseSessionTracePage = (value: unknown, sessionId: string): CursorPageTrace =>
    parseContract(sessionTracePageSchema, value, `/api/v1/sessions/${sessionId}/traces`);

export const parseSpanPage = (value: unknown, traceId: string): CursorPageSpan =>
    parseContract(spanPageSchema, value, `/api/v1/traces/${traceId}/spans`);

export const parseMetricPage = (value: unknown): CursorPageMetricDescriptor =>
    parseContract(metricPageSchema, value, '/api/v1/metrics');

export const parseMetricQuery = (value: unknown, name: string): MetricQueryResult =>
    parseContract(metricQuerySchema, value, `/api/v1/metrics/${name}/query`);

export const parseLogStreamEvent = (value: unknown): LogStreamEvent =>
    parseContract(logStreamEventSchema, value, '/api/v1/stream/logs log event');

export const parseHeartbeatEvent = (value: unknown): HeartbeatEvent =>
    parseContract(heartbeatEventSchema, value, '/api/v1/stream/logs heartbeat event');

export const parseProblemDetails = (value: unknown): ProblemDetails =>
    parseContract(problemDetailsSchema, value, 'error response');
