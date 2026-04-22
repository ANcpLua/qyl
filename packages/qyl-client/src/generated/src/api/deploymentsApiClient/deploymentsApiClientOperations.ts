import { parse } from "uri-template";
import type { PathUncheckedResponse } from "@typespec/ts-http-runtime";
import { DeploymentsApiClientContext } from "./deploymentsApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";
import { buildPagedAsyncIterator, type PagedAsyncIterableIterator } from "../../helpers/pagingHelpers.js";
import { dateRfc3339Serializer, jsonCursorPageToApplicationTransform_7 as jsonCursorPageToApplicationTransform, jsonDeploymentCreateToTransportTransform, jsonDeploymentEntityToApplicationTransform, jsonDeploymentUpdateToTransportTransform, jsonDoraMetricsToApplicationTransform } from "../../models/internal/serializers.js";
import { DeploymentCreate, DeploymentEntity, type DeploymentEnvironment, type DeploymentStatus, DeploymentUpdate, type DoraMetrics } from "../../models/models.js";

export interface ListOptions extends OperationOptions {
  serviceName?: string
  environment?: DeploymentEnvironment
  status?: DeploymentStatus
  startTime?: Date
  endTime?: Date
  limit?: number
  cursor?: string
}
export interface ListPageSettings {}
export interface ListPageResponse {
  items: Array<DeploymentEntity>
  nextCursor?: string
}
async function listSend(
  client: DeploymentsApiClientContext,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/deployments{?serviceName,environment,status,startTime,endTime,limit,cursor}").expand({
    ...(options?.serviceName && {serviceName: options.serviceName}),
    ...(options?.environment && {environment: options.environment}),
    ...(options?.status && {status: options.status}),
    ...(options?.startTime && {startTime: dateRfc3339Serializer(options.startTime)}),
    ...(options?.endTime && {endTime: dateRfc3339Serializer(options.endTime)}),
    limit: options?.limit ?? 100,
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
  client: DeploymentsApiClientContext,
  options?: ListOptions,
): PagedAsyncIterableIterator<DeploymentEntity,ListPageResponse,ListPageSettings> {
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
  return buildPagedAsyncIterator<DeploymentEntity, ListPageResponse, ListPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetOptions extends OperationOptions {}
/**
 * Get deployment by ID
 *
 * @param {DeploymentsApiClientContext} client
 * @param {string} deploymentId
 * @param {GetOptions} [options]
 */
export async function get(
  client: DeploymentsApiClientContext,
  deploymentId: string,
  options?: GetOptions,
): Promise<DeploymentEntity> {
  const path = parse("/api/v1/deployments/{deploymentId}").expand({
    deploymentId: deploymentId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonDeploymentEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface CreateOptions extends OperationOptions {}
/**
 * Record new deployment
 *
 * @param {DeploymentsApiClientContext} client
 * @param {DeploymentCreate} deployment
 * @param {CreateOptions} [options]
 */
export async function create(
  client: DeploymentsApiClientContext,
  deployment: DeploymentCreate,
  options?: CreateOptions,
): Promise<DeploymentEntity> {
  const path = parse("/api/v1/deployments").expand({});
  const httpRequestOptions = {
    headers: {},body: jsonDeploymentCreateToTransportTransform(deployment),
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonDeploymentEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface UpdateOptions extends OperationOptions {}
/**
 * Update deployment status
 *
 * @param {DeploymentsApiClientContext} client
 * @param {string} deploymentId
 * @param {DeploymentUpdate} update
 * @param {UpdateOptions} [options]
 */
export async function update(
  client: DeploymentsApiClientContext,
  deploymentId: string,
  update: DeploymentUpdate,
  options?: UpdateOptions,
): Promise<DeploymentEntity> {
  const path = parse("/api/v1/deployments/{deploymentId}").expand({
    deploymentId: deploymentId
  });
  const httpRequestOptions = {
    headers: {},body: jsonDeploymentUpdateToTransportTransform(update),
  };
  const response = await client.pathUnchecked(path).patch(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonDeploymentEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetDoraMetricsOptions extends OperationOptions {
  serviceName?: string
  environment?: DeploymentEnvironment
  startTime?: Date
  endTime?: Date
}
/**
 * Get DORA metrics
 *
 * @param {DeploymentsApiClientContext} client
 * @param {GetDoraMetricsOptions} [options]
 */
export async function getDoraMetrics(
  client: DeploymentsApiClientContext,
  options?: GetDoraMetricsOptions,
): Promise<DoraMetrics> {
  const path = parse("/api/v1/deployments/metrics/dora{?serviceName,environment,startTime,endTime}").expand({
    ...(options?.serviceName && {serviceName: options.serviceName}),
    ...(options?.environment && {environment: options.environment}),
    ...(options?.startTime && {startTime: dateRfc3339Serializer(options.startTime)}),
    ...(options?.endTime && {endTime: dateRfc3339Serializer(options.endTime)})
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonDoraMetricsToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
