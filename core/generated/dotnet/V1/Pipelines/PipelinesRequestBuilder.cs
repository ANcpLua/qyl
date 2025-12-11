
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using Qyl.Core.Models.Qyl.Domains.Ops.Cicd;
using Qyl.Core.V1.Pipelines.Stats;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Pipelines
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class PipelinesRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder Stats
        {
            get => new global::Qyl.Core.V1.Pipelines.Stats.StatsRequestBuilder(PathParameters, RequestAdapter);
        }
                public PipelinesRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/pipelines{?cursor,endTime,limit,pipelineName,startTime,status,system}", pathParameters)
        {
        }
                public PipelinesRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/pipelines{?cursor,endTime,limit,pipelineName,startTime,status,system}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.V1.Pipelines.PipelinesGetResponse?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder.PipelinesRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.V1.Pipelines.PipelinesGetResponse> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder.PipelinesRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "400", global::Qyl.Core.Models.Qyl.Common.Errors.ValidationError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.V1.Pipelines.PipelinesGetResponse>(requestInfo, global::Qyl.Core.V1.Pipelines.PipelinesGetResponse.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder.PipelinesRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder.PipelinesRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
                public global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class PipelinesRequestBuilderGetQueryParameters
        {
            #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
            [QueryParameter("cursor")]
            public string? Cursor { get; set; }
#nullable restore
#else
            [QueryParameter("cursor")]
            public string Cursor { get; set; }
#endif
                        [QueryParameter("endTime")]
            public DateTimeOffset? EndTime { get; set; }
                        [QueryParameter("limit")]
            public int? Limit { get; set; }
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
                        [QueryParameter("status")]
            public global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdPipelineStatus? Status { get; set; }
                        [QueryParameter("system")]
            public global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdSystem? System { get; set; }
        }
    }
}
#pragma warning restore CS0618
