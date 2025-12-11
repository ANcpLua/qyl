
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using Qyl.Core.V1.Traces.Item;
using Qyl.Core.V1.Traces.Search;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Traces
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class TracesRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.Traces.Search.SearchRequestBuilder Search
        {
            get => new global::Qyl.Core.V1.Traces.Search.SearchRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Traces.Item.WithTraceItemRequestBuilder this[string position]
        {
            get
            {
                var urlTplParams = new Dictionary<string, object>(PathParameters);
                urlTplParams.Add("traceId", position);
                return new global::Qyl.Core.V1.Traces.Item.WithTraceItemRequestBuilder(urlTplParams, RequestAdapter);
            }
        }
                public TracesRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/traces{?cursor,endTime,limit,maxDurationMs,minDurationMs,serviceName,startTime,status}", pathParameters)
        {
        }
                public TracesRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/traces{?cursor,endTime,limit,maxDurationMs,minDurationMs,serviceName,startTime,status}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.V1.Traces.TracesGetResponse?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.V1.Traces.TracesGetResponse> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "400", global::Qyl.Core.Models.Qyl.Common.Errors.ValidationError.CreateFromDiscriminatorValue },
                { "404", global::Qyl.Core.Models.Qyl.Common.Errors.NotFoundError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.V1.Traces.TracesGetResponse>(requestInfo, global::Qyl.Core.V1.Traces.TracesGetResponse.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
                public global::Qyl.Core.V1.Traces.TracesRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Traces.TracesRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class TracesRequestBuilderGetQueryParameters
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
                        [QueryParameter("maxDurationMs")]
            public long? MaxDurationMs { get; set; }
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
                        [QueryParameter("startTime")]
            public DateTimeOffset? StartTime { get; set; }
                        [QueryParameter("status")]
            public double? Status { get; set; }
        }
    }
}
#pragma warning restore CS0618
