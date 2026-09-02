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
import {publishedContractSchema} from '@ancplua/qyl-api-schema/zod';
import {z} from 'zod';

const healthReportSchema = publishedContractSchema<HealthReport>('Operations.health_ready.Response.200');
const sessionPageSchema = publishedContractSchema<CursorPageSessionEntity>('Operations.SessionsApi_list.Response.200');
const tracePageSchema = publishedContractSchema<CursorPageTrace>('Operations.TracesApi_list.Response.200');
const sessionTracePageSchema = publishedContractSchema<CursorPageTrace>('Operations.SessionsApi_getTraces.Response.200');
const spanPageSchema = publishedContractSchema<CursorPageSpan>('Operations.TracesApi_getSpans.Response.200');
const metricPageSchema = publishedContractSchema<CursorPageMetricDescriptor>('Operations.MetricsApi_list.Response.200');
const metricQuerySchema = publishedContractSchema<MetricQueryResult>('Operations.MetricsApi_query.Response.200');
const logStreamEventSchema = publishedContractSchema<LogStreamEvent>('Streaming.LogStreamEvent');
const heartbeatEventSchema = publishedContractSchema<HeartbeatEvent>('Streaming.HeartbeatEvent');
const problemDetailsSchema = publishedContractSchema<ProblemDetails>('Common.Errors.ProblemDetails');

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
