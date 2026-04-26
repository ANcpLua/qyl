import {parse} from "uri-template";
import type {PathUncheckedResponse} from "@typespec/ts-http-runtime";
import {SessionsApiClientContext} from "./sessionsApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {buildPagedAsyncIterator, type PagedAsyncIterableIterator} from "../../helpers/pagingHelpers.js";
import {
    dateRfc3339Serializer,
    jsonCursorPageToApplicationTransform as jsonCursorPageToApplicationTransform_2,
    jsonCursorPageToApplicationTransform_5 as jsonCursorPageToApplicationTransform,
    jsonSessionEntityToApplicationTransform,
    jsonSessionStatsToApplicationTransform
} from "../../models/internal/serializers.js";
import {SessionEntity, type SessionStats, Trace} from "../../models/models.js";

export interface ListOptions extends OperationOptions {
    userId?: string
    isActive?: boolean
    startTime?: Date
    endTime?: Date
    limit?: number
    cursor?: string
}

export interface ListPageSettings {
}

export interface ListPageResponse {
    items: Array<SessionEntity>
    nextCursor?: string
}

async function listSend(
    client: SessionsApiClientContext,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/sessions{?userId,isActive,startTime,endTime,limit,cursor}").expand({
        ...(options?.userId && {userId: options.userId}),
        ...(options?.isActive && {isActive: options.isActive}),
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
    client: SessionsApiClientContext,
    options?: ListOptions,
): PagedAsyncIterableIterator<SessionEntity, ListPageResponse, ListPageSettings> {
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

    return buildPagedAsyncIterator<SessionEntity, ListPageResponse, ListPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface GetOptions extends OperationOptions {
}

/**
 * Get session by ID
 *
 * @param {SessionsApiClientContext} client
 * @param {string} sessionId
 * @param {GetOptions} [options]
 */
export async function get(
    client: SessionsApiClientContext,
    sessionId: string,
    options?: GetOptions,
): Promise<SessionEntity> {
    const path = parse("/api/v1/sessions/{sessionId}").expand({
        sessionId: sessionId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonSessionEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetTracesOptions extends OperationOptions {
    limit?: number
    cursor?: string
}

export interface GetTracesPageSettings {
}

export interface GetTracesPageResponse {
    items: Array<Trace>
    nextCursor?: string
}

async function getTracesSend(
    client: SessionsApiClientContext,
    sessionId: string,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/sessions/{sessionId}/traces{?limit,cursor}").expand({
        sessionId: sessionId,
        limit: options?.limit ?? 100,
        ...(options?.cursor && {cursor: options.cursor})
    });
    const httpRequestOptions = {
        headers: {},
    };
    return await client.pathUnchecked(path).get(httpRequestOptions);
}

function getTracesDeserialize(
    response: PathUncheckedResponse,
    options?: GetTracesOptions,
) {
    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonCursorPageToApplicationTransform_2(response.body)!;
    }
    throw createRestError(response);
}

export function getTraces(
    client: SessionsApiClientContext,
    sessionId: string,
    options?: GetTracesOptions,
): PagedAsyncIterableIterator<Trace, GetTracesPageResponse, GetTracesPageSettings> {
    function getElements(response: GetTracesPageResponse) {
        return response.items;
    }

    async function getPagedResponse(
        nextToken?: string,
        settings?: GetTracesPageSettings,
    ) {

        let response: PathUncheckedResponse;
        if (nextToken) {
            response = await client.pathUnchecked(nextToken).get();
        } else {
            const combinedOptions = {...options, ...settings};
            response = await getTracesSend(client, sessionId, combinedOptions);
        }
        return {
            pagedResponse: await getTracesDeserialize(response, options),
            nextToken: response.body["nextCursor"],
        };
    }

    return buildPagedAsyncIterator<Trace, GetTracesPageResponse, GetTracesPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface GetStatsOptions extends OperationOptions {
    startTime?: Date
    endTime?: Date
}

/**
 * Get session statistics
 *
 * @param {SessionsApiClientContext} client
 * @param {GetStatsOptions} [options]
 */
export async function getStats(
    client: SessionsApiClientContext,
    options?: GetStatsOptions,
): Promise<SessionStats> {
    const path = parse("/api/v1/sessions/stats{?startTime,endTime}").expand({
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
        return jsonSessionStatsToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

