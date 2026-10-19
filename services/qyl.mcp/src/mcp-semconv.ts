import { SpanKind, type Attributes } from "@opentelemetry/api";
import {
    SeverityNumber,
    type LogAttributes,
    type LogBody,
} from "@opentelemetry/api-logs";
import {
    ATTR_CLIENT_ADDRESS,
    ATTR_CLIENT_PORT,
    ATTR_ERROR_TYPE,
    ATTR_NETWORK_PROTOCOL_NAME,
    ATTR_NETWORK_PROTOCOL_VERSION,
    ATTR_NETWORK_TRANSPORT,
    ATTR_USER_AGENT_ORIGINAL,
    NETWORK_TRANSPORT_VALUE_PIPE,
    NETWORK_TRANSPORT_VALUE_TCP,
} from "@ancplua/qyl-semantic-conventions";
import {
    ATTR_QYL_MCP_EXECUTION_ID,
    ATTR_QYL_MCP_SERVER_ID,
    EVENT_QYL_MCP_OPERATION,
    ATTR_GEN_AI_OPERATION_NAME,
    ATTR_GEN_AI_PROMPT_NAME,
    ATTR_GEN_AI_TOOL_NAME,
    ATTR_JSONRPC_PROTOCOL_VERSION,
    ATTR_JSONRPC_REQUEST_ID,
    ATTR_MCP_METHOD_NAME,
    ATTR_MCP_PROTOCOL_VERSION,
    ATTR_MCP_RESOURCE_URI,
    ATTR_MCP_SESSION_ID,
    ATTR_RPC_RESPONSE_STATUS_CODE,
    ATTR_USER_AGENT_NAME,
    ATTR_USER_AGENT_VERSION,
    GEN_AI_OPERATION_NAME_VALUE_EXECUTE_TOOL,
    METRIC_MCP_SERVER_OPERATION_DURATION,
    METRIC_MCP_SERVER_OPERATION_DURATION_UNIT,
} from "@ancplua/qyl-semantic-conventions/incubating";
import { SecretRedactor } from "./secret-redactor.js";

const ATTRIBUTE_VALUE_LIMIT = 2_000;
const LOG_BODY_CHARACTER_LIMIT = 64_000;
const JSONRPC_DEFAULT_VERSION = "2.0";

/** The transports this server is reached through; `inproc` and `builtin` have no network. */
export type McpTelemetryTransport =
    | "stdio"
    | "streamable_http"
    | "inproc"
    | "builtin";

export type McpPropagationCarrier = Readonly<Record<string, string>>;

interface McpNetworkInput {
    transport: McpTelemetryTransport;
    networkProtocolName?: string;
    networkProtocolVersion?: string;
    peerAddress?: string;
    peerPort?: number;
}

interface McpProtocolInput extends McpNetworkInput {
    /** The revision the request was served on: the 2026-07-28 envelope's, or the 2025 era's negotiated one. */
    protocolVersion?: string;
    jsonRpcProtocolVersion?: string;
    errorType?: string;
    rpcResponseStatusCode?: string;
}

/**
 * The caller as the request identifies it. The MCP conventions define no client
 * attribute: the client's self-declared `clientInfo` goes to `user_agent.name` and
 * `user_agent.version`, the HTTP `User-Agent` header to `user_agent.original`.
 * Span and log only, like the peer address: per-caller values would multiply the
 * metric's series.
 */
interface McpCallerInput {
    sessionId?: string;
    clientName?: string;
    clientVersion?: string;
    userAgent?: string;
}

/** One operation this server answered. Payloads never enter its telemetry. */
export interface McpOperationInput extends McpProtocolInput, McpCallerInput {
    method: string;
    toolName?: string;
    promptName?: string;
    resourceUri?: string;
    jsonRpcRequestId?: string | number;
    serverId?: string;
    executionId?: string;
    startTimeMs: number;
    endTimeMs: number;
    remotePropagation?: McpPropagationCarrier;
}

export interface McpSpanDescriptor {
    name: string;
    kind: SpanKind.SERVER;
    attributes: Attributes;
}

export interface McpMetricDescriptor {
    name: typeof METRIC_MCP_SERVER_OPERATION_DURATION;
    unit: typeof METRIC_MCP_SERVER_OPERATION_DURATION_UNIT;
    value: number;
    attributes: Attributes;
}

export interface McpLogDescriptor {
    eventName: typeof EVENT_QYL_MCP_OPERATION;
    severityNumber: SeverityNumber.INFO | SeverityNumber.ERROR;
    severityText: "INFO" | "ERROR";
    body: LogBody;
    attributes: LogAttributes;
}

