
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
using Qyl.Domains.Configurator;

namespace Qyl.Api
{
    public partial class ConfiguratorApi
    {
        private readonly Uri _endpoint;

        protected ConfiguratorApi()
        {
        }

        internal ConfiguratorApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetProfiles(int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetProfilesRequest(limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetProfilesAsync(int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetProfilesRequest(limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageGenerationProfileEntity> GetProfiles(int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetProfiles(limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageGenerationProfileEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageGenerationProfileEntity>> GetProfilesAsync(int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetProfilesAsync(limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageGenerationProfileEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetProfile(string profileId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            using PipelineMessage message = CreateGetProfileRequest(profileId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetProfileAsync(string profileId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            using PipelineMessage message = CreateGetProfileRequest(profileId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<GenerationProfileEntity> GetProfile(string profileId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            ClientResult result = GetProfile(profileId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((GenerationProfileEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<GenerationProfileEntity>> GetProfileAsync(string profileId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            ClientResult result = await GetProfileAsync(profileId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((GenerationProfileEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult CreateProfile(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateProfileRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> CreateProfileAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateProfileRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<GenerationProfileEntity> CreateProfile(GenerationProfileCreateRequest profile, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(profile, nameof(profile));

            ClientResult result = CreateProfile(profile, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((GenerationProfileEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<GenerationProfileEntity>> CreateProfileAsync(GenerationProfileCreateRequest profile, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(profile, nameof(profile));

            ClientResult result = await CreateProfileAsync(profile, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((GenerationProfileEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetSelections(string workspaceId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(workspaceId, nameof(workspaceId));

            using PipelineMessage message = CreateGetSelectionsRequest(workspaceId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetSelectionsAsync(string workspaceId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(workspaceId, nameof(workspaceId));

            using PipelineMessage message = CreateGetSelectionsRequest(workspaceId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<GenerationSelectionEntity>> GetSelections(string workspaceId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(workspaceId, nameof(workspaceId));

            ClientResult result = GetSelections(workspaceId, cancellationToken.ToRequestOptions());
            List<GenerationSelectionEntity> value = new List<GenerationSelectionEntity>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(GenerationSelectionEntity.DeserializeGenerationSelectionEntity(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<GenerationSelectionEntity>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<GenerationSelectionEntity>>> GetSelectionsAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(workspaceId, nameof(workspaceId));

            ClientResult result = await GetSelectionsAsync(workspaceId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<GenerationSelectionEntity> value = new List<GenerationSelectionEntity>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(GenerationSelectionEntity.DeserializeGenerationSelectionEntity(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<GenerationSelectionEntity>)value, result.GetRawResponse());
        }

        public virtual ClientResult SaveSelections(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateSaveSelectionsRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> SaveSelectionsAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateSaveSelectionsRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<GenerationSelectionEntity> SaveSelections(GenerationSelectionSaveRequest selections, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(selections, nameof(selections));

            ClientResult result = SaveSelections(selections, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((GenerationSelectionEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<GenerationSelectionEntity>> SaveSelectionsAsync(GenerationSelectionSaveRequest selections, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(selections, nameof(selections));

            ClientResult result = await SaveSelectionsAsync(selections, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((GenerationSelectionEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult CreateJob(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateJobRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> CreateJobAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateCreateJobRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<GenerationJobEntity> CreateJob(GenerationJobCreateRequest job, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(job, nameof(job));

            ClientResult result = CreateJob(job, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((GenerationJobEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<GenerationJobEntity>> CreateJobAsync(GenerationJobCreateRequest job, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(job, nameof(job));

            ClientResult result = await CreateJobAsync(job, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((GenerationJobEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult GetJob(string jobId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(jobId, nameof(jobId));

            using PipelineMessage message = CreateGetJobRequest(jobId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetJobAsync(string jobId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(jobId, nameof(jobId));

            using PipelineMessage message = CreateGetJobRequest(jobId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<GenerationJobEntity> GetJob(string jobId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(jobId, nameof(jobId));

            ClientResult result = GetJob(jobId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((GenerationJobEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<GenerationJobEntity>> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(jobId, nameof(jobId));

            ClientResult result = await GetJobAsync(jobId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((GenerationJobEntity)result, result.GetRawResponse());
        }
    }
}
