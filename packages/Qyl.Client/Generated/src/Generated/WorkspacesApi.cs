
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.Domains.Workspace;

namespace Qyl.Api
{
    public partial class WorkspacesApi
    {
        private readonly Uri _endpoint;

        protected WorkspacesApi()
        {
        }

        internal WorkspacesApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetCurrent(RequestOptions options)
        {
            using PipelineMessage message = CreateGetCurrentRequest(options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetCurrentAsync(RequestOptions options)
        {
            using PipelineMessage message = CreateGetCurrentRequest(options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<WorkspaceEnvelopeEntity> GetCurrent(CancellationToken cancellationToken = default)
        {
            ClientResult result = GetCurrent(cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((WorkspaceEnvelopeEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<WorkspaceEnvelopeEntity>> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetCurrentAsync(cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((WorkspaceEnvelopeEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult Heartbeat(RequestOptions options)
        {
            using PipelineMessage message = CreateHeartbeatRequest(options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> HeartbeatAsync(RequestOptions options)
        {
            using PipelineMessage message = CreateHeartbeatRequest(options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<WorkspaceEnvelopeEntity> Heartbeat(CancellationToken cancellationToken = default)
        {
            ClientResult result = Heartbeat(cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((WorkspaceEnvelopeEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<WorkspaceEnvelopeEntity>> HeartbeatAsync(CancellationToken cancellationToken = default)
        {
            ClientResult result = await HeartbeatAsync(cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((WorkspaceEnvelopeEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetProjects(int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetProjectsRequest(limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetProjectsAsync(int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetProjectsRequest(limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageProjectEntity> GetProjects(int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetProjects(limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageProjectEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageProjectEntity>> GetProjectsAsync(int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetProjectsAsync(limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageProjectEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetProject(string projectId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            using PipelineMessage message = CreateGetProjectRequest(projectId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetProjectAsync(string projectId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            using PipelineMessage message = CreateGetProjectRequest(projectId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ProjectEntity> GetProject(string projectId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            ClientResult result = GetProject(projectId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ProjectEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ProjectEntity>> GetProjectAsync(string projectId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            ClientResult result = await GetProjectAsync(projectId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ProjectEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult CreateProject(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateProjectRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> CreateProjectAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateProjectRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ProjectEntity> CreateProject(ProjectCreateRequest project, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(project, nameof(project));

            ClientResult result = CreateProject(project, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ProjectEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ProjectEntity>> CreateProjectAsync(ProjectCreateRequest project, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(project, nameof(project));

            ClientResult result = await CreateProjectAsync(project, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ProjectEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetEnvironments(string projectId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            using PipelineMessage message = CreateGetEnvironmentsRequest(projectId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetEnvironmentsAsync(string projectId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            using PipelineMessage message = CreateGetEnvironmentsRequest(projectId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<ProjectEnvironmentEntity>> GetEnvironments(string projectId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            ClientResult result = GetEnvironments(projectId, cancellationToken.ToRequestOptions());
            List<ProjectEnvironmentEntity> value = new List<ProjectEnvironmentEntity>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProjectEnvironmentEntity.DeserializeProjectEnvironmentEntity(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProjectEnvironmentEntity>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<ProjectEnvironmentEntity>>> GetEnvironmentsAsync(string projectId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(projectId, nameof(projectId));

            ClientResult result = await GetEnvironmentsAsync(projectId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<ProjectEnvironmentEntity> value = new List<ProjectEnvironmentEntity>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProjectEnvironmentEntity.DeserializeProjectEnvironmentEntity(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProjectEnvironmentEntity>)value, result.GetRawResponse());
        }
    }
}
