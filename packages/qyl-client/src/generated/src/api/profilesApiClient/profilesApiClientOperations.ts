import { parse } from "uri-template";
import { ProfilesApiClientContext } from "./profilesApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";
import { jsonArrayProfileRecordToApplicationTransform, jsonProfileRecordToApplicationTransform } from "../../models/internal/serializers.js";
import type { ProfileRecord } from "../../models/models.js";

export interface ListOptions extends OperationOptions {
  sessionId?: string
  traceId?: string
  serviceName?: string
  sampleType?: string
  limit?: number
}
/**
 * List profiles with filtering
 *
 * @param {ProfilesApiClientContext} client
 * @param {ListOptions} [options]
 */
export async function list(
  client: ProfilesApiClientContext,
  options?: ListOptions,
): Promise<Array<ProfileRecord>> {
  const path = parse("/api/v1/profiles{?sessionId,traceId,serviceName,sampleType,limit}").expand({
    ...(options?.sessionId && {sessionId: options.sessionId}),
    ...(options?.traceId && {traceId: options.traceId}),
    ...(options?.serviceName && {serviceName: options.serviceName}),
    ...(options?.sampleType && {sampleType: options.sampleType}),
    limit: options?.limit ?? 100
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonArrayProfileRecordToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetOptions extends OperationOptions {}
/**
 * Get a specific profile by ID
 *
 * @param {ProfilesApiClientContext} client
 * @param {string} profileId
 * @param {GetOptions} [options]
 */
export async function get(
  client: ProfilesApiClientContext,
  profileId: string,
  options?: GetOptions,
): Promise<ProfileRecord> {
  const path = parse("/api/v1/profiles/{profileId}").expand({
    profileId: profileId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonProfileRecordToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetByTraceOptions extends OperationOptions {
  limit?: number
}
/**
 * Get profiles for a trace
 *
 * @param {ProfilesApiClientContext} client
 * @param {string} traceId
 * @param {GetByTraceOptions} [options]
 */
export async function getByTrace(
  client: ProfilesApiClientContext,
  traceId: string,
  options?: GetByTraceOptions,
): Promise<Array<ProfileRecord>> {
  const path = parse("/api/v1/profiles/by-trace/{traceId}{?limit}").expand({
    traceId: traceId,
    limit: options?.limit ?? 100
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonArrayProfileRecordToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetBySpanOptions extends OperationOptions {
  limit?: number
}
/**
 * Get profiles for a span
 *
 * @param {ProfilesApiClientContext} client
 * @param {string} spanId
 * @param {GetBySpanOptions} [options]
 */
export async function getBySpan(
  client: ProfilesApiClientContext,
  spanId: string,
  options?: GetBySpanOptions,
): Promise<Array<ProfileRecord>> {
  const path = parse("/api/v1/profiles/by-span/{spanId}{?limit}").expand({
    spanId: spanId,
    limit: options?.limit ?? 100
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonArrayProfileRecordToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
