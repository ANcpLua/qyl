import { type AlertsApiClientContext, type AlertsApiClientOptions, createAlertsApiClientContext } from "./api/alertsApiClient/alertsApiClientContext.js";
import { getFixRun, type GetFixRunOptions, getRule, type GetRuleOptions, listFirings, type ListFiringsOptions, listFixRuns, type ListFixRunsOptions, listRules, type ListRulesOptions } from "./api/alertsApiClient/alertsApiClientOperations.js";
import { type ApiClientContext, type ApiClientOptions, createApiClientContext } from "./api/apiClientContext.js";
import { type ConfiguratorApiClientContext, type ConfiguratorApiClientOptions, createConfiguratorApiClientContext } from "./api/configuratorApiClient/configuratorApiClientContext.js";
import { createJob, type CreateJobOptions, createProfile, type CreateProfileOptions, getJob, type GetJobOptions, getProfile, type GetProfileOptions, getSelections, type GetSelectionsOptions, listProfiles, type ListProfilesOptions, saveSelections, type SaveSelectionsOptions } from "./api/configuratorApiClient/configuratorApiClientOperations.js";
import { createDeploymentsApiClientContext, type DeploymentsApiClientContext, type DeploymentsApiClientOptions } from "./api/deploymentsApiClient/deploymentsApiClientContext.js";
import { create, type CreateOptions, get as get_5, getDoraMetrics, type GetDoraMetricsOptions, type GetOptions as GetOptions_5, list as list_7, type ListOptions as ListOptions_7, update as update_2, type UpdateOptions as UpdateOptions_2 } from "./api/deploymentsApiClient/deploymentsApiClientOperations.js";
import { createErrorsApiClientContext, type ErrorsApiClientContext, type ErrorsApiClientOptions } from "./api/errorsApiClient/errorsApiClientContext.js";
import { get as get_4, getCorrelations, type GetCorrelationsOptions, type GetOptions as GetOptions_4, getStats as getStats_3, type GetStatsOptions as GetStatsOptions_3, list as list_6, type ListOptions as ListOptions_6, update, type UpdateOptions } from "./api/errorsApiClient/errorsApiClientOperations.js";
import { createHealthApiClientContext, type HealthApiClientContext, type HealthApiClientOptions } from "./api/healthApiClient/healthApiClientContext.js";
import { alive, type AliveOptions, ready, type ReadyOptions } from "./api/healthApiClient/healthApiClientOperations.js";
import { createIssuesApiClientContext, type IssuesApiClientContext, type IssuesApiClientOptions } from "./api/issuesApiClient/issuesApiClientContext.js";
import { get as get_7, getBreadcrumbs, type GetBreadcrumbsOptions, getEvents, type GetEventsOptions, type GetOptions as GetOptions_7, list as list_9, type ListOptions as ListOptions_9, update as update_3, type UpdateOptions as UpdateOptions_3 } from "./api/issuesApiClient/issuesApiClientOperations.js";
import { createLogsApiClientContext, type LogsApiClientContext, type LogsApiClientOptions } from "./api/logsApiClient/logsApiClientContext.js";
import { aggregate, type AggregateOptions, getPatterns, type GetPatternsOptions, getStats, type GetStatsOptions, list as list_2, type ListOptions as ListOptions_2, search as search_2, type SearchOptions as SearchOptions_2 } from "./api/logsApiClient/logsApiClientOperations.js";
import { createMetricsApiClientContext, type MetricsApiClientContext, type MetricsApiClientOptions } from "./api/metricsApiClient/metricsApiClientContext.js";
import { getMetadata, type GetMetadataOptions, list as list_3, type ListOptions as ListOptions_3, query, type QueryOptions } from "./api/metricsApiClient/metricsApiClientOperations.js";
import { createOnboardingApiClientContext, type OnboardingApiClientContext, type OnboardingApiClientOptions } from "./api/onboardingApiClient/onboardingApiClientContext.js";
import { getHandshake, type GetHandshakeOptions, startHandshake, type StartHandshakeOptions, verifyHandshake, type VerifyHandshakeOptions } from "./api/onboardingApiClient/onboardingApiClientOperations.js";
import { createProfilesApiClientContext, type ProfilesApiClientContext, type ProfilesApiClientOptions } from "./api/profilesApiClient/profilesApiClientContext.js";
import { get as get_2, getBySpan, type GetBySpanOptions, getByTrace, type GetByTraceOptions, type GetOptions as GetOptions_2, list as list_4, type ListOptions as ListOptions_4 } from "./api/profilesApiClient/profilesApiClientOperations.js";
import { createSearchApiClientContext, type SearchApiClientContext, type SearchApiClientOptions } from "./api/searchApiClient/searchApiClientContext.js";
import { getSuggestions, type GetSuggestionsOptions, search as search_3, type SearchOptions as SearchOptions_3 } from "./api/searchApiClient/searchApiClientOperations.js";
import { createServicesApiClientContext, type ServicesApiClientContext, type ServicesApiClientOptions } from "./api/servicesApiClient/servicesApiClientContext.js";
import { get as get_6, getDependencies, type GetDependenciesOptions, getOperations, type GetOperationsOptions, type GetOptions as GetOptions_6, list as list_8, type ListOptions as ListOptions_8 } from "./api/servicesApiClient/servicesApiClientOperations.js";
import { createSessionsApiClientContext, type SessionsApiClientContext, type SessionsApiClientOptions } from "./api/sessionsApiClient/sessionsApiClientContext.js";
import { get as get_3, type GetOptions as GetOptions_3, getStats as getStats_2, type GetStatsOptions as GetStatsOptions_2, getTraces, type GetTracesOptions, list as list_5, type ListOptions as ListOptions_5 } from "./api/sessionsApiClient/sessionsApiClientOperations.js";
import { createStreamingApiClientContext, type StreamingApiClientContext, type StreamingApiClientOptions } from "./api/streamingClient/streamingApiClient/streamingApiClientContext.js";
import { streamDeployments, type StreamDeploymentsOptions, streamEvents, type StreamEventsOptions, streamLogs, type StreamLogsOptions, streamMetrics, type StreamMetricsOptions, streamTraces, type StreamTracesOptions, streamTraceSpans, type StreamTraceSpansOptions } from "./api/streamingClient/streamingApiClient/streamingApiClientOperations.js";
import { createStreamingClientContext, type StreamingClientContext, type StreamingClientOptions } from "./api/streamingClient/streamingClientContext.js";
import { createTracesApiClientContext, type TracesApiClientContext, type TracesApiClientOptions } from "./api/tracesApiClient/tracesApiClientContext.js";
import { get, type GetOptions, getSpans, type GetSpansOptions, list, type ListOptions, search, type SearchOptions } from "./api/tracesApiClient/tracesApiClientOperations.js";
import { createWorkflowsApiClientContext, type WorkflowsApiClientContext, type WorkflowsApiClientOptions } from "./api/workflowsApiClient/workflowsApiClientContext.js";
import { approveStep, type ApproveStepOptions, getRun, getRunEvents, type GetRunEventsOptions, getRunNodes, type GetRunNodesOptions, type GetRunOptions, listRuns, type ListRunsOptions, resumeRun, type ResumeRunOptions } from "./api/workflowsApiClient/workflowsApiClientOperations.js";
import { createWorkspacesApiClientContext, type WorkspacesApiClientContext, type WorkspacesApiClientOptions } from "./api/workspacesApiClient/workspacesApiClientContext.js";
import { createProject, type CreateProjectOptions, getCurrent, type GetCurrentOptions, getProject, type GetProjectOptions, heartbeat, type HeartbeatOptions, listEnvironments, type ListEnvironmentsOptions, listProjects, type ListProjectsOptions } from "./api/workspacesApiClient/workspacesApiClientOperations.js";
import type { DeploymentCreate, DeploymentUpdate, ErrorUpdate, GenerationJobCreateRequest, GenerationProfileCreateRequest, GenerationSelectionSaveRequest, HandshakeStartRequest, HandshakeVerifyRequest, IssueUpdateRequest, LogAggregationRequest, LogQuery, MetricQueryRequest, ProjectCreateRequest, SearchRequest, TraceQuery } from "./models/models.js";

