
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Threading;

namespace Qyl.Api._Streaming
{
    public partial class Streaming
    {
        private readonly Uri _endpoint;
        private StreamingStreamingApi _cachedStreamingStreamingApi;

        protected Streaming()
        {
        }

        internal Streaming(ClientPipeline pipeline, Uri endpoint)
        {
            _endpoint = endpoint;
            Pipeline = pipeline;
        }

        public ClientPipeline Pipeline { get; }

        public virtual StreamingStreamingApi GetStreamingStreamingApiClient()
        {
            return Volatile.Read(ref _cachedStreamingStreamingApi) ?? Interlocked.CompareExchange(ref _cachedStreamingStreamingApi, new StreamingStreamingApi(Pipeline, _endpoint), null) ?? _cachedStreamingStreamingApi;
        }
    }
}
