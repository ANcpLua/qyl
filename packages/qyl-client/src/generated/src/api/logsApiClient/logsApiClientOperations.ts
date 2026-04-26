import {parse} from "uri-template";
import type {PathUncheckedResponse} from "@typespec/ts-http-runtime";
import {LogsApiClientContext} from "./logsApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {buildPagedAsyncIterator, type PagedAsyncIterableIterator} from "../../helpers/pagingHelpers.js";
import {
    dateRfc3339Serializer,
    jsonArrayLogPatternToApplicationTransform,
    jsonCursorPageToApplicationTransform_3 as jsonCursorPageToApplicationTransform,
    jsonLogAggregationRequestToTransportTransform,
    jsonLogAggregationResponseToApplicationTransform,
    jsonLogQueryToTransportTransform,
    jsonLogStatsToApplicationTransform
} from "../../models/internal/serializers.js";
import {
    LogAggregationRequest,
    type LogAggregationResponse,
    type LogOrderBy,
    type LogPattern,
    type LogQuery,
    LogRecord,
    type LogStats,
    type SeverityNumber
} from "../../models/models.js";

export interface ListOptions extends OperationOptions {
    serviceName?: string
    severityMin?: SeverityNumber
    severityMax?: SeverityNumber
    traceId?: string
    startTime?: Date
    endTime?: Date
    query?: string
    limit?: number
    cursor?: string
    orderBy?: LogOrderBy
}

export interface ListPageSettings {
}

export interface ListPageResponse {
    items: Array<LogRecord>
    nextCursor?: string
}

async function listSend(
    client: LogsApiClientContext,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/logs{?serviceName,severityMin,severityMax,traceId,startTime,endTime,query,limit,cursor,orderBy}").expand({
        ...(options?.serviceName && {serviceName: options.serviceName}),
        ...(options?.severityMin && {severityMin: options.severityMin}),
        ...(options?.severityMax && {severityMax: options.severityMax}),
        ...(options?.traceId && {traceId: options.traceId}),
        ...(options?.startTime && {startTime: dateRfc3339Serializer(options.startTime)}),
        ...(options?.endTime && {endTime: dateRfc3339Serializer(options.endTime)}),
        ...(options?.query && {query: options.query}),
        limit: options?.limit ?? 100,
        ...(options?.cursor && {cursor: options.cursor}),
        ...(options?.orderBy && {orderBy: options.orderBy})
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
    client: LogsApiClientContext,
    options?: ListOptions,
): PagedAsyncIterableIterator<LogRecord, ListPageResponse, ListPageSettings> {
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

    return buildPagedAsyncIterator<LogRecord, ListPageResponse, ListPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface SearchOptions extends OperationOptions {
}

export interface SearchPageSettings {
}

export interface SearchPageResponse {
    items: Array<LogRecord>
    nextCursor?: string
}

async function searchSend(
    client: LogsApiClientContext,
    query: LogQuery,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/logs/search").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonLogQueryToTransportTransform(query),
    };
    return await client.pathUnchecked(path).post(httpRequestOptions);
}

function searchDeserialize(
    response: PathUncheckedResponse,
    options?: SearchOptions,
) {
    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonCursorPageToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export function search(
    client: LogsApiClientContext,
    query: LogQuery,
    options?: SearchOptions,
): PagedAsyncIterableIterator<LogRecord, SearchPageResponse, SearchPageSettings> {
    function getElements(response: SearchPageResponse) {
        return response.items;
    }

    async function getPagedResponse(
        nextToken?: string,
        settings?: SearchPageSettings,
    ) {

        let response: PathUncheckedResponse;
        if (nextToken) {
            response = await client.pathUnchecked(nextToken).get();
        } else {
            const combinedOptions = {...options, ...settings};
            response = await searchSend(client, query, combinedOptions);
        }
        return {
            pagedResponse: await searchDeserialize(response, options),
            nextToken: response.body["nextCursor"],
        };
    }

    return buildPagedAsyncIterator<LogRecord, SearchPageResponse, SearchPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface AggregateOptions extends OperationOptions {
}

/**
 * Aggregate logs
 *
 * @param {LogsApiClientContext} client
 * @param {LogAggregationRequest} request
 * @param {AggregateOptions} [options]
 */
export async function aggregate(
    client: LogsApiClientContext,
    request: LogAggregationRequest,
    options?: AggregateOptions,
): Promise<LogAggregationResponse> {
    const path = parse("/api/v1/logs/aggregate").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonLogAggregationRequestToTransportTransform(request),
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonLogAggregationResponseToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetPatternsOptions extends OperationOptions {
    serviceName?: string
    startTime?: Date
    endTime?: Date
    minCount?: number
}

/**
 * Get log patterns
 *
 * @param {LogsApiClientContext} client
 * @param {GetPatternsOptions} [options]
 */
export async function getPatterns(
    client: LogsApiClientContext,
    options?: GetPatternsOptions,
): Promise<Array<LogPattern>> {
    const path = parse("/api/v1/logs/patterns{?serviceName,startTime,endTime,minCount}").expand({
        ...(options?.serviceName && {serviceName: options.serviceName}),
        ...(options?.startTime && {startTime: dateRfc3339Serializer(options.startTime)}),
        ...(options?.endTime && {endTime: dateRfc3339Serializer(options.endTime)}),
        ...(options?.minCount && {minCount: options.minCount})
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonArrayLogPatternToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetStatsOptions extends OperationOptions {
    serviceName?: string
    startTime?: Date
    endTime?: Date
}

/**
 * Get log statistics
 *
 * @param {LogsApiClientContext} client
 * @param {GetStatsOptions} [options]
 */
export async function getStats(
    client: LogsApiClientContext,
    options?: GetStatsOptions,
): Promise<LogStats> {
    const path = parse("/api/v1/logs/stats{?serviceName,startTime,endTime}").expand({
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
        return jsonLogStatsToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

