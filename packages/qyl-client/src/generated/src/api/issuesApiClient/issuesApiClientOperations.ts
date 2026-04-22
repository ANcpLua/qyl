import { parse } from "uri-template";
import type { PathUncheckedResponse } from "@typespec/ts-http-runtime";
import { IssuesApiClientContext } from "./issuesApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";
import { buildPagedAsyncIterator, type PagedAsyncIterableIterator } from "../../helpers/pagingHelpers.js";
import { dateRfc3339Serializer, jsonArrayErrorBreadcrumbEntityToApplicationTransform, jsonCursorPageToApplicationTransform_12 as jsonCursorPageToApplicationTransform, jsonCursorPageToApplicationTransform_13 as jsonCursorPageToApplicationTransform_2, jsonErrorIssueEntityToApplicationTransform, jsonIssueUpdateRequestToTransportTransform } from "../../models/internal/serializers.js";
import { type ErrorBreadcrumbEntity, ErrorIssueEntity, ErrorIssueEventEntity, type IssueLevel, type IssuePriority, type IssueStatus, IssueUpdateRequest } from "../../models/models.js";

export interface ListOptions extends OperationOptions {
  projectId?: string
  status?: IssueStatus
  priority?: IssuePriority
  level?: IssueLevel
  startTime?: Date
  endTime?: Date
  limit?: number
  cursor?: string
}
export interface ListPageSettings {}
export interface ListPageResponse {
  items: Array<ErrorIssueEntity>
  nextCursor?: string
}
async function listSend(
  client: IssuesApiClientContext,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/issues{?projectId,status,priority,level,startTime,endTime,limit,cursor}").expand({
    ...(options?.projectId && {projectId: options.projectId}),
    ...(options?.status && {status: options.status}),
    ...(options?.priority && {priority: options.priority}),
    ...(options?.level && {level: options.level}),
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
function listDeserialize(
  response: PathUncheckedResponse,
  options?: ListOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
export function list(
  client: IssuesApiClientContext,
  options?: ListOptions,
): PagedAsyncIterableIterator<ErrorIssueEntity,ListPageResponse,ListPageSettings> {
  function getElements(response: ListPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: ListPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await listSend(client, combinedOptions);
            }
    return {
    pagedResponse: await listDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<ErrorIssueEntity, ListPageResponse, ListPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetOptions extends OperationOptions {}
/**
 * Get issue by ID
 *
 * @param {IssuesApiClientContext} client
 * @param {string} issueId
 * @param {GetOptions} [options]
 */
export async function get(
  client: IssuesApiClientContext,
  issueId: string,
  options?: GetOptions,
): Promise<ErrorIssueEntity> {
  const path = parse("/api/v1/issues/{issueId}").expand({
    issueId: issueId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonErrorIssueEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface UpdateOptions extends OperationOptions {}
/**
 * Update issue status
 *
 * @param {IssuesApiClientContext} client
 * @param {string} issueId
 * @param {IssueUpdateRequest} update
 * @param {UpdateOptions} [options]
 */
export async function update(
  client: IssuesApiClientContext,
  issueId: string,
  update: IssueUpdateRequest,
  options?: UpdateOptions,
): Promise<ErrorIssueEntity> {
  const path = parse("/api/v1/issues/{issueId}").expand({
    issueId: issueId
  });
  const httpRequestOptions = {
    headers: {},body: jsonIssueUpdateRequestToTransportTransform(update),
  };
  const response = await client.pathUnchecked(path).patch(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonErrorIssueEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetEventsOptions extends OperationOptions {
  limit?: number
  cursor?: string
}
export interface GetEventsPageSettings {}
export interface GetEventsPageResponse {
  items: Array<ErrorIssueEventEntity>
  nextCursor?: string
}
async function getEventsSend(
  client: IssuesApiClientContext,
  issueId: string,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/issues/{issueId}/events{?limit,cursor}").expand({
    issueId: issueId,
    limit: options?.limit ?? 20,
    ...(options?.cursor && {cursor: options.cursor})
  });
  const httpRequestOptions = {
    headers: {},
  };
  return await client.pathUnchecked(path).get(httpRequestOptions);;
}
function getEventsDeserialize(
  response: PathUncheckedResponse,
  options?: GetEventsOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform_2(response.body)!;
  }
  throw createRestError(response);
}
export function getEvents(
  client: IssuesApiClientContext,
  issueId: string,
  options?: GetEventsOptions,
): PagedAsyncIterableIterator<ErrorIssueEventEntity,GetEventsPageResponse,GetEventsPageSettings> {
  function getElements(response: GetEventsPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: GetEventsPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await getEventsSend(client, issueId, combinedOptions);
            }
    return {
    pagedResponse: await getEventsDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<ErrorIssueEventEntity, GetEventsPageResponse, GetEventsPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetBreadcrumbsOptions extends OperationOptions {
  limit?: number
}
/**
 * Get issue breadcrumbs
 *
 * @param {IssuesApiClientContext} client
 * @param {string} issueId
 * @param {GetBreadcrumbsOptions} [options]
 */
export async function getBreadcrumbs(
  client: IssuesApiClientContext,
  issueId: string,
  options?: GetBreadcrumbsOptions,
): Promise<Array<ErrorBreadcrumbEntity>> {
  const path = parse("/api/v1/issues/{issueId}/breadcrumbs{?limit}").expand({
    issueId: issueId,
    limit: options?.limit ?? 20
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonArrayErrorBreadcrumbEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