export function describeMcpOperationSpan(
    input: McpOperationInput,
    redactor: SecretRedactor = new SecretRedactor(),
): McpSpanDescriptor {
    validateTiming(input.startTimeMs, input.endTimeMs);
    const safe = safeText(redactor);
    const method = requireText(input.method, "method", safe);
    const attributes: Attributes = { [ATTR_MCP_METHOD_NAME]: method };

    addOperationAttributes(attributes, input, safe);
    if (input.jsonRpcRequestId !== undefined) {
        attributes[ATTR_JSONRPC_REQUEST_ID] = safe(String(input.jsonRpcRequestId));
    }
    if (input.resourceUri && resourceMethodHasUri(input.method)) {
        attributes[ATTR_MCP_RESOURCE_URI] = bounded(redactor.redactUri(input.resourceUri));
    }
    addPeerAttributes(attributes, input, safe);
    addQylAttributes(attributes, input, safe);

    const target = input.method === "tools/call"
        ? input.toolName
        : input.method === "prompts/get" ? input.promptName : undefined;
    return {
        name: target ? `${method} ${safe(target)}` : method,
        kind: SpanKind.SERVER,
        attributes,
    };
}

export function describeMcpOperationMetric(
    input: McpOperationInput,
    redactor: SecretRedactor = new SecretRedactor(),
): McpMetricDescriptor {
    validateTiming(input.startTimeMs, input.endTimeMs);
    const safe = safeText(redactor);
    const attributes: Attributes = {
        [ATTR_MCP_METHOD_NAME]: requireText(input.method, "method", safe),
    };
    addOperationAttributes(attributes, input, safe);
    return {
        name: METRIC_MCP_SERVER_OPERATION_DURATION,
        unit: METRIC_MCP_SERVER_OPERATION_DURATION_UNIT,
        value: durationSeconds(input.startTimeMs, input.endTimeMs),
        attributes,
    };
}

export function describeMcpOperationLog(
    input: McpOperationInput,
    redactor: SecretRedactor = new SecretRedactor(),
): McpLogDescriptor {
    validateTiming(input.startTimeMs, input.endTimeMs);
    const safe = safeText(redactor);
    const attributes: Attributes = {
        [ATTR_MCP_METHOD_NAME]: requireText(input.method, "method", safe),
    };
    addOperationAttributes(attributes, input, safe);
    if (input.jsonRpcRequestId !== undefined) {
        attributes[ATTR_JSONRPC_REQUEST_ID] = safe(String(input.jsonRpcRequestId));
    }
    if (input.resourceUri && resourceMethodHasUri(input.method)) {
        attributes[ATTR_MCP_RESOURCE_URI] = bounded(redactor.redactUri(input.resourceUri));
    }
    addPeerAttributes(attributes, input, safe);
    addQylAttributes(attributes, input, safe);

    const failed = input.errorType !== undefined;
    const body = {
        message: "MCP operation completed.",
        role: "server",
        duration_ms: input.endTimeMs - input.startTimeMs,
        ...(failed
            ? {
                error: {
                    type: input.errorType,
                    ...(input.rpcResponseStatusCode === undefined
                        ? {}
                        : { code: input.rpcResponseStatusCode }),
                },
            }
            : {}),
    };
    return {
        eventName: EVENT_QYL_MCP_OPERATION,
        severityNumber: failed ? SeverityNumber.ERROR : SeverityNumber.INFO,
        severityText: failed ? "ERROR" : "INFO",
        body: boundedLogBody(body, redactor),
        attributes: attributes as LogAttributes,
    };
}

