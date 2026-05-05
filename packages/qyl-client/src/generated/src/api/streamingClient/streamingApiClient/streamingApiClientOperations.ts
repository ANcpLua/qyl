import { parse } from "uri-template";
import { StreamingApiClientContext } from "./streamingApiClientContext.js";
import { createRestError } from "../../../helpers/error.js";
import type { OperationOptions } from "../../../helpers/interfaces.js";
import { jsonArrayStreamEventTypeToTransportTransform } from "../../../models/internal/serializers.js";
import type { StreamEventType } from "../../../models/models.js";

export interface StreamEventsOptions extends OperationOptions {
  types?: Array<StreamEventType>
  serviceName?: string
  sampleRate?: number
}
/**
 * Stream all telemetry events (SSE)
 *
 * @param {StreamingApiClientContext} client
 * @param {StreamEventsOptions} [options]
 */
export async function streamEvents(
  client: StreamingApiClientContext,
  options?: StreamEventsOptions,
): Promise<string> {
  const path = parse("/api/v1/stream/events{?types,serviceName,sampleRate}").expand({
    ...(options?.types && {types: jsonArrayStreamEventTypeToTransportTransform(options.types)}),
    ...(options?.serviceName && {serviceName: options.serviceName}),
    ...(options?.sampleRate && {sampleRate: options.sampleRate})
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("text/event-stream")) {
    return response.body!;
  }
  throw createRestError(response);
}
;
export interface StreamTracesOptions extends OperationOptions {
  serviceName?: string
  minDurationMs?: bigint
}
/**
 * Stream traces in real-time
 *
 * @param {StreamingApiClientContext} client
 * @param {StreamTracesOptions} [options]
 */
export async function streamTraces(
  client: StreamingApiClientContext,
  options?: StreamTracesOptions,
): Promise<string> {
  const path = parse("/api/v1/stream/traces{?serviceName,minDurationMs}").expand({
    ...(options?.serviceName && {serviceName: options.serviceName}),
    ...(options?.minDurationMs && {minDurationMs: options.minDurationMs})
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("text/event-stream")) {
    return response.body!;
  }
  throw createRestError(response);
}
;
export interface StreamTraceSpansOptions extends OperationOptions {}
/**
 * Stream spans for a specific trace
 *
 * @param {StreamingApiClientContext} client
 * @param {string} traceId
 * @param {StreamTraceSpansOptions} [options]
 */
export async function streamTraceSpans(
  client: StreamingApiClientContext,
  traceId: string,
  options?: StreamTraceSpansOptions,
): Promise<string> {
  const path = parse("/api/v1/stream/traces/{traceId}/spans").expand({
    traceId: traceId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("text/event-stream")) {
    return response.body!;
  }
  throw createRestError(response);
}
;
export interface StreamLogsOptions extends OperationOptions {
  serviceName?: string
  minSeverity?: number
  query?: string
}
/**
 * Stream logs in real-time
 *
 * @param {StreamingApiClientContext} client
 * @param {StreamLogsOptions} [options]
 */
export async function streamLogs(
  client: StreamingApiClientContext,
  options?: StreamLogsOptions,
): Promise<string> {
  const path = parse("/api/v1/stream/logs{?serviceName,minSeverity,query}").expand({
    ...(options?.serviceName && {serviceName: options.serviceName}),
    ...(options?.minSeverity && {minSeverity: options.minSeverity}),
    ...(options?.query && {query: options.query})
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("text/event-stream")) {
    return response.body!;
  }
  throw createRestError(response);
}
;
export interface StreamMetricsOptions extends OperationOptions {
  metricName?: string
  serviceName?: string
}
/**
 * Stream metrics in real-time
 *
 * @param {StreamingApiClientContext} client
 * @param {StreamMetricsOptions} [options]
 */
export async function streamMetrics(
  client: StreamingApiClientContext,
  options?: StreamMetricsOptions,
): Promise<string> {
  const path = parse("/api/v1/stream/metrics{?metricName,serviceName}").expand({
    ...(options?.metricName && {metricName: options.metricName}),
    ...(options?.serviceName && {serviceName: options.serviceName})
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("text/event-stream")) {
    return response.body!;
  }
  throw createRestError(response);
}
;
export interface StreamDeploymentsOptions extends OperationOptions {
  serviceName?: string
  environment?: string
}
/**
 * Stream deployment events
 *
 * @param {StreamingApiClientContext} client
 * @param {StreamDeploymentsOptions} [options]
 */
export async function streamDeployments(
  client: StreamingApiClientContext,
  options?: StreamDeploymentsOptions,
): Promise<string> {
  const path = parse("/api/v1/stream/deployments{?serviceName,environment}").expand({
    ...(options?.serviceName && {serviceName: options.serviceName}),
    ...(options?.environment && {environment: options.environment})
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("text/event-stream")) {
    return response.body!;
  }
  throw createRestError(response);
}
;
