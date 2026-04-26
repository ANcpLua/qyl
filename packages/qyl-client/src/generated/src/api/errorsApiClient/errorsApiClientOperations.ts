import {parse} from "uri-template";
import type {PathUncheckedResponse} from "@typespec/ts-http-runtime";
import {ErrorsApiClientContext} from "./errorsApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {buildPagedAsyncIterator, type PagedAsyncIterableIterator} from "../../helpers/pagingHelpers.js";
import {
    dateRfc3339Serializer,
    jsonCursorPageToApplicationTransform_6 as jsonCursorPageToApplicationTransform,
    jsonErrorCorrelationToApplicationTransform,
    jsonErrorEntityToApplicationTransform,
    jsonErrorStatsToApplicationTransform,
    jsonErrorUpdateToTransportTransform
} from "../../models/internal/serializers.js";
import {
    type ErrorCategory,
    type ErrorCorrelation,
    ErrorEntity,
    type ErrorStats,
    type ErrorStatus,
    ErrorUpdate
} from "../../models/models.js";

export interface ListOptions extends OperationOptions {
    serviceName?: string
    status?: ErrorStatus
    category?: ErrorCategory
    startTime?: Date
    endTime?: Date
    limit?: number
    cursor?: string
}

export interface ListPageSettings {
}

export interface ListPageResponse {
    items: Array<ErrorEntity>
    nextCursor?: string
}

async function listSend(
    client: ErrorsApiClientContext,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/errors{?serviceName,status,category,startTime,endTime,limit,cursor}").expand({
        ...(options?.serviceName && {serviceName: options.serviceName}),
        ...(options?.status && {status: options.status}),
        ...(options?.category && {category: options.category}),
        ...(options?.startTime && {startTime: dateRfc3339Serializer(options.startTime)}),
        ...(options?.endTime && {endTime: dateRfc3339Serializer(options.endTime)}),
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
    client: ErrorsApiClientContext,
    options?: ListOptions,
): PagedAsyncIterableIterator<ErrorEntity, ListPageResponse, ListPageSettings> {
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

    return buildPagedAsyncIterator<ErrorEntity, ListPageResponse, ListPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface GetOptions extends OperationOptions {
}

/**
 * Get error by ID
 *
 * @param {ErrorsApiClientContext} client
 * @param {string} errorId
 * @param {GetOptions} [options]
 */
export async function get(
    client: ErrorsApiClientContext,
    errorId: string,
    options?: GetOptions,
): Promise<ErrorEntity> {
    const path = parse("/api/v1/errors/{errorId}").expand({
        errorId: errorId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonErrorEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface UpdateOptions extends OperationOptions {
}

/**
 * Update error status
 *
 * @param {ErrorsApiClientContext} client
 * @param {string} errorId
 * @param {ErrorUpdate} update
 * @param {UpdateOptions} [options]
 */
export async function update(
    client: ErrorsApiClientContext,
    errorId: string,
    update: ErrorUpdate,
    options?: UpdateOptions,
): Promise<ErrorEntity> {
    const path = parse("/api/v1/errors/{errorId}").expand({
        errorId: errorId
    });
    const httpRequestOptions = {
        headers: {}, body: jsonErrorUpdateToTransportTransform(update),
    };
    const response = await client.pathUnchecked(path).patch(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonErrorEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetStatsOptions extends OperationOptions {
    serviceName?: string
    startTime?: Date
    endTime?: Date
}

/**
 * Get error statistics
 *
 * @param {ErrorsApiClientContext} client
 * @param {GetStatsOptions} [options]
 */
export async function getStats(
    client: ErrorsApiClientContext,
    options?: GetStatsOptions,
): Promise<ErrorStats> {
    const path = parse("/api/v1/errors/stats{?serviceName,startTime,endTime}").expand({
        ...(options?.serviceName && {serviceName: options.serviceName}),
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
        return jsonErrorStatsToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetCorrelationsOptions extends OperationOptions {
}

/**
 * Get error correlations
 *
 * @param {ErrorsApiClientContext} client
 * @param {string} errorId
 * @param {GetCorrelationsOptions} [options]
 */
export async function getCorrelations(
    client: ErrorsApiClientContext,
    errorId: string,
    options?: GetCorrelationsOptions,
): Promise<ErrorCorrelation> {
    const path = parse("/api/v1/errors/{errorId}/correlations").expand({
        errorId: errorId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonErrorCorrelationToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

