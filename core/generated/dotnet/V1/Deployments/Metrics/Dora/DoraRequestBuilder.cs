
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using Qyl.Core.Models.Qyl.Domains.Ops.Deployment;
using Qyl.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Deployments.Metrics.Dora
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class DoraRequestBuilder : BaseRequestBuilder
    {
                public DoraRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments/metrics/dora{?endTime,environment,serviceName,startTime}", pathParameters)
        {
        }
                public DoraRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments/metrics/dora{?endTime,environment,serviceName,startTime}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.Models.DoraMetrics?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder.DoraRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.Models.DoraMetrics> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder.DoraRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.Models.DoraMetrics>(requestInfo, global::Qyl.Core.Models.DoraMetrics.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder.DoraRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder.DoraRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
                public global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class DoraRequestBuilderGetQueryParameters
        {
                        [QueryParameter("endTime")]
            public DateTimeOffset? EndTime { get; set; }
                        [QueryParameter("environment")]
            public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment? Environment { get; set; }
            #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
            [QueryParameter("serviceName")]
            public string? ServiceName { get; set; }
#nullable restore
#else
            [QueryParameter("serviceName")]
            public string ServiceName { get; set; }
#endif
                        [QueryParameter("startTime")]
            public DateTimeOffset? StartTime { get; set; }
        }
    }
}
#pragma warning restore CS0618
