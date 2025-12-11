
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Health.Live;
using Qyl.Core.Health.Ready;
using Qyl.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.Health
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class HealthRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.Health.Live.LiveRequestBuilder Live
        {
            get => new global::Qyl.Core.Health.Live.LiveRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.Health.Ready.ReadyRequestBuilder Ready
        {
            get => new global::Qyl.Core.Health.Ready.ReadyRequestBuilder(PathParameters, RequestAdapter);
        }
                public HealthRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/health", pathParameters)
        {
        }
                public HealthRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/health", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.Models.HealthResponse?> GetAsync(Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.Models.HealthResponse> GetAsync(Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            return await RequestAdapter.SendAsync<global::Qyl.Core.Models.HealthResponse>(requestInfo, global::Qyl.Core.Models.HealthResponse.CreateFromDiscriminatorValue, default, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
                public global::Qyl.Core.Health.HealthRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.Health.HealthRequestBuilder(rawUrl, RequestAdapter);
        }
    }
}
#pragma warning restore CS0618
