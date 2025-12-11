
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.StreamNamespace.Deployments
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class DeploymentsRequestBuilder : BaseRequestBuilder
    {
                public DeploymentsRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream/deployments{?environment,serviceName}", pathParameters)
        {
        }
                public DeploymentsRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream/deployments{?environment,serviceName}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<Stream?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<Stream> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            return await RequestAdapter.SendPrimitiveAsync<Stream>(requestInfo, default, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "text/event-stream");
            return requestInfo;
        }
                public global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class DeploymentsRequestBuilderGetQueryParameters
        {
            #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
            [QueryParameter("environment")]
            public string? Environment { get; set; }
#nullable restore
#else
            [QueryParameter("environment")]
            public string Environment { get; set; }
#endif
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