export class ApiClient {
  #context: ApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: ApiClientOptions,
  ) {
    this.#context = createApiClientContext(endpoint, options);

  }
}
export class AlertsApiClient {
  #context: AlertsApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: AlertsApiClientOptions,
  ) {
    this.#context = createAlertsApiClientContext(endpoint, options);

  }
  listRules(options?: ListRulesOptions) {
    return listRules(this.#context, options);
  };
  async getRule(ruleId: string, options?: GetRuleOptions) {
    return getRule(this.#context, ruleId, options);
  };
  listFirings(options?: ListFiringsOptions) {
    return listFirings(this.#context, options);
  };
  listFixRuns(options?: ListFixRunsOptions) {
    return listFixRuns(this.#context, options);
  };
  async getFixRun(fixId: string, options?: GetFixRunOptions) {
    return getFixRun(this.#context, fixId, options);
  }
}
export class SearchApiClient {
  #context: SearchApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: SearchApiClientOptions,
  ) {
    this.#context = createSearchApiClientContext(endpoint, options);

  }
  async search(request: SearchRequest, options?: SearchOptions_3) {
    return search_3(this.#context, request, options);
  };
  async getSuggestions(query: string, options?: GetSuggestionsOptions) {
    return getSuggestions(this.#context, query, options);
  }
}
export class WorkflowsApiClient {
  #context: WorkflowsApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: WorkflowsApiClientOptions,
  ) {
    this.#context = createWorkflowsApiClientContext(endpoint, options);

  }
  listRuns(options?: ListRunsOptions) {
    return listRuns(this.#context, options);
  };
  async getRun(runId: string, options?: GetRunOptions) {
    return getRun(this.#context, runId, options);
  };
  getRunNodes(runId: string, options?: GetRunNodesOptions) {
    return getRunNodes(this.#context, runId, options);
  };
  async getRunEvents(runId: string, options?: GetRunEventsOptions) {
    return getRunEvents(this.#context, runId, options);
  };
  async resumeRun(runId: string, options?: ResumeRunOptions) {
    return resumeRun(this.#context, runId, options);
  };
  async approveStep(
    runId: string,
    nodeId: string,
    options?: ApproveStepOptions,
  ) {
    return approveStep(this.#context, runId, nodeId, options);
  }
}
export class IssuesApiClient {
  #context: IssuesApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: IssuesApiClientOptions,
  ) {
    this.#context = createIssuesApiClientContext(endpoint, options);

  }
  list(options?: ListOptions_9) {
    return list_9(this.#context, options);
  };
  async get(issueId: string, options?: GetOptions_7) {
    return get_7(this.#context, issueId, options);
  };
  async update(
    issueId: string,
    update: IssueUpdateRequest,
    options?: UpdateOptions_3,
  ) {
    return update_3(this.#context, issueId, update, options);
  };
  getEvents(issueId: string, options?: GetEventsOptions) {
    return getEvents(this.#context, issueId, options);
  };
  async getBreadcrumbs(issueId: string, options?: GetBreadcrumbsOptions) {
    return getBreadcrumbs(this.#context, issueId, options);
  }
}
export class ConfiguratorApiClient {
  #context: ConfiguratorApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: ConfiguratorApiClientOptions,
  ) {
    this.#context = createConfiguratorApiClientContext(endpoint, options);

  }
  listProfiles(options?: ListProfilesOptions) {
    return listProfiles(this.#context, options);
  };
  async getProfile(profileId: string, options?: GetProfileOptions) {
    return getProfile(this.#context, profileId, options);
  };
  async createProfile(
    profile: GenerationProfileCreateRequest,
    options?: CreateProfileOptions,
  ) {
    return createProfile(this.#context, profile, options);
  };
  async getSelections(workspaceId: string, options?: GetSelectionsOptions) {
    return getSelections(this.#context, workspaceId, options);
  };
  async saveSelections(
    selections: GenerationSelectionSaveRequest,
    options?: SaveSelectionsOptions,
  ) {
    return saveSelections(this.#context, selections, options);
  };
  async createJob(job: GenerationJobCreateRequest, options?: CreateJobOptions) {
    return createJob(this.#context, job, options);
  };
  async getJob(jobId: string, options?: GetJobOptions) {
    return getJob(this.#context, jobId, options);
  }
}
export class OnboardingApiClient {
  #context: OnboardingApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: OnboardingApiClientOptions,
  ) {
    this.#context = createOnboardingApiClientContext(endpoint, options);

  }
  async startHandshake(
    request: HandshakeStartRequest,
    options?: StartHandshakeOptions,
  ) {
    return startHandshake(this.#context, request, options);
  };
  async verifyHandshake(
    sessionId: string,
    request: HandshakeVerifyRequest,
    options?: VerifyHandshakeOptions,
  ) {
    return verifyHandshake(this.#context, sessionId, request, options);
  };
  async getHandshake(sessionId: string, options?: GetHandshakeOptions) {
    return getHandshake(this.#context, sessionId, options);
  }
}
export class WorkspacesApiClient {
  #context: WorkspacesApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: WorkspacesApiClientOptions,
  ) {
    this.#context = createWorkspacesApiClientContext(endpoint, options);

  }
  async getCurrent(options?: GetCurrentOptions) {
    return getCurrent(this.#context, options);
  };
  async heartbeat(options?: HeartbeatOptions) {
    return heartbeat(this.#context, options);
  };
  listProjects(options?: ListProjectsOptions) {
    return listProjects(this.#context, options);
  };
  async getProject(projectId: string, options?: GetProjectOptions) {
    return getProject(this.#context, projectId, options);
  };
  async createProject(
    project: ProjectCreateRequest,
    options?: CreateProjectOptions,
  ) {
    return createProject(this.#context, project, options);
  };
  async listEnvironments(projectId: string, options?: ListEnvironmentsOptions) {
    return listEnvironments(this.#context, projectId, options);
  }
}
export class HealthApiClient {
  #context: HealthApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: HealthApiClientOptions,
  ) {
    this.#context = createHealthApiClientContext(endpoint, options);

  }
  async alive(options?: AliveOptions) {
    return alive(this.#context, options);
  };
  async ready(options?: ReadyOptions) {
    return ready(this.#context, options);
  }
}
export class ServicesApiClient {
  #context: ServicesApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: ServicesApiClientOptions,
  ) {
    this.#context = createServicesApiClientContext(endpoint, options);

  }
  list(options?: ListOptions_8) {
    return list_8(this.#context, options);
  };
  async get(serviceName: string, options?: GetOptions_6) {
    return get_6(this.#context, serviceName, options);
  };
  async getDependencies(serviceName: string, options?: GetDependenciesOptions) {
    return getDependencies(this.#context, serviceName, options);
  };
  getOperations(serviceName: string, options?: GetOperationsOptions) {
    return getOperations(this.#context, serviceName, options);
  }
}
export class DeploymentsApiClient {
  #context: DeploymentsApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: DeploymentsApiClientOptions,
  ) {
    this.#context = createDeploymentsApiClientContext(endpoint, options);

  }
  list(options?: ListOptions_7) {
    return list_7(this.#context, options);
  };
  async get(deploymentId: string, options?: GetOptions_5) {
    return get_5(this.#context, deploymentId, options);
  };
  async create(deployment: DeploymentCreate, options?: CreateOptions) {
    return create(this.#context, deployment, options);
  };
  async update(
    deploymentId: string,
    update: DeploymentUpdate,
    options?: UpdateOptions_2,
  ) {
    return update_2(this.#context, deploymentId, update, options);
  };
  async getDoraMetrics(options?: GetDoraMetricsOptions) {
    return getDoraMetrics(this.#context, options);
  }
}
export class ErrorsApiClient {
  #context: ErrorsApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: ErrorsApiClientOptions,
  ) {
    this.#context = createErrorsApiClientContext(endpoint, options);

  }
  list(options?: ListOptions_6) {
    return list_6(this.#context, options);
  };
  async get(errorId: string, options?: GetOptions_4) {
    return get_4(this.#context, errorId, options);
  };
  async update(errorId: string, update: ErrorUpdate, options?: UpdateOptions) {
    return update(this.#context, errorId, update, options);
  };
  async getStats(options?: GetStatsOptions_3) {
    return getStats_3(this.#context, options);
  };
  async getCorrelations(errorId: string, options?: GetCorrelationsOptions) {
    return getCorrelations(this.#context, errorId, options);
  }
}
export class SessionsApiClient {
  #context: SessionsApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: SessionsApiClientOptions,
  ) {
    this.#context = createSessionsApiClientContext(endpoint, options);

  }
  list(options?: ListOptions_5) {
    return list_5(this.#context, options);
  };
  async get(sessionId: string, options?: GetOptions_3) {
    return get_3(this.#context, sessionId, options);
  };
  getTraces(sessionId: string, options?: GetTracesOptions) {
    return getTraces(this.#context, sessionId, options);
  };
  async getStats(options?: GetStatsOptions_2) {
    return getStats_2(this.#context, options);
  }
}
export class ProfilesApiClient {
  #context: ProfilesApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: ProfilesApiClientOptions,
  ) {
    this.#context = createProfilesApiClientContext(endpoint, options);

  }
  async list(options?: ListOptions_4) {
    return list_4(this.#context, options);
  };
  async get(profileId: string, options?: GetOptions_2) {
    return get_2(this.#context, profileId, options);
  };
  async getByTrace(traceId: string, options?: GetByTraceOptions) {
    return getByTrace(this.#context, traceId, options);
  };
  async getBySpan(spanId: string, options?: GetBySpanOptions) {
    return getBySpan(this.#context, spanId, options);
  }
}
export class MetricsApiClient {
  #context: MetricsApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: MetricsApiClientOptions,
  ) {
    this.#context = createMetricsApiClientContext(endpoint, options);

  }
  list(options?: ListOptions_3) {
    return list_3(this.#context, options);
  };
  async query(request: MetricQueryRequest, options?: QueryOptions) {
    return query(this.#context, request, options);
  };
  async getMetadata(metricName: string, options?: GetMetadataOptions) {
    return getMetadata(this.#context, metricName, options);
  }
}
export class LogsApiClient {
  #context: LogsApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: LogsApiClientOptions,
  ) {
    this.#context = createLogsApiClientContext(endpoint, options);

  }
  list(options?: ListOptions_2) {
    return list_2(this.#context, options);
  };
  search(query: LogQuery, options?: SearchOptions_2) {
    return search_2(this.#context, query, options);
  };
  async aggregate(request: LogAggregationRequest, options?: AggregateOptions) {
    return aggregate(this.#context, request, options);
  };
  async getPatterns(options?: GetPatternsOptions) {
    return getPatterns(this.#context, options);
  };
  async getStats(options?: GetStatsOptions) {
    return getStats(this.#context, options);
  }
}
export class TracesApiClient {
  #context: TracesApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: TracesApiClientOptions,
  ) {
    this.#context = createTracesApiClientContext(endpoint, options);

  }
  list(options?: ListOptions) {
    return list(this.#context, options);
  };
  async get(traceId: string, options?: GetOptions) {
    return get(this.#context, traceId, options);
  };
  getSpans(traceId: string, options?: GetSpansOptions) {
    return getSpans(this.#context, traceId, options);
  };
  search(query: TraceQuery, options?: SearchOptions) {
    return search(this.#context, query, options);
  }
}
export class StreamingClient {
  #context: StreamingClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: StreamingClientOptions,
  ) {
    this.#context = createStreamingClientContext(endpoint, options);

  }
}
export class StreamingApiClient {
  #context: StreamingApiClientContext
  constructor(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: StreamingApiClientOptions,
  ) {
    this.#context = createStreamingApiClientContext(endpoint, options);

  }
  async streamEvents(options?: StreamEventsOptions) {
    return streamEvents(this.#context, options);
  };
  async streamTraces(options?: StreamTracesOptions) {
    return streamTraces(this.#context, options);
  };
  async streamTraceSpans(traceId: string, options?: StreamTraceSpansOptions) {
    return streamTraceSpans(this.#context, traceId, options);
  };
  async streamLogs(options?: StreamLogsOptions) {
    return streamLogs(this.#context, options);
  };
  async streamMetrics(options?: StreamMetricsOptions) {
    return streamMetrics(this.#context, options);
  };
  async streamDeployments(options?: StreamDeploymentsOptions) {
    return streamDeployments(this.#context, options);
  }
}
