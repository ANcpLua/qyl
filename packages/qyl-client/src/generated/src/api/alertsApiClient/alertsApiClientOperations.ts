import { parse } from "uri-template";
import type { PathUncheckedResponse } from "@typespec/ts-http-runtime";
import { AlertsApiClientContext } from "./alertsApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";
import { buildPagedAsyncIterator, type PagedAsyncIterableIterator } from "../../helpers/pagingHelpers.js";
import { dateRfc3339Serializer, jsonAlertFiringAcknowledgementToTransportTransform, jsonAlertFiringEntityToApplicationTransform, jsonAlertRuleEntityToApplicationTransform, jsonAlertRuleEntityToTransportTransform, jsonCursorPageToApplicationTransform_16 as jsonCursorPageToApplicationTransform, jsonCursorPageToApplicationTransform_17 as jsonCursorPageToApplicationTransform_2, jsonCursorPageToApplicationTransform_18 as jsonCursorPageToApplicationTransform_3, jsonFixRunEntityToApplicationTransform } from "../../models/internal/serializers.js";
import { AlertFiringAcknowledgement, AlertFiringEntity, type AlertFiringStatus, AlertRuleEntity, FixRunEntity, type FixRunStatus } from "../../models/models.js";

export interface ListRulesOptions extends OperationOptions {
  projectId?: string
  enabled?: boolean
  limit?: number
  cursor?: string
}
export interface ListRulesPageSettings {}
export interface ListRulesPageResponse {
  items: Array<AlertRuleEntity>
  nextCursor?: string
}
async function listRulesSend(
  client: AlertsApiClientContext,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/alerts/rules{?projectId,enabled,limit,cursor}").expand({
    ...(options?.projectId && {projectId: options.projectId}),
    ...(options?.enabled && {enabled: options.enabled}),
    limit: options?.limit ?? 20,
    ...(options?.cursor && {cursor: options.cursor})
  });
  const httpRequestOptions = {
    headers: {},
  };
  return await client.pathUnchecked(path).get(httpRequestOptions);;
}
function listRulesDeserialize(
  response: PathUncheckedResponse,
  options?: ListRulesOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
export function listRules(
  client: AlertsApiClientContext,
  options?: ListRulesOptions,
): PagedAsyncIterableIterator<AlertRuleEntity,ListRulesPageResponse,ListRulesPageSettings> {
  function getElements(response: ListRulesPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: ListRulesPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await listRulesSend(client, combinedOptions);
            }
    return {
    pagedResponse: await listRulesDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<AlertRuleEntity, ListRulesPageResponse, ListRulesPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetRuleOptions extends OperationOptions {}
/**
 * Get alert rule by ID
 *
 * @param {AlertsApiClientContext} client
 * @param {string} ruleId
 * @param {GetRuleOptions} [options]
 */
export async function getRule(
  client: AlertsApiClientContext,
  ruleId: string,
  options?: GetRuleOptions,
): Promise<AlertRuleEntity> {
  const path = parse("/api/v1/alerts/rules/{ruleId}").expand({
    ruleId: ruleId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonAlertRuleEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface CreateRuleOptions extends OperationOptions {}
/**
 * Create a new alert rule
 *
 * @param {AlertsApiClientContext} client
 * @param {AlertRuleEntity} rule
 * @param {CreateRuleOptions} [options]
 */
export async function createRule(
  client: AlertsApiClientContext,
  rule: AlertRuleEntity,
  options?: CreateRuleOptions,
): Promise<AlertRuleEntity> {
  const path = parse("/api/v1/alerts/rules").expand({});
  const httpRequestOptions = {
    headers: {},body: jsonAlertRuleEntityToTransportTransform(rule),
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonAlertRuleEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface UpdateRuleOptions extends OperationOptions {}
/**
 * Update an existing alert rule
 *
 * @param {AlertsApiClientContext} client
 * @param {string} ruleId
 * @param {AlertRuleEntity} rule
 * @param {UpdateRuleOptions} [options]
 */
export async function updateRule(
  client: AlertsApiClientContext,
  ruleId: string,
  rule: AlertRuleEntity,
  options?: UpdateRuleOptions,
): Promise<AlertRuleEntity> {
  const path = parse("/api/v1/alerts/rules/{ruleId}").expand({
    ruleId: ruleId
  });
  const httpRequestOptions = {
    headers: {},body: jsonAlertRuleEntityToTransportTransform(rule),
  };
  const response = await client.pathUnchecked(path).put(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonAlertRuleEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface DeleteRuleOptions extends OperationOptions {}
/**
 * Delete an alert rule
 *
 * @param {AlertsApiClientContext} client
 * @param {string} ruleId
 * @param {DeleteRuleOptions} [options]
 */
export async function deleteRule(
  client: AlertsApiClientContext,
  ruleId: string,
  options?: DeleteRuleOptions,
): Promise<void> {
  const path = parse("/api/v1/alerts/rules/{ruleId}").expand({
    ruleId: ruleId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).delete(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 204 && !response.body) {
    return;
  }
  throw createRestError(response);
}
;
export interface ListFiringsOptions extends OperationOptions {
  ruleId?: string
  status?: AlertFiringStatus
  startTime?: Date
  endTime?: Date
  limit?: number
  cursor?: string
}
export interface ListFiringsPageSettings {}
export interface ListFiringsPageResponse {
  items: Array<AlertFiringEntity>
  nextCursor?: string
}
async function listFiringsSend(
  client: AlertsApiClientContext,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/alerts/firings{?ruleId,status,startTime,endTime,limit,cursor}").expand({
    ...(options?.ruleId && {ruleId: options.ruleId}),
    ...(options?.status && {status: options.status}),
    ...(options?.startTime && {startTime: dateRfc3339Serializer(options.startTime)}),
    ...(options?.endTime && {endTime: dateRfc3339Serializer(options.endTime)}),
    limit: options?.limit ?? 20,
    ...(options?.cursor && {cursor: options.cursor})
  });
  const httpRequestOptions = {
    headers: {},
  };
  return await client.pathUnchecked(path).get(httpRequestOptions);;
}
function listFiringsDeserialize(
  response: PathUncheckedResponse,
  options?: ListFiringsOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform_2(response.body)!;
  }
  throw createRestError(response);
}
export function listFirings(
  client: AlertsApiClientContext,
  options?: ListFiringsOptions,
): PagedAsyncIterableIterator<AlertFiringEntity,ListFiringsPageResponse,ListFiringsPageSettings> {
  function getElements(response: ListFiringsPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: ListFiringsPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await listFiringsSend(client, combinedOptions);
            }
    return {
    pagedResponse: await listFiringsDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<AlertFiringEntity, ListFiringsPageResponse, ListFiringsPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface AcknowledgeFiringOptions extends OperationOptions {}
/**
 * Acknowledge an alert firing
 *
 * @param {AlertsApiClientContext} client
 * @param {string} firingId
 * @param {AlertFiringAcknowledgement} acknowledgement
 * @param {AcknowledgeFiringOptions} [options]
 */
export async function acknowledgeFiring(
  client: AlertsApiClientContext,
  firingId: string,
  acknowledgement: AlertFiringAcknowledgement,
  options?: AcknowledgeFiringOptions,
): Promise<AlertFiringEntity> {
  const path = parse("/api/v1/alerts/firings/{firingId}/acknowledge").expand({
    firingId: firingId
  });
  const httpRequestOptions = {
    headers: {

    },body: jsonAlertFiringAcknowledgementToTransportTransform(acknowledgement),
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonAlertFiringEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface ResolveFiringOptions extends OperationOptions {}
/**
 * Resolve an alert firing
 *
 * @param {AlertsApiClientContext} client
 * @param {string} firingId
 * @param {ResolveFiringOptions} [options]
 */
export async function resolveFiring(
  client: AlertsApiClientContext,
  firingId: string,
  options?: ResolveFiringOptions,
): Promise<AlertFiringEntity> {
  const path = parse("/api/v1/alerts/firings/{firingId}/resolve").expand({
    firingId: firingId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonAlertFiringEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface ListFixRunsOptions extends OperationOptions {
  issueId?: string
  status?: FixRunStatus
  limit?: number
  cursor?: string
}
export interface ListFixRunsPageSettings {}
export interface ListFixRunsPageResponse {
  items: Array<FixRunEntity>
  nextCursor?: string
}
async function listFixRunsSend(
  client: AlertsApiClientContext,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/alerts/fixes{?issueId,status,limit,cursor}").expand({
    ...(options?.issueId && {issueId: options.issueId}),
    ...(options?.status && {status: options.status}),
    limit: options?.limit ?? 20,
    ...(options?.cursor && {cursor: options.cursor})
  });
  const httpRequestOptions = {
    headers: {},
  };
  return await client.pathUnchecked(path).get(httpRequestOptions);;
}
function listFixRunsDeserialize(
  response: PathUncheckedResponse,
  options?: ListFixRunsOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform_3(response.body)!;
  }
  throw createRestError(response);
}
export function listFixRuns(
  client: AlertsApiClientContext,
  options?: ListFixRunsOptions,
): PagedAsyncIterableIterator<FixRunEntity,ListFixRunsPageResponse,ListFixRunsPageSettings> {
  function getElements(response: ListFixRunsPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: ListFixRunsPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await listFixRunsSend(client, combinedOptions);
            }
    return {
    pagedResponse: await listFixRunsDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<FixRunEntity, ListFixRunsPageResponse, ListFixRunsPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetFixRunOptions extends OperationOptions {}
/**
 * Get fix run by ID
 *
 * @param {AlertsApiClientContext} client
 * @param {string} fixId
 * @param {GetFixRunOptions} [options]
 */
export async function getFixRun(
  client: AlertsApiClientContext,
  fixId: string,
  options?: GetFixRunOptions,
): Promise<FixRunEntity> {
  const path = parse("/api/v1/alerts/fixes/{fixId}").expand({
    fixId: fixId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonFixRunEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
