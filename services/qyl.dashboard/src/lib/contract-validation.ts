import type {
    CursorPageSessionEntity,
    CursorPageSpan,
    CursorPageTrace,
    GenAiEtlAuditEvaluationRequest,
    GenAiEtlAuditEvaluationResponse,
    GenAiEtlAuditReport,
    HealthReport,
    HeartbeatEvent,
    LogStreamEvent,
    ProblemDetails,
    SessionEntity,
    Span,
    Trace,
} from '@ancplua/qyl-api-schema/types';
import qylSchema from '@ancplua/qyl-api-schema/json-schema' with {type: 'json'};
import Ajv2020, {type ValidateFunction} from 'ajv/dist/2020.js';

const validator = new Ajv2020({strict: false, validateFormats: false});
validator.addSchema(qylSchema);

function compile<T>(definition: string): ValidateFunction<T> {
    return validator.compile<T>({$ref: `${qylSchema.$id}#/$defs/${definition}`});
}

const validateHealthReport = compile<HealthReport>('Health.HealthReport');
const validateSession = compile<SessionEntity>('Domains.Observe.Session.SessionEntity');
const validateSpan = compile<Span>('OTel.Traces.Span');
const validateTrace = compile<Trace>('OTel.Traces.Trace');
const validateAuditReport = compile<GenAiEtlAuditReport>('Cost.GenAiEtlAuditReport');
const validateAuditRequest = compile<GenAiEtlAuditEvaluationRequest>('Cost.GenAiEtlAuditEvaluationRequest');
const validateAuditResponse = compile<GenAiEtlAuditEvaluationResponse>('Cost.GenAiEtlAuditEvaluationResponse');
const validateLogStreamEvent = compile<LogStreamEvent>('Streaming.LogStreamEvent');
const validateHeartbeatEvent = compile<HeartbeatEvent>('Streaming.HeartbeatEvent');
const validateProblemDetails = compile<ProblemDetails>('Common.Errors.ProblemDetails');

function assertContract<T>(
    contractValidator: ValidateFunction<T>,
    value: unknown,
    context: string,
): asserts value is T {
    if (contractValidator(value)) return;
    throw new Error(
        `Collector contract mismatch for ${context}: ${validator.errorsText(
            contractValidator.errors,
            {separator: '; ', dataVar: context},
        )}`,
    );
}

function parseContract<T>(contractValidator: ValidateFunction<T>, value: unknown, context: string): T {
    assertContract(contractValidator, value, context);
    return value;
}

function parseCursorPage<TPage, TItem>(
    value: unknown,
    context: string,
    itemValidator: ValidateFunction<TItem>,
): TPage {
    if (typeof value !== 'object' || value === null || Array.isArray(value)) {
        throw new Error(`Collector contract mismatch for ${context}: expected an object`);
    }

    const page = value as Record<string, unknown>;
    if (!Array.isArray(page.items) || typeof page.has_more !== 'boolean') {
        throw new Error(`Collector contract mismatch for ${context}: expected items[] and has_more:boolean`);
    }
    for (const cursor of ['next_cursor', 'prev_cursor'] as const) {
        if (page[cursor] !== undefined && typeof page[cursor] !== 'string') {
            throw new Error(`Collector contract mismatch for ${context}.${cursor}: expected a string`);
        }
    }
    page.items.forEach((item, index) => assertContract(itemValidator, item, `${context}.items[${index}]`));
    return value as TPage;
}

export const parseHealthReport = (value: unknown): HealthReport =>
    parseContract(validateHealthReport, value, '/health');

export const parseSessionPage = (value: unknown): CursorPageSessionEntity =>
    parseCursorPage(value, '/api/v1/sessions', validateSession);

export const parseTracePage = (value: unknown, context = '/api/v1/traces'): CursorPageTrace =>
    parseCursorPage(value, context, validateTrace);

export const parseSpanPage = (value: unknown, context: string): CursorPageSpan =>
    parseCursorPage(value, context, validateSpan);

export const parseGenAiEtlAuditReport = (value: unknown): GenAiEtlAuditReport =>
    parseContract(validateAuditReport, value, '/api/v1/cost/etl-audit');

export const parseGenAiEtlAuditEvaluationRequest = (value: unknown): GenAiEtlAuditEvaluationRequest =>
    parseContract(validateAuditRequest, value, '/api/v1/cost/etl-audit/evaluate request');

export const parseGenAiEtlAuditEvaluationResponse = (value: unknown): GenAiEtlAuditEvaluationResponse =>
    parseContract(validateAuditResponse, value, '/api/v1/cost/etl-audit/evaluate response');

export const parseLogStreamEvent = (value: unknown): LogStreamEvent =>
    parseContract(validateLogStreamEvent, value, '/api/v1/stream/logs log event');

export const parseHeartbeatEvent = (value: unknown): HeartbeatEvent =>
    parseContract(validateHeartbeatEvent, value, '/api/v1/stream/logs heartbeat event');

export const parseProblemDetails = (value: unknown): ProblemDetails =>
    parseContract(validateProblemDetails, value, 'error response');
