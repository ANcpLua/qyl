import {parse} from "uri-template";
import type {PathUncheckedResponse} from "@typespec/ts-http-runtime";
import {ConfiguratorApiClientContext} from "./configuratorApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {buildPagedAsyncIterator, type PagedAsyncIterableIterator} from "../../helpers/pagingHelpers.js";
import {
    jsonArrayGenerationSelectionEntityToApplicationTransform,
    jsonCursorPageToApplicationTransform_11 as jsonCursorPageToApplicationTransform,
    jsonGenerationJobCreateRequestToTransportTransform,
    jsonGenerationJobEntityToApplicationTransform,
    jsonGenerationProfileCreateRequestToTransportTransform,
    jsonGenerationProfileEntityToApplicationTransform,
    jsonGenerationSelectionEntityToApplicationTransform,
    jsonGenerationSelectionSaveRequestToTransportTransform
} from "../../models/internal/serializers.js";
import {
    GenerationJobCreateRequest,
    type GenerationJobEntity,
    GenerationProfileCreateRequest,
    GenerationProfileEntity,
    type GenerationSelectionEntity,
    GenerationSelectionSaveRequest
} from "../../models/models.js";

export interface ListProfilesOptions extends OperationOptions {
    limit?: number
    cursor?: string
}

export interface ListProfilesPageSettings {
}

export interface ListProfilesPageResponse {
    items: Array<GenerationProfileEntity>
    nextCursor?: string
}

async function listProfilesSend(
    client: ConfiguratorApiClientContext,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/configurator/profiles{?limit,cursor}").expand({
        limit: options?.limit ?? 20,
        ...(options?.cursor && {cursor: options.cursor})
    });
    const httpRequestOptions = {
        headers: {},
    };
    return await client.pathUnchecked(path).get(httpRequestOptions);
}

function listProfilesDeserialize(
    response: PathUncheckedResponse,
    options?: ListProfilesOptions,
) {
    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonCursorPageToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export function listProfiles(
    client: ConfiguratorApiClientContext,
    options?: ListProfilesOptions,
): PagedAsyncIterableIterator<GenerationProfileEntity, ListProfilesPageResponse, ListProfilesPageSettings> {
    function getElements(response: ListProfilesPageResponse) {
        return response.items;
    }

    async function getPagedResponse(
        nextToken?: string,
        settings?: ListProfilesPageSettings,
    ) {

        let response: PathUncheckedResponse;
        if (nextToken) {
            response = await client.pathUnchecked(nextToken).get();
        } else {
            const combinedOptions = {...options, ...settings};
            response = await listProfilesSend(client, combinedOptions);
        }
        return {
            pagedResponse: await listProfilesDeserialize(response, options),
            nextToken: response.body["nextCursor"],
        };
    }

    return buildPagedAsyncIterator<GenerationProfileEntity, ListProfilesPageResponse, ListProfilesPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface GetProfileOptions extends OperationOptions {
}

/**
 * Get generation profile by ID
 *
 * @param {ConfiguratorApiClientContext} client
 * @param {string} profileId
 * @param {GetProfileOptions} [options]
 */
export async function getProfile(
    client: ConfiguratorApiClientContext,
    profileId: string,
    options?: GetProfileOptions,
): Promise<GenerationProfileEntity> {
    const path = parse("/api/v1/configurator/profiles/{profileId}").expand({
        profileId: profileId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonGenerationProfileEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface CreateProfileOptions extends OperationOptions {
}

/**
 * Create generation profile
 *
 * @param {ConfiguratorApiClientContext} client
 * @param {GenerationProfileCreateRequest} profile
 * @param {CreateProfileOptions} [options]
 */
export async function createProfile(
    client: ConfiguratorApiClientContext,
    profile: GenerationProfileCreateRequest,
    options?: CreateProfileOptions,
): Promise<GenerationProfileEntity> {
    const path = parse("/api/v1/configurator/profiles").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonGenerationProfileCreateRequestToTransportTransform(profile),
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonGenerationProfileEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetSelectionsOptions extends OperationOptions {
}

/**
 * Get generation selections for a workspace
 *
 * @param {ConfiguratorApiClientContext} client
 * @param {string} workspaceId
 * @param {GetSelectionsOptions} [options]
 */
export async function getSelections(
    client: ConfiguratorApiClientContext,
    workspaceId: string,
    options?: GetSelectionsOptions,
): Promise<Array<GenerationSelectionEntity>> {
    const path = parse("/api/v1/configurator/selections{?workspaceId}").expand({
        workspaceId: workspaceId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonArrayGenerationSelectionEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface SaveSelectionsOptions extends OperationOptions {
}

/**
 * Save generation selections
 *
 * @param {ConfiguratorApiClientContext} client
 * @param {GenerationSelectionSaveRequest} selections
 * @param {SaveSelectionsOptions} [options]
 */
export async function saveSelections(
    client: ConfiguratorApiClientContext,
    selections: GenerationSelectionSaveRequest,
    options?: SaveSelectionsOptions,
): Promise<GenerationSelectionEntity> {
    const path = parse("/api/v1/configurator/selections").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonGenerationSelectionSaveRequestToTransportTransform(selections),
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonGenerationSelectionEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface CreateJobOptions extends OperationOptions {
}

/**
 * Create a generation job
 *
 * @param {ConfiguratorApiClientContext} client
 * @param {GenerationJobCreateRequest} job
 * @param {CreateJobOptions} [options]
 */
export async function createJob(
    client: ConfiguratorApiClientContext,
    job: GenerationJobCreateRequest,
    options?: CreateJobOptions,
): Promise<GenerationJobEntity> {
    const path = parse("/api/v1/configurator/jobs").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonGenerationJobCreateRequestToTransportTransform(job),
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonGenerationJobEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface GetJobOptions extends OperationOptions {
}

/**
 * Get generation job status
 *
 * @param {ConfiguratorApiClientContext} client
 * @param {string} jobId
 * @param {GetJobOptions} [options]
 */
export async function getJob(
    client: ConfiguratorApiClientContext,
    jobId: string,
    options?: GetJobOptions,
): Promise<GenerationJobEntity> {
    const path = parse("/api/v1/configurator/jobs/{jobId}").expand({
        jobId: jobId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonGenerationJobEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