function addOperationAttributes(
    attributes: Attributes,
    input: McpOperationInput,
    safe: (value: string) => string,
): void {
    if (input.errorType) attributes[ATTR_ERROR_TYPE] = safe(input.errorType);
    if (input.protocolVersion) {
        attributes[ATTR_MCP_PROTOCOL_VERSION] = safe(input.protocolVersion);
    }
    if (input.jsonRpcProtocolVersion
        && input.jsonRpcProtocolVersion !== JSONRPC_DEFAULT_VERSION) {
        attributes[ATTR_JSONRPC_PROTOCOL_VERSION] = safe(input.jsonRpcProtocolVersion);
    }
    if (input.rpcResponseStatusCode) {
        attributes[ATTR_RPC_RESPONSE_STATUS_CODE] = safe(input.rpcResponseStatusCode);
    }
    if (input.method === "tools/call") {
        attributes[ATTR_GEN_AI_OPERATION_NAME] = GEN_AI_OPERATION_NAME_VALUE_EXECUTE_TOOL;
    }
    if (input.method === "tools/call" && input.toolName) {
        attributes[ATTR_GEN_AI_TOOL_NAME] = safe(input.toolName);
    }
    if (input.method === "prompts/get" && input.promptName) {
        attributes[ATTR_GEN_AI_PROMPT_NAME] = safe(input.promptName);
    }
    const transport = networkTransport(input.transport);
    if (transport) attributes[ATTR_NETWORK_TRANSPORT] = transport;
    const protocolName = input.networkProtocolName ?? inferredNetworkProtocol(input.transport);
    if (protocolName) attributes[ATTR_NETWORK_PROTOCOL_NAME] = safe(protocolName);
    if (input.networkProtocolVersion) {
        attributes[ATTR_NETWORK_PROTOCOL_VERSION] = safe(input.networkProtocolVersion);
    }
}

/** Span and log only: a client address or identity would explode the metric's cardinality. */
function addPeerAttributes(
    attributes: Attributes,
    input: McpNetworkInput & McpCallerInput,
    safe: (value: string) => string,
): void {
    if (input.peerAddress) attributes[ATTR_CLIENT_ADDRESS] = safe(input.peerAddress);
    if (input.peerAddress && input.peerPort !== undefined) {
        attributes[ATTR_CLIENT_PORT] = input.peerPort;
    }
    if (input.sessionId) attributes[ATTR_MCP_SESSION_ID] = safe(input.sessionId);
    if (input.clientName) attributes[ATTR_USER_AGENT_NAME] = safe(input.clientName);
    if (input.clientVersion) attributes[ATTR_USER_AGENT_VERSION] = safe(input.clientVersion);
    if (input.userAgent) attributes[ATTR_USER_AGENT_ORIGINAL] = safe(input.userAgent);
}

function addQylAttributes(
    attributes: Attributes,
    input: McpOperationInput,
    safe: (value: string) => string,
): void {
    if (input.serverId) attributes[ATTR_QYL_MCP_SERVER_ID] = safe(input.serverId);
    if (input.executionId) attributes[ATTR_QYL_MCP_EXECUTION_ID] = safe(input.executionId);
}

function resourceMethodHasUri(method: string): boolean {
    return method === "resources/read"
        || method === "resources/subscribe"
        || method === "resources/unsubscribe"
        || method === "notifications/resources/updated";
}

function networkTransport(transport: McpTelemetryTransport): string | undefined {
    if (transport === "stdio") return NETWORK_TRANSPORT_VALUE_PIPE;
    if (transport === "streamable_http") return NETWORK_TRANSPORT_VALUE_TCP;
    return undefined;
}

function inferredNetworkProtocol(transport: McpTelemetryTransport): string | undefined {
    return transport === "streamable_http" ? "http" : undefined;
}

function safeText(redactor: SecretRedactor): (value: string) => string {
    return (value) => bounded(redactor.redactText(value));
}

function requireText(value: string, name: string, safe: (value: string) => string): string {
    if (value.trim().length === 0) throw new Error(`${name} must not be empty.`);
    return safe(value);
}

function bounded(value: string): string {
    return value.length <= ATTRIBUTE_VALUE_LIMIT
        ? value
        : `${value.slice(0, ATTRIBUTE_VALUE_LIMIT - 1)}…`;
}

function boundedLogBody(value: unknown, redactor: SecretRedactor): LogBody {
    const redacted = redactor.redact(value);
    const serialized = JSON.stringify(redacted);
    if (serialized === undefined) return String(redacted);
    if (serialized.length <= LOG_BODY_CHARACTER_LIMIT) {
        return JSON.parse(serialized) as LogBody;
    }
    return {
        truncated: true,
        original_characters: serialized.length,
        preview: `${serialized.slice(0, LOG_BODY_CHARACTER_LIMIT - 1)}…`,
    };
}

function validateTiming(startTimeMs: number, endTimeMs: number): void {
    if (!Number.isFinite(startTimeMs)
        || !Number.isFinite(endTimeMs)
        || endTimeMs < startTimeMs) {
        throw new Error("MCP telemetry timestamps must be finite and ordered.");
    }
}

function durationSeconds(startTimeMs: number, endTimeMs: number): number {
    return (endTimeMs - startTimeMs) / 1_000;
}
