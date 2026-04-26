import {parse} from "uri-template";
import {SearchApiClientContext} from "./searchApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {
    jsonArrayStringToApplicationTransform,
    jsonSearchRequestToTransportTransform,
    jsonSearchResponseToApplicationTransform
} from "../../models/internal/serializers.js";
import {SearchRequest, type SearchResponse} from "../../models/models.js";

export interface SearchOptions extends OperationOptions {
}

/**
 * Unified search across all entity types
 *
 * @param {SearchApiClientContext} client
 * @param {SearchRequest} request
 * @param {SearchOptions} [options]
 */
export async function search(
    client: SearchApiClientContext,
    request: SearchRequest,
    options?: SearchOptions,
): Promise<SearchResponse> {
    const path = parse("/api/v1/search").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonSearchRequestToTransportTransform(request),
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonSearchResponseToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetSuggestionsOptions extends OperationOptions {
    limit?: number
}

/**
 * Get search suggestions
 *
 * @param {SearchApiClientContext} client
 * @param {string} query
 * @param {GetSuggestionsOptions} [options]
 */
export async function getSuggestions(
    client: SearchApiClientContext,
    query: string,
    options?: GetSuggestionsOptions,
): Promise<Array<string>> {
    const path = parse("/api/v1/search/suggestions{?query,limit}").expand({
        query: query,
        limit: options?.limit ?? 5
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonArrayStringToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

