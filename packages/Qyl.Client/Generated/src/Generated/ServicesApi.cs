
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
using Qyl.Domains.Identity;

namespace Qyl.Api
{
    public partial class ServicesApi
    {
        private readonly Uri _endpoint;

        protected ServicesApi()
        {
        }

        internal ServicesApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string namespaceName, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(namespaceName, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string namespaceName, int? limit, string cursor, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(namespaceName, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageServiceInfo> GetAll(string namespaceName = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetAll(namespaceName, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageServiceInfo)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageServiceInfo>> GetAllAsync(string namespaceName = default, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetAllAsync(namespaceName, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageServiceInfo)result, result.GetRawResponse());
        }

        public virtual ClientResult Get(string serviceName, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            using PipelineMessage message = CreateGetRequest(serviceName, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAsync(string serviceName, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            using PipelineMessage message = CreateGetRequest(serviceName, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ServiceDetails> Get(string serviceName, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            ClientResult result = Get(serviceName, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ServiceDetails)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ServiceDetails>> GetAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            ClientResult result = await GetAsync(serviceName, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ServiceDetails)result, result.GetRawResponse());
        }

        public virtual ClientResult GetDependencies(string serviceName, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            using PipelineMessage message = CreateGetDependenciesRequest(serviceName, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetDependenciesAsync(string serviceName, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            using PipelineMessage message = CreateGetDependenciesRequest(serviceName, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<ServiceDependency>> GetDependencies(string serviceName, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            ClientResult result = GetDependencies(serviceName, cancellationToken.ToRequestOptions());
            List<ServiceDependency> value = new List<ServiceDependency>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ServiceDependency.DeserializeServiceDependency(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ServiceDependency>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<ServiceDependency>>> GetDependenciesAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            ClientResult result = await GetDependenciesAsync(serviceName, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<ServiceDependency> value = new List<ServiceDependency>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ServiceDependency.DeserializeServiceDependency(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ServiceDependency>)value, result.GetRawResponse());
        }

        public virtual ClientResult GetOperations(string serviceName, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            using PipelineMessage message = CreateGetOperationsRequest(serviceName, limit, cursor, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetOperationsAsync(string serviceName, int? limit, string cursor, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            using PipelineMessage message = CreateGetOperationsRequest(serviceName, limit, cursor, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<CursorPageOperationInfo> GetOperations(string serviceName, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            ClientResult result = GetOperations(serviceName, limit, cursor, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((CursorPageOperationInfo)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<CursorPageOperationInfo>> GetOperationsAsync(string serviceName, int? limit = default, string cursor = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(serviceName, nameof(serviceName));

            ClientResult result = await GetOperationsAsync(serviceName, limit, cursor, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((CursorPageOperationInfo)result, result.GetRawResponse());
        }
    }
}
