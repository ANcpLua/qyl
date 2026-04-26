import {parse} from "uri-template";
import type {PathUncheckedResponse} from "@typespec/ts-http-runtime";
import {WorkspacesApiClientContext} from "./workspacesApiClientContext.js";
import {createRestError} from "../../helpers/error.js";
import type {OperationOptions} from "../../helpers/interfaces.js";
import {buildPagedAsyncIterator, type PagedAsyncIterableIterator} from "../../helpers/pagingHelpers.js";
import {
    jsonArrayProjectEnvironmentEntityToApplicationTransform,
    jsonCursorPageToApplicationTransform_10 as jsonCursorPageToApplicationTransform,
    jsonProjectCreateRequestToTransportTransform,
    jsonProjectEntityToApplicationTransform,
    jsonWorkspaceEnvelopeEntityToApplicationTransform
} from "../../models/internal/serializers.js";
import {
    ProjectCreateRequest,
    ProjectEntity,
    type ProjectEnvironmentEntity,
    type WorkspaceEnvelopeEntity
} from "../../models/models.js";

export interface GetCurrentOptions extends OperationOptions {
}

/**
 * Get current workspace envelope
 *
 * @param {WorkspacesApiClientContext} client
 * @param {GetCurrentOptions} [options]
 */
export async function getCurrent(
    client: WorkspacesApiClientContext,
    options?: GetCurrentOptions,
): Promise<WorkspaceEnvelopeEntity> {
    const path = parse("/api/v1/workspaces/current").expand({});
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonWorkspaceEnvelopeEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface HeartbeatOptions extends OperationOptions {
}

/**
 * Register workspace heartbeat
 *
 * @param {WorkspacesApiClientContext} client
 * @param {HeartbeatOptions} [options]
 */
export async function heartbeat(
    client: WorkspacesApiClientContext,
    options?: HeartbeatOptions,
): Promise<WorkspaceEnvelopeEntity> {
    const path = parse("/api/v1/workspaces/current/heartbeat").expand({});
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonWorkspaceEnvelopeEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface ListProjectsOptions extends OperationOptions {
    limit?: number
    cursor?: string
}

export interface ListProjectsPageSettings {
}

export interface ListProjectsPageResponse {
    items: Array<ProjectEntity>
    nextCursor?: string
}

async function listProjectsSend(
    client: WorkspacesApiClientContext,
    options?: Record<string, any>,
) {
    const path = parse("/api/v1/workspaces/projects{?limit,cursor}").expand({
        limit: options?.limit ?? 20,
        ...(options?.cursor && {cursor: options.cursor})
    });
    const httpRequestOptions = {
        headers: {},
    };
    return await client.pathUnchecked(path).get(httpRequestOptions);
}

function listProjectsDeserialize(
    response: PathUncheckedResponse,
    options?: ListProjectsOptions,
) {
    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonCursorPageToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export function listProjects(
    client: WorkspacesApiClientContext,
    options?: ListProjectsOptions,
): PagedAsyncIterableIterator<ProjectEntity, ListProjectsPageResponse, ListProjectsPageSettings> {
    function getElements(response: ListProjectsPageResponse) {
        return response.items;
    }

    async function getPagedResponse(
        nextToken?: string,
        settings?: ListProjectsPageSettings,
    ) {

        let response: PathUncheckedResponse;
        if (nextToken) {
            response = await client.pathUnchecked(nextToken).get();
        } else {
            const combinedOptions = {...options, ...settings};
            response = await listProjectsSend(client, combinedOptions);
        }
        return {
            pagedResponse: await listProjectsDeserialize(response, options),
            nextToken: response.body["nextCursor"],
        };
    }

    return buildPagedAsyncIterator<ProjectEntity, ListProjectsPageResponse, ListProjectsPageSettings>({
        getElements,
        getPagedResponse: getPagedResponse as any
    });
}

export interface GetProjectOptions extends OperationOptions {
}

/**
 * Get project by ID
 *
 * @param {WorkspacesApiClientContext} client
 * @param {string} projectId
 * @param {GetProjectOptions} [options]
 */
export async function getProject(
    client: WorkspacesApiClientContext,
    projectId: string,
    options?: GetProjectOptions,
): Promise<ProjectEntity> {
    const path = parse("/api/v1/workspaces/projects/{projectId}").expand({
        projectId: projectId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonProjectEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface CreateProjectOptions extends OperationOptions {
}

/**
 * Create a new project
 *
 * @param {WorkspacesApiClientContext} client
 * @param {ProjectCreateRequest} project
 * @param {CreateProjectOptions} [options]
 */
export async function createProject(
    client: WorkspacesApiClientContext,
    project: ProjectCreateRequest,
    options?: CreateProjectOptions,
): Promise<ProjectEntity> {
    const path = parse("/api/v1/workspaces/projects").expand({});
    const httpRequestOptions = {
        headers: {}, body: jsonProjectCreateRequestToTransportTransform(project),
    };
    const response = await client.pathUnchecked(path).post(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonProjectEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

export interface ListEnvironmentsOptions extends OperationOptions {
}

/**
 * List environments for a project
 *
 * @param {WorkspacesApiClientContext} client
 * @param {string} projectId
 * @param {ListEnvironmentsOptions} [options]
 */
export async function listEnvironments(
    client: WorkspacesApiClientContext,
    projectId: string,
    options?: ListEnvironmentsOptions,
): Promise<Array<ProjectEnvironmentEntity>> {
    const path = parse("/api/v1/workspaces/projects/{projectId}/environments").expand({
        projectId: projectId
    });
    const httpRequestOptions = {
        headers: {},
    };
    const response = await client.pathUnchecked(path).get(httpRequestOptions);


    if (typeof options?.operationOptions?.onResponse === "function") {
        options?.operationOptions?.onResponse(response);
    }
    if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
        return jsonArrayProjectEnvironmentEntityToApplicationTransform(response.body)!;
    }
    throw createRestError(response);
}

