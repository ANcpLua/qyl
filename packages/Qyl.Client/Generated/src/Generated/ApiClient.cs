
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Qyl.Api._Streaming;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class ApiClient
    {
        private readonly Uri _endpoint;
        private Streaming _cachedStreaming;
        private TracesApi _cachedTracesApi;
        private LogsApi _cachedLogsApi;
        private MetricsApi _cachedMetricsApi;
        private ProfilesApi _cachedProfilesApi;
        private SessionsApi _cachedSessionsApi;
        private ErrorsApi _cachedErrorsApi;
        private DeploymentsApi _cachedDeploymentsApi;
        private ServicesApi _cachedServicesApi;
        private HealthApi _cachedHealthApi;
        private WorkspacesApi _cachedWorkspacesApi;
        private OnboardingApi _cachedOnboardingApi;
        private ConfiguratorApi _cachedConfiguratorApi;
        private IssuesApi _cachedIssuesApi;
        private WorkflowsApi _cachedWorkflowsApi;
        private SearchApi _cachedSearchApi;
        private AlertsApi _cachedAlertsApi;

        public ApiClient() : this(new Uri("https://api.staging.qyl.dev"), new ApiClientOptions())
        {
        }

        internal ApiClient(AuthenticationPolicy authenticationPolicy, Uri endpoint, ApiClientOptions options)
        {
            Argument.AssertNotNull(endpoint, nameof(endpoint));

            options ??= new ApiClientOptions();

            _endpoint = endpoint;
            if (authenticationPolicy != null)
            {
                Pipeline = ClientPipeline.Create(options, Array.Empty<PipelinePolicy>(), new PipelinePolicy[] { new UserAgentPolicy(typeof(ApiClient).Assembly), authenticationPolicy }, Array.Empty<PipelinePolicy>());
            }
            else
            {
                Pipeline = ClientPipeline.Create(options, Array.Empty<PipelinePolicy>(), new PipelinePolicy[] { new UserAgentPolicy(typeof(ApiClient).Assembly) }, Array.Empty<PipelinePolicy>());
            }
        }

        public ApiClient(Uri endpoint, ApiClientOptions options) : this(null, endpoint, options)
        {
        }

        public ApiClient(ApiClientSettings settings) : this(AuthenticationPolicy.Create(settings), settings?.Endpoint, settings?.Options)
        {
        }

        public ClientPipeline Pipeline { get; }

        public virtual Streaming GetStreamingClient()
        {
            return Volatile.Read(ref _cachedStreaming) ?? Interlocked.CompareExchange(ref _cachedStreaming, new Streaming(Pipeline, _endpoint), null) ?? _cachedStreaming;
        }

        public virtual TracesApi GetTracesApiClient()
        {
            return Volatile.Read(ref _cachedTracesApi) ?? Interlocked.CompareExchange(ref _cachedTracesApi, new TracesApi(Pipeline, _endpoint), null) ?? _cachedTracesApi;
        }

        public virtual LogsApi GetLogsApiClient()
        {
            return Volatile.Read(ref _cachedLogsApi) ?? Interlocked.CompareExchange(ref _cachedLogsApi, new LogsApi(Pipeline, _endpoint), null) ?? _cachedLogsApi;
        }

        public virtual MetricsApi GetMetricsApiClient()
        {
            return Volatile.Read(ref _cachedMetricsApi) ?? Interlocked.CompareExchange(ref _cachedMetricsApi, new MetricsApi(Pipeline, _endpoint), null) ?? _cachedMetricsApi;
        }

        public virtual ProfilesApi GetProfilesApiClient()
        {
            return Volatile.Read(ref _cachedProfilesApi) ?? Interlocked.CompareExchange(ref _cachedProfilesApi, new ProfilesApi(Pipeline, _endpoint), null) ?? _cachedProfilesApi;
        }

        public virtual SessionsApi GetSessionsApiClient()
        {
            return Volatile.Read(ref _cachedSessionsApi) ?? Interlocked.CompareExchange(ref _cachedSessionsApi, new SessionsApi(Pipeline, _endpoint), null) ?? _cachedSessionsApi;
        }

        public virtual ErrorsApi GetErrorsApiClient()
        {
            return Volatile.Read(ref _cachedErrorsApi) ?? Interlocked.CompareExchange(ref _cachedErrorsApi, new ErrorsApi(Pipeline, _endpoint), null) ?? _cachedErrorsApi;
        }

        public virtual DeploymentsApi GetDeploymentsApiClient()
        {
            return Volatile.Read(ref _cachedDeploymentsApi) ?? Interlocked.CompareExchange(ref _cachedDeploymentsApi, new DeploymentsApi(Pipeline, _endpoint), null) ?? _cachedDeploymentsApi;
        }

        public virtual ServicesApi GetServicesApiClient()
        {
            return Volatile.Read(ref _cachedServicesApi) ?? Interlocked.CompareExchange(ref _cachedServicesApi, new ServicesApi(Pipeline, _endpoint), null) ?? _cachedServicesApi;
        }

        public virtual HealthApi GetHealthApiClient()
        {
            return Volatile.Read(ref _cachedHealthApi) ?? Interlocked.CompareExchange(ref _cachedHealthApi, new HealthApi(Pipeline, _endpoint), null) ?? _cachedHealthApi;
        }

        public virtual WorkspacesApi GetWorkspacesApiClient()
        {
            return Volatile.Read(ref _cachedWorkspacesApi) ?? Interlocked.CompareExchange(ref _cachedWorkspacesApi, new WorkspacesApi(Pipeline, _endpoint), null) ?? _cachedWorkspacesApi;
        }

        public virtual OnboardingApi GetOnboardingApiClient()
        {
            return Volatile.Read(ref _cachedOnboardingApi) ?? Interlocked.CompareExchange(ref _cachedOnboardingApi, new OnboardingApi(Pipeline, _endpoint), null) ?? _cachedOnboardingApi;
        }

        public virtual ConfiguratorApi GetConfiguratorApiClient()
        {
            return Volatile.Read(ref _cachedConfiguratorApi) ?? Interlocked.CompareExchange(ref _cachedConfiguratorApi, new ConfiguratorApi(Pipeline, _endpoint), null) ?? _cachedConfiguratorApi;
        }

        public virtual IssuesApi GetIssuesApiClient()
        {
            return Volatile.Read(ref _cachedIssuesApi) ?? Interlocked.CompareExchange(ref _cachedIssuesApi, new IssuesApi(Pipeline, _endpoint), null) ?? _cachedIssuesApi;
        }

        public virtual WorkflowsApi GetWorkflowsApiClient()
        {
            return Volatile.Read(ref _cachedWorkflowsApi) ?? Interlocked.CompareExchange(ref _cachedWorkflowsApi, new WorkflowsApi(Pipeline, _endpoint), null) ?? _cachedWorkflowsApi;
        }

        public virtual SearchApi GetSearchApiClient()
        {
            return Volatile.Read(ref _cachedSearchApi) ?? Interlocked.CompareExchange(ref _cachedSearchApi, new SearchApi(Pipeline, _endpoint), null) ?? _cachedSearchApi;
        }

        public virtual AlertsApi GetAlertsApiClient()
        {
            return Volatile.Read(ref _cachedAlertsApi) ?? Interlocked.CompareExchange(ref _cachedAlertsApi, new AlertsApi(Pipeline, _endpoint), null) ?? _cachedAlertsApi;
        }
    }
}
