
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.V1.StreamNamespace.Traces.Item;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.StreamNamespace.Traces
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class TracesRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.StreamNamespace.Traces.Item.WithTraceItemRequestBuilder this[string position]
        {
            get
            {
                var urlTplParams = new Dictionary<string, object>(PathParameters);
                urlTplParams.Add("traceId", position);
                return new global::Qyl.Core.V1.StreamNamespace.Traces.Item.WithTraceItemRequestBuilder(urlTplParams, RequestAdapter);
            }
        }
                public TracesRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream/traces{?minDurationMs,serviceName}", pathParameters)
        {
        }
                public TracesRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream/traces{?minDurationMs,serviceName}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<Stream?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<Stream> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            return await RequestAdapter.SendPrimitiveAsync<Stream>(requestInfo, default, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "text/event-stream");
            return requestInfo;
        }
                public global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class TracesRequestBuilderGetQueryParameters
        {
                        [QueryParameter("minDurationMs")]
            public long? MinDurationMs { get; set; }
            #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
            [QueryParameter("serviceName")]
            public string? ServiceName { get; set; }
#nullable restore
#else
            [QueryParameter("serviceName")]
            public string ServiceName { get; set; }
#endif
        }
    }
}
#pragma warning restore CS0618
