
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;
using System.Threading.Tasks;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class HealthApi
    {
        private readonly Uri _endpoint;

        protected HealthApi()
        {
        }

        internal HealthApi(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual ClientResult Alive(RequestOptions options)
        {
            using PipelineMessage message = CreateAliveRequest(options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> AliveAsync(RequestOptions options)
        {
            using PipelineMessage message = CreateAliveRequest(options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult Alive(CancellationToken cancellationToken = default)
        {
            return Alive(cancellationToken.ToRequestOptions());
        }

        public virtual async Task<ClientResult> AliveAsync(CancellationToken cancellationToken = default)
        {
            return await AliveAsync(cancellationToken.ToRequestOptions()).ConfigureAwait(false);
        }

        public virtual ClientResult Ready(RequestOptions options)
        {
            using PipelineMessage message = CreateReadyRequest(options);
            return ClientResult.FromResponse(Pipeline.ProcessMessage(message, options));
        }

        public virtual async Task<ClientResult> ReadyAsync(RequestOptions options)
        {
            using PipelineMessage message = CreateReadyRequest(options);
            return ClientResult.FromResponse(await Pipeline.ProcessMessageAsync(message, options).ConfigureAwait(false));
        }

        public virtual ClientResult Ready(CancellationToken cancellationToken = default)
        {
            return Ready(cancellationToken.ToRequestOptions());
        }

        public virtual async Task<ClientResult> ReadyAsync(CancellationToken cancellationToken = default)
        {
            return await ReadyAsync(cancellationToken.ToRequestOptions()).ConfigureAwait(false);
        }
    }
}
