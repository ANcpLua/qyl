import { parse } from "uri-template";
import type { PathUncheckedResponse } from "@typespec/ts-http-runtime";
import { ServicesApiClientContext } from "./servicesApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";
import { buildPagedAsyncIterator, type PagedAsyncIterableIterator } from "../../helpers/pagingHelpers.js";
import { jsonArrayServiceDependencyToApplicationTransform, jsonCursorPageToApplicationTransform_8 as jsonCursorPageToApplicationTransform, jsonCursorPageToApplicationTransform_9 as jsonCursorPageToApplicationTransform_2, jsonServiceDetailsToApplicationTransform } from "../../models/internal/serializers.js";
import { OperationInfo, type ServiceDependency, type ServiceDetails, ServiceInfo } from "../../models/models.js";

export interface ListOptions extends OperationOptions {
  namespaceName?: string
  limit?: number
  cursor?: string
}
export interface ListPageSettings {}
export interface ListPageResponse {
  items: Array<ServiceInfo>
  nextCursor?: string
}
async function listSend(
  client: ServicesApiClientContext,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/services{?namespaceName,limit,cursor}").expand({
    ...(options?.namespaceName && {namespaceName: options.namespaceName}),
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
  client: ServicesApiClientContext,
  options?: ListOptions,
): PagedAsyncIterableIterator<ServiceInfo,ListPageResponse,ListPageSettings> {
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
  return buildPagedAsyncIterator<ServiceInfo, ListPageResponse, ListPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
export interface GetOptions extends OperationOptions {}
/**
 * Get service details
 *
 * @param {ServicesApiClientContext} client
 * @param {string} serviceName
 * @param {GetOptions} [options]
 */
export async function get(
  client: ServicesApiClientContext,
  serviceName: string,
  options?: GetOptions,
): Promise<ServiceDetails> {
  const path = parse("/api/v1/services/{serviceName}").expand({
    serviceName: serviceName
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonServiceDetailsToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetDependenciesOptions extends OperationOptions {}
/**
 * Get service dependencies
 *
 * @param {ServicesApiClientContext} client
 * @param {string} serviceName
 * @param {GetDependenciesOptions} [options]
 */
export async function getDependencies(
  client: ServicesApiClientContext,
  serviceName: string,
  options?: GetDependenciesOptions,
): Promise<Array<ServiceDependency>> {
  const path = parse("/api/v1/services/{serviceName}/dependencies").expand({
    serviceName: serviceName
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonArrayServiceDependencyToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetOperationsOptions extends OperationOptions {
  limit?: number
  cursor?: string
}
export interface GetOperationsPageSettings {}
export interface GetOperationsPageResponse {
  items: Array<OperationInfo>
  nextCursor?: string
}
async function getOperationsSend(
  client: ServicesApiClientContext,
  serviceName: string,
  options?: Record<string, any>,
) {
  const path = parse("/api/v1/services/{serviceName}/operations{?limit,cursor}").expand({
    serviceName: serviceName,
    limit: options?.limit ?? 100,
    ...(options?.cursor && {cursor: options.cursor})
  });
  const httpRequestOptions = {
    headers: {},
  };
  return await client.pathUnchecked(path).get(httpRequestOptions);;
}
function getOperationsDeserialize(
  response: PathUncheckedResponse,
  options?: GetOperationsOptions,
) {
  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonCursorPageToApplicationTransform_2(response.body)!;
  }
  throw createRestError(response);
}
export function getOperations(
  client: ServicesApiClientContext,
  serviceName: string,
  options?: GetOperationsOptions,
): PagedAsyncIterableIterator<OperationInfo,GetOperationsPageResponse,GetOperationsPageSettings> {
  function getElements(response: GetOperationsPageResponse) {
    return response.items;
  }
  async function getPagedResponse(
    nextToken?: string,
    settings?: GetOperationsPageSettings,
  ) {

            let response: PathUncheckedResponse;
            if (nextToken) {
              response = await client.pathUnchecked(nextToken).get();
            } else {
              const combinedOptions = { ...options, ...settings };
              response = await getOperationsSend(client, serviceName, combinedOptions);
            }
    return {
    pagedResponse: await getOperationsDeserialize(response, options),
    nextToken: response.body["nextCursor"],
    };
  }
  return buildPagedAsyncIterator<OperationInfo, GetOperationsPageResponse, GetOperationsPageSettings>({getElements, getPagedResponse: getPagedResponse as any});
}
