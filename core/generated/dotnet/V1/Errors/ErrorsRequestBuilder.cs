
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using Qyl.Core.Models.Qyl.Domains.Observe.Error;
using Qyl.Core.V1.Errors.Item;
using Qyl.Core.V1.Errors.Stats;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Errors
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ErrorsRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.Errors.Stats.StatsRequestBuilder Stats
        {
            get => new global::Qyl.Core.V1.Errors.Stats.StatsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Errors.Item.WithErrorItemRequestBuilder this[string position]
        {
            get
            {
                var urlTplParams = new Dictionary<string, object>(PathParameters);
                urlTplParams.Add("errorId", position);
                return new global::Qyl.Core.V1.Errors.Item.WithErrorItemRequestBuilder(urlTplParams, RequestAdapter);
            }
        }
                public ErrorsRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/errors{?category,cursor,endTime,limit,serviceName,startTime,status}", pathParameters)
        {
        }
                public ErrorsRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/errors{?category,cursor,endTime,limit,serviceName,startTime,status}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.V1.Errors.ErrorsGetResponse?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Errors.ErrorsRequestBuilder.ErrorsRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.V1.Errors.ErrorsGetResponse> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Errors.ErrorsRequestBuilder.ErrorsRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "400", global::Qyl.Core.Models.Qyl.Common.Errors.ValidationError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.V1.Errors.ErrorsGetResponse>(requestInfo, global::Qyl.Core.V1.Errors.ErrorsGetResponse.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Errors.ErrorsRequestBuilder.ErrorsRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Errors.ErrorsRequestBuilder.ErrorsRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
                public global::Qyl.Core.V1.Errors.ErrorsRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Errors.ErrorsRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class ErrorsRequestBuilderGetQueryParameters
        {
                        [QueryParameter("category")]
            public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategory? Category { get; set; }
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
            public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus? Status { get; set; }
        }
    }
}
#pragma warning restore CS0618
