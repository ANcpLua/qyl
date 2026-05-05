
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.Domains.Ops.Deployment;

namespace Qyl.Api
{
    public partial class DeploymentsApi
    {
        private readonly Uri _endpoint;

        protected DeploymentsApi()
        {
        }

        internal DeploymentsApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string serviceName, string environment, string status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(serviceName, environment, status, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string serviceName, string environment, string status, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(serviceName, environment, status, startTime, endTime, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageDeploymentEntity> GetAll(string serviceName = default, DeploymentEnvironment? environment = default, DeploymentStatus? status = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetAll(serviceName, environment?.ToSerialString(), status?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageDeploymentEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageDeploymentEntity>> GetAllAsync(string serviceName = default, DeploymentEnvironment? environment = default, DeploymentStatus? status = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetAllAsync(serviceName, environment?.ToSerialString(), status?.ToSerialString(), startTime, endTime, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageDeploymentEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Get(string deploymentId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(deploymentId, nameof(deploymentId));

            using PipelineMessage message = CreateGetRequest(deploymentId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAsync(string deploymentId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(deploymentId, nameof(deploymentId));

            using PipelineMessage message = CreateGetRequest(deploymentId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<DeploymentEntity> Get(string deploymentId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(deploymentId, nameof(deploymentId));

            ClientResult result = Get(deploymentId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((DeploymentEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<DeploymentEntity>> GetAsync(string deploymentId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(deploymentId, nameof(deploymentId));

            ClientResult result = await GetAsync(deploymentId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((DeploymentEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Create(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> CreateAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<DeploymentEntity> Create(DeploymentCreate deployment, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deployment, nameof(deployment));

            ClientResult result = Create(deployment, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((DeploymentEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<DeploymentEntity>> CreateAsync(DeploymentCreate deployment, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(deployment, nameof(deployment));

            ClientResult result = await CreateAsync(deployment, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((DeploymentEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Update(string deploymentId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(deploymentId, nameof(deploymentId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRequest(deploymentId, content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> UpdateAsync(string deploymentId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(deploymentId, nameof(deploymentId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateUpdateRequest(deploymentId, content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult GetDoraMetrics(string serviceName, string environment, DateTimeOffset? startTime, DateTimeOffset? endTime, RequestOptions options)
        {
            using PipelineMessage message = CreateGetDoraMetricsRequest(serviceName, environment, startTime, endTime, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetDoraMetricsAsync(string serviceName, string environment, DateTimeOffset? startTime, DateTimeOffset? endTime, RequestOptions options)
        {
            using PipelineMessage message = CreateGetDoraMetricsRequest(serviceName, environment, startTime, endTime, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<DoraMetrics> GetDoraMetrics(string serviceName = default, DeploymentEnvironment? environment = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetDoraMetrics(serviceName, environment?.ToSerialString(), startTime, endTime, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((DoraMetrics)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<DoraMetrics>> GetDoraMetricsAsync(string serviceName = default, DeploymentEnvironment? environment = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetDoraMetricsAsync(serviceName, environment?.ToSerialString(), startTime, endTime, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((DoraMetrics)result, result.GetRawResponse());
        }
    }
}
