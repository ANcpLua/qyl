import type {
    CursorPageSessionEntity,
    CursorPageSpan,
    CursorPageTrace,
    HealthReport,
    HeartbeatEvent,
    LogStreamEvent,
    ProblemDetails,
} from '@ancplua/qyl-api-schema/types';
import qylSchema from '@ancplua/qyl-api-schema/json-schema' with {type: 'json'};
import Ajv2020, {type ValidateFunction} from 'ajv/dist/2020.js';

const validator = new Ajv2020({strict: false, validateFormats: false});
validator.addSchema(qylSchema);

function compile<T>(definition: string): ValidateFunction<T> {
    return validator.compile<T>({$ref: `${qylSchema.$id}#/$defs/${definition}`});
}

const validateHealthReport = compile<HealthReport>('Operations.health_ready.Response.200');
const validateSessionPage = compile<CursorPageSessionEntity>('Operations.SessionsApi_list.Response.200');
const validateTracePage = compile<CursorPageTrace>('Operations.TracesApi_list.Response.200');
const validateSessionTracePage = compile<CursorPageTrace>('Operations.SessionsApi_getTraces.Response.200');
const validateSpanPage = compile<CursorPageSpan>('Operations.TracesApi_getSpans.Response.200');
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

export const parseHealthReport = (value: unknown): HealthReport =>
    parseContract(validateHealthReport, value, '/health');

export const parseSessionPage = (value: unknown): CursorPageSessionEntity =>
    parseContract(validateSessionPage, value, '/api/v1/sessions');

export const parseTracePage = (value: unknown): CursorPageTrace =>
    parseContract(validateTracePage, value, '/api/v1/traces');

export const parseSessionTracePage = (value: unknown, sessionId: string): CursorPageTrace =>
    parseContract(validateSessionTracePage, value, `/api/v1/sessions/${sessionId}/traces`);

export const parseSpanPage = (value: unknown, traceId: string): CursorPageSpan =>
    parseContract(validateSpanPage, value, `/api/v1/traces/${traceId}/spans`);

export const parseLogStreamEvent = (value: unknown): LogStreamEvent =>
    parseContract(validateLogStreamEvent, value, '/api/v1/stream/logs log event');

export const parseHeartbeatEvent = (value: unknown): HeartbeatEvent =>
    parseContract(validateHeartbeatEvent, value, '/api/v1/stream/logs heartbeat event');

export const parseProblemDetails = (value: unknown): ProblemDetails =>
    parseContract(validateProblemDetails, value, 'error response');
