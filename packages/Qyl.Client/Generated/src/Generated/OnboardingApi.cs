
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;
using Qyl.Domains.Workspace;

namespace Qyl.Api
{
    public partial class OnboardingApi
    {
        private readonly Uri _endpoint;

        protected OnboardingApi()
        {
        }

        internal OnboardingApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult StartHandshake(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateStartHandshakeRequest(content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> StartHandshakeAsync(BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateStartHandshakeRequest(content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<HandshakeSessionEntity> StartHandshake(HandshakeStartRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = StartHandshake(request, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((HandshakeSessionEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<HandshakeSessionEntity>> StartHandshakeAsync(HandshakeStartRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = await StartHandshakeAsync(request, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((HandshakeSessionEntity)result, result.GetRawResponse());
        }

        public virtual ClientResult VerifyHandshake(string sessionId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateVerifyHandshakeRequest(sessionId, content, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> VerifyHandshakeAsync(string sessionId, BinaryContent content, RequestOptions options = null)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));
            Argument.AssertNotNull(content, nameof(content));

            using PipelineMessage message = CreateVerifyHandshakeRequest(sessionId, content, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<HandshakeVerifyResponse> VerifyHandshake(string sessionId, HandshakeVerifyRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = VerifyHandshake(sessionId, request, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((HandshakeVerifyResponse)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<HandshakeVerifyResponse>> VerifyHandshakeAsync(string sessionId, HandshakeVerifyRequest request, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));
            Argument.AssertNotNull(request, nameof(request));

            ClientResult result = await VerifyHandshakeAsync(sessionId, request, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((HandshakeVerifyResponse)result, result.GetRawResponse());
        }

        public virtual ClientResult GetHandshake(string sessionId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            using PipelineMessage message = CreateGetHandshakeRequest(sessionId, options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> GetHandshakeAsync(string sessionId, RequestOptions options)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            using PipelineMessage message = CreateGetHandshakeRequest(sessionId, options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult<HandshakeSessionEntity> GetHandshake(string sessionId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            ClientResult result = GetHandshake(sessionId, cancellationToken.ToRequestOptions());
            return ClientResult.FromValue((HandshakeSessionEntity)result, result.GetRawResponse());
        }

        public virtual async Task<ClientResult<HandshakeSessionEntity>> GetHandshakeAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(sessionId, nameof(sessionId));

            ClientResult result = await GetHandshakeAsync(sessionId, cancellationToken.ToRequestOptions()).ConfigureAwait(false);
            return ClientResult.FromValue((HandshakeSessionEntity)result, result.GetRawResponse());
        }
    }
}
