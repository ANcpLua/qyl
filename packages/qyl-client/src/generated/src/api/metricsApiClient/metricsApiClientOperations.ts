import {parse} from "uri-template";
import type {PathUncheckedResponse} from "@typespec/ts-http-runtime";
import {MetricsApiClientContext} from "./metricsApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {buildPagedAsyncIterator, type PagedAsyncIterableIterator} from "../../helpers/pagingHelpers.js";
import {
    jsonCursorPageToApplicationTransform_4 as jsonCursorPageToApplicationTransform,
    jsonMetricMetadataToApplicationTransform,
    jsonMetricQueryRequestToTransportTransform,
    jsonMetricQueryResponseToApplicationTransform
} from "../../models/internal/serializers.js";
import {MetricMetadata, MetricQueryRequest, type MetricQueryResponse} from "../../models/models.js";

export interface ListOptions extends OperationOptions {
    serviceName?: string
    namePattern?: string
    limit?: number
    cursor?: string
}

export interface ListPageSettings {
}

export interface ListPageResponse {
    items: Array<MetricMetadata>
    nextCursor?: string
}

async function listSend(
    client: MetricsApiClientContext,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/metrics{?serviceName,namePattern,limit,cursor}").expand({
        ...(options?.serviceName && {serviceName: options.serviceName}),
        ...(options?.namePattern && {namePattern: options.namePattern}),
        limit: options?.limit ?? 100,
        ...(options?.cursor && {cursor: options.cursor})
    });
    const httpRequestOptions = {
        headers: {},
    };
    return await client.pathUnchecked(path).get(httpRequestOptions);
}

function listDeserialize(
    response: PathUncheckedResponse,
    options?: ListOptions,
) {
    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonCursorPageToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export function list(
    client: MetricsApiClientContext,
    options?: ListOptions,
): PagedAsyncIterableIterator<MetricMetadata, ListPageResponse, ListPageSettings> {
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
            const combinedOptions = {...options, ...settings};
            response = await listSend(client, combinedOptions);
        }
        return {
            pagedResponse: await listDeserialize(response, options),
            nextToken: response.body["nextCursor"],
        };
    }

    return buildPagedAsyncIterator<MetricMetadata, ListPageResponse, ListPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface QueryOptions extends OperationOptions {
}

/**
 * Query metric data points
 *
 * @param {MetricsApiClientContext} client
 * @param {MetricQueryRequest} request
 * @param {QueryOptions} [options]
 */
export async function query(
    client: MetricsApiClientContext,
    request: MetricQueryRequest,
    options?: QueryOptions,
): Promise<MetricQueryResponse> {
    const path = parse("/api/v1/metrics/query").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonMetricQueryRequestToTransportTransform(request),
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonMetricQueryResponseToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetMetadataOptions extends OperationOptions {
}

/**
 * Get metric metadata
 *
 * @param {MetricsApiClientContext} client
 * @param {string} metricName
 * @param {GetMetadataOptions} [options]
 */
export async function getMetadata(
    client: MetricsApiClientContext,
    metricName: string,
    options?: GetMetadataOptions,
): Promise<MetricMetadata> {
    const path = parse("/api/v1/metrics/{metricName}").expand({
        metricName: metricName
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonMetricMetadataToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

