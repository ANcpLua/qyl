
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Sessions.Item.Traces
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class TracesRequestBuilder : BaseRequestBuilder
    {
                public TracesRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/sessions/{sessionId}/traces{?cursor,limit}", pathParameters)
        {
        }
                public TracesRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/sessions/{sessionId}/traces{?cursor,limit}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.V1.Sessions.Item.Traces.TracesGetResponse?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Sessions.Item.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.V1.Sessions.Item.Traces.TracesGetResponse> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Sessions.Item.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "404", global::Qyl.Core.Models.Qyl.Common.Errors.NotFoundError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.V1.Sessions.Item.Traces.TracesGetResponse>(requestInfo, global::Qyl.Core.V1.Sessions.Item.Traces.TracesGetResponse.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Sessions.Item.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Sessions.Item.Traces.TracesRequestBuilder.TracesRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
                public global::Qyl.Core.V1.Sessions.Item.Traces.TracesRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Sessions.Item.Traces.TracesRequestBuilder(rawUrl, RequestAdapter);
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
                        [QueryParameter("limit")]
            public int? Limit { get; set; }
        }
    }
}
#pragma warning restore CS0618
