
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using Qyl.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Pipelines.Stats
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class StatsRequestBuilder : BaseRequestBuilder
    {
                public StatsRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/pipelines/stats{?endTime,pipelineName,startTime}", pathParameters)
        {
        }
                public StatsRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/pipelines/stats{?endTime,pipelineName,startTime}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.Models.PipelineStats?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder.StatsRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.Models.PipelineStats> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder.StatsRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.Models.PipelineStats>(requestInfo, global::Qyl.Core.Models.PipelineStats.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder.StatsRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder.StatsRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
                public global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class StatsRequestBuilderGetQueryParameters
        {
                        [QueryParameter("endTime")]
            public DateTimeOffset? EndTime { get; set; }
            #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
            [QueryParameter("pipelineName")]
            public string? PipelineName { get; set; }
#nullable restore
#else
            [QueryParameter("pipelineName")]
            public string PipelineName { get; set; }
#endif
                        [QueryParameter("startTime")]
            public DateTimeOffset? StartTime { get; set; }
        }
    }
}
#pragma warning restore CS0618
