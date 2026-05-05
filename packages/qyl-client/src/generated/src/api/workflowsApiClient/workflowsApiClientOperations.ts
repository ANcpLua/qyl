import { parse } from "uri-template";
import type { PathUncheckedResponse } from "@typespec/ts-http-runtime";
import { WorkflowsApiClientContext } from "./workflowsApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";
import { buildPagedAsyncIterator, type PagedAsyncIterableIterator } from "../../helpers/pagingHelpers.js";
import { dateRfc3339Serializer, jsonArrayWorkflowEventEntityToApplicationTransform, jsonCursorPageToApplicationTransform_14 as jsonCursorPageToApplicationTransform, jsonCursorPageToApplicationTransform_15 as jsonCursorPageToApplicationTransform_2, jsonWorkflowNodeEntityToApplicationTransform, jsonWorkflowRunEntityToApplicationTransform } from "../../models/internal/serializers.js";
import { type WorkflowEventEntity, WorkflowNodeEntity, WorkflowRunEntity, type WorkflowRunStatus } from "../../models/models.js";

export interface ListRunsOptions extends OperationOptions {
  projectId?: string
  workflowId?: string
  status?: WorkflowRunStatus
  startTime?: Date
  endTime?: Date
  limit?: number
  cursor?: string
}
export interface ListRunsPageSettings {}
export interface ListRunsPageResponse {
  items: Array<WorkflowRunEntity>
  nextCursor?: string
}
async function listRunsSend(
  client: WorkflowsApiClientContext,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/workflows/runs{?projectId,workflowId,status,startTime,endTime,limit,cursor}").expand({
    ...(options?.projectId && {projectId: options.projectId}),
    ...(options?.workflowId && {workflowId: options.workflowId}),
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
function listRunsDeserialize(
  response: PathUncheckedResponse,
  options?: ListRunsOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
export function listRuns(
  client: WorkflowsApiClientContext,
  options?: ListRunsOptions,
): PagedAsyncIterableIterator<WorkflowRunEntity,ListRunsPageResponse,ListRunsPageSettings> {
  function getElements(response: ListRunsPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: ListRunsPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await listRunsSend(client, combinedOptions);
            }
    return {
    pagedResponse: await listRunsDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<WorkflowRunEntity, ListRunsPageResponse, ListRunsPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetRunOptions extends OperationOptions {}
/**
 * Get workflow run by ID
 *
 * @param {WorkflowsApiClientContext} client
 * @param {string} runId
 * @param {GetRunOptions} [options]
 */
export async function getRun(
  client: WorkflowsApiClientContext,
  runId: string,
  options?: GetRunOptions,
): Promise<WorkflowRunEntity> {
  const path = parse("/api/v1/workflows/runs/{runId}").expand({
    runId: runId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonWorkflowRunEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetRunNodesOptions extends OperationOptions {
  limit?: number
  cursor?: string
}
export interface GetRunNodesPageSettings {}
export interface GetRunNodesPageResponse {
  items: Array<WorkflowNodeEntity>
  nextCursor?: string
}
async function getRunNodesSend(
  client: WorkflowsApiClientContext,
  runId: string,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/workflows/runs/{runId}/nodes{?limit,cursor}").expand({
    runId: runId,
    limit: options?.limit ?? 50,
    ...(options?.cursor && {cursor: options.cursor})
  });
  const httpRequestOptions = {
    headers: {},
  };
  return await client.pathUnchecked(path).get(httpRequestOptions);;
}
function getRunNodesDeserialize(
  response: PathUncheckedResponse,
  options?: GetRunNodesOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform_2(response.body)!;
  }
  throw createRestError(response);
}
export function getRunNodes(
  client: WorkflowsApiClientContext,
  runId: string,
  options?: GetRunNodesOptions,
): PagedAsyncIterableIterator<WorkflowNodeEntity,GetRunNodesPageResponse,GetRunNodesPageSettings> {
  function getElements(response: GetRunNodesPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: GetRunNodesPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await getRunNodesSend(client, runId, combinedOptions);
            }
    return {
    pagedResponse: await getRunNodesDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<WorkflowNodeEntity, GetRunNodesPageResponse, GetRunNodesPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetRunEventsOptions extends OperationOptions {
  afterSequence?: bigint
  limit?: number
}
/**
 * Get workflow events
 *
 * @param {WorkflowsApiClientContext} client
 * @param {string} runId
 * @param {GetRunEventsOptions} [options]
 */
export async function getRunEvents(
  client: WorkflowsApiClientContext,
  runId: string,
  options?: GetRunEventsOptions,
): Promise<Array<WorkflowEventEntity>> {
  const path = parse("/api/v1/workflows/runs/{runId}/events{?afterSequence,limit}").expand({
    runId: runId,
    ...(options?.afterSequence && {afterSequence: options.afterSequence}),
    limit: options?.limit ?? 50
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonArrayWorkflowEventEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface ResumeRunOptions extends OperationOptions {}
/**
 * Resume a paused workflow
 *
 * @param {WorkflowsApiClientContext} client
 * @param {string} runId
 * @param {ResumeRunOptions} [options]
 */
export async function resumeRun(
  client: WorkflowsApiClientContext,
  runId: string,
  options?: ResumeRunOptions,
): Promise<WorkflowRunEntity> {
  const path = parse("/api/v1/workflows/runs/{runId}/resume").expand({
    runId: runId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonWorkflowRunEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface ApproveStepOptions extends OperationOptions {}
/**
 * Approve a pending workflow step
 *
 * @param {WorkflowsApiClientContext} client
 * @param {string} runId
 * @param {string} nodeId
 * @param {ApproveStepOptions} [options]
 */
export async function approveStep(
  client: WorkflowsApiClientContext,
  runId: string,
  nodeId: string,
  options?: ApproveStepOptions,
): Promise<WorkflowNodeEntity> {
  const path = parse("/api/v1/workflows/runs/{runId}/nodes/{nodeId}/approve").expand({
    runId: runId,
    nodeId: nodeId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonWorkflowNodeEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
