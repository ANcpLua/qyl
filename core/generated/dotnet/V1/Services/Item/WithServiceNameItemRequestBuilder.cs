
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using Qyl.Core.Models;
using Qyl.Core.V1.Services.Item.Dependencies;
using Qyl.Core.V1.Services.Item.Operations;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Services.Item
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class WithServiceNameItemRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.Services.Item.Dependencies.DependenciesRequestBuilder Dependencies
        {
            get => new global::Qyl.Core.V1.Services.Item.Dependencies.DependenciesRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Services.Item.Operations.OperationsRequestBuilder Operations
        {
            get => new global::Qyl.Core.V1.Services.Item.Operations.OperationsRequestBuilder(PathParameters, RequestAdapter);
        }
                public WithServiceNameItemRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/services/{serviceName}", pathParameters)
        {
        }
                public WithServiceNameItemRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/services/{serviceName}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.Models.ServiceDetails?> GetAsync(Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.Models.ServiceDetails> GetAsync(Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "404", global::Qyl.Core.Models.Qyl.Common.Errors.NotFoundError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.Models.ServiceDetails>(requestInfo, global::Qyl.Core.Models.ServiceDetails.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
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
                public global::Qyl.Core.V1.Services.Item.WithServiceNameItemRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Services.Item.WithServiceNameItemRequestBuilder(rawUrl, RequestAdapter);
        }
    }
}
#pragma warning restore CS0618
