import {parse} from "uri-template";
import type {PathUncheckedResponse} from "@typespec/ts-http-runtime";
import {TracesApiClientContext} from "./tracesApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {buildPagedAsyncIterator, type PagedAsyncIterableIterator} from "../../helpers/pagingHelpers.js";
import {
    dateRfc3339Serializer,
    jsonCursorPageToApplicationTransform,
    jsonCursorPageToApplicationTransform_2,
    jsonTraceQueryToTransportTransform,
    jsonTraceToApplicationTransform
} from "../../models/internal/serializers.js";
import {SpanRecord, type SpanStatusCode, Trace, type TraceQuery} from "../../models/models.js";

export interface ListOptions extends OperationOptions {
    serviceName?: string
    minDurationMs?: bigint
    maxDurationMs?: bigint
    status?: SpanStatusCode
    startTime?: Date
    endTime?: Date
    limit?: number
    cursor?: string
}

export interface ListPageSettings {
}

export interface ListPageResponse {
    items: Array<Trace>
    nextCursor?: string
}

async function listSend(
    client: TracesApiClientContext,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/traces{?serviceName,minDurationMs,maxDurationMs,status,startTime,endTime,limit,cursor}").expand({
        ...(options?.serviceName && {serviceName: options.serviceName}),
        ...(options?.minDurationMs && {minDurationMs: options.minDurationMs}),
        ...(options?.maxDurationMs && {maxDurationMs: options.maxDurationMs}),
        ...(options?.status && {status: options.status}),
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
    client: TracesApiClientContext,
    options?: ListOptions,
): PagedAsyncIterableIterator<Trace, ListPageResponse, ListPageSettings> {
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

    return buildPagedAsyncIterator<Trace, ListPageResponse, ListPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface GetOptions extends OperationOptions {
}

/**
 * Get a specific trace by ID
 *
 * @param {TracesApiClientContext} client
 * @param {string} traceId
 * @param {GetOptions} [options]
 */
export async function get(
    client: TracesApiClientContext,
    traceId: string,
    options?: GetOptions,
): Promise<Trace> {
    const path = parse("/api/v1/traces/{traceId}").expand({
        traceId: traceId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonTraceToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetSpansOptions extends OperationOptions {
    limit?: number
    cursor?: string
}

export interface GetSpansPageSettings {
}

export interface GetSpansPageResponse {
    items: Array<SpanRecord>
    nextCursor?: string
}

async function getSpansSend(
    client: TracesApiClientContext,
    traceId: string,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/traces/{traceId}/spans{?limit,cursor}").expand({
        traceId: traceId,
        limit: options?.limit ?? 100,
        ...(options?.cursor && {cursor: options.cursor})
    });
    const httpRequestOptions = {
        headers: {},
    };
    return await client.pathUnchecked(path).get(httpRequestOptions);
}

function getSpansDeserialize(
    response: PathUncheckedResponse,
    options?: GetSpansOptions,
) {
    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonCursorPageToApplicationTransform_2(response.body)!;
    }
    throw createRestError(response);
}

export function getSpans(
    client: TracesApiClientContext,
    traceId: string,
    options?: GetSpansOptions,
): PagedAsyncIterableIterator<SpanRecord, GetSpansPageResponse, GetSpansPageSettings> {
    function getElements(response: GetSpansPageResponse) {
        return response.items;
    }

    async function getPagedResponse(
        nextToken?: string,
        settings?: GetSpansPageSettings,
    ) {

        let response: PathUncheckedResponse;
        if (nextToken) {
            response = await client.pathUnchecked(nextToken).get();
        } else {
            const combinedOptions = {...options, ...settings};
            response = await getSpansSend(client, traceId, combinedOptions);
        }
        return {
            pagedResponse: await getSpansDeserialize(response, options),
            nextToken: response.body["nextCursor"],
        };
    }

    return buildPagedAsyncIterator<SpanRecord, GetSpansPageResponse, GetSpansPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface SearchOptions extends OperationOptions {
}

export interface SearchPageSettings {
}

export interface SearchPageResponse {
    items: Array<Trace>
    nextCursor?: string
}

async function searchSend(
    client: TracesApiClientContext,
    query: TraceQuery,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/traces/search").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonTraceQueryToTransportTransform(query),
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
    client: TracesApiClientContext,
    query: TraceQuery,
    options?: SearchOptions,
): PagedAsyncIterableIterator<Trace, SearchPageResponse, SearchPageSettings> {
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

    return buildPagedAsyncIterator<Trace, SearchPageResponse, SearchPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}
