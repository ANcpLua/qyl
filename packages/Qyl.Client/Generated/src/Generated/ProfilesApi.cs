
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Storage;

namespace Qyl.Api
{
    public partial class ProfilesApi
    {
        private readonly Uri _endpoint;

        protected ProfilesApi()
        {
        }

        internal ProfilesApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult GetAll(string sessionId, string traceId, string serviceName, string sampleType, int? limit, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(sessionId, traceId, serviceName, sampleType, limit, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAllAsync(string sessionId, string traceId, string serviceName, string sampleType, int? limit, RequestOptions options)
        {
            using PipelineMessage message = CreateGetAllRequest(sessionId, traceId, serviceName, sampleType, limit, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<ProfileRecord>> GetAll(string sessionId = default, string traceId = default, string serviceName = default, string sampleType = default, int? limit = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = GetAll(sessionId, traceId, serviceName, sampleType, limit, cancellationToken.ToRequestOptions());
            List<ProfileRecord> value = new List<ProfileRecord>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProfileRecord.DeserializeProfileRecord(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProfileRecord>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<ProfileRecord>>> GetAllAsync(string sessionId = default, string traceId = default, string serviceName = default, string sampleType = default, int? limit = default, CancellationToken cancellationToken = default)
        {
            ClientResult result = await GetAllAsync(sessionId, traceId, serviceName, sampleType, limit, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<ProfileRecord> value = new List<ProfileRecord>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProfileRecord.DeserializeProfileRecord(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProfileRecord>)value, result.GetRawResponse());
        }

        public virtual ClientResult Get(string profileId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            using PipelineMessage message = CreateGetRequest(profileId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetAsync(string profileId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            using PipelineMessage message = CreateGetRequest(profileId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<ProfileRecord> Get(string profileId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            ClientResult result = Get(profileId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((ProfileRecord)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<ProfileRecord>> GetAsync(string profileId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(profileId, nameof(profileId));

            ClientResult result = await GetAsync(profileId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((ProfileRecord)result, result.GetRawResponse());
        }

        public virtual ClientResult GetByTrace(string traceId, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateGetByTraceRequest(traceId, limit, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetByTraceAsync(string traceId, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            using PipelineMessage message = CreateGetByTraceRequest(traceId, limit, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<ProfileRecord>> GetByTrace(string traceId, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = GetByTrace(traceId, limit, cancellationToken.ToRequestOptions());
            List<ProfileRecord> value = new List<ProfileRecord>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProfileRecord.DeserializeProfileRecord(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProfileRecord>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<ProfileRecord>>> GetByTraceAsync(string traceId, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(traceId, nameof(traceId));

            ClientResult result = await GetByTraceAsync(traceId, limit, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<ProfileRecord> value = new List<ProfileRecord>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProfileRecord.DeserializeProfileRecord(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProfileRecord>)value, result.GetRawResponse());
        }

        public virtual ClientResult GetBySpan(string spanId, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(spanId, nameof(spanId));

            using PipelineMessage message = CreateGetBySpanRequest(spanId, limit, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetBySpanAsync(string spanId, int? limit, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(spanId, nameof(spanId));

            using PipelineMessage message = CreateGetBySpanRequest(spanId, limit, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<IReadOnlyList<ProfileRecord>> GetBySpan(string spanId, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(spanId, nameof(spanId));

            ClientResult result = GetBySpan(spanId, limit, cancellationToken.ToRequestOptions());
            List<ProfileRecord> value = new List<ProfileRecord>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProfileRecord.DeserializeProfileRecord(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProfileRecord>)value, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<IReadOnlyList<ProfileRecord>>> GetBySpanAsync(string spanId, int? limit = default, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(spanId, nameof(spanId));

            ClientResult result = await GetBySpanAsync(spanId, limit, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            List<ProfileRecord> value = new List<ProfileRecord>();
            BinaryData data = result.GetRawResponse().Content;
            using JsonDocument document = JsonDocument.Parse(data);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                value.Add(ProfileRecord.DeserializeProfileRecord(item, ModelSerializationExtensions.WireOptions));
            }
            return ClientResult.FromValue((IReadOnlyList<ProfileRecord>)value, result.GetRawResponse());
        }
    }
}
