
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
namespace Qyl.Core.V1.Deployments.Item
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class WithDeploymentItemRequestBuilder : BaseRequestBuilder
    {
                public WithDeploymentItemRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments/{deploymentId}", pathParameters)
        {
        }
                public WithDeploymentItemRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments/{deploymentId}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity?> GetAsync(Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity> GetAsync(Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "404", global::Qyl.Core.Models.Qyl.Common.Errors.NotFoundError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity>(requestInfo, global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity?> PatchAsync(global::Qyl.Core.Models.DeploymentUpdate body, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity> PatchAsync(global::Qyl.Core.Models.DeploymentUpdate body, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));
            var requestInfo = ToPatchRequestInformation(body, requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "400", global::Qyl.Core.Models.Qyl.Common.Errors.ValidationError.CreateFromDiscriminatorValue },
                { "404", global::Qyl.Core.Models.Qyl.Common.Errors.NotFoundError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity>(requestInfo, global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
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
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToPatchRequestInformation(global::Qyl.Core.Models.DeploymentUpdate body, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToPatchRequestInformation(global::Qyl.Core.Models.DeploymentUpdate body, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default)
        {
#endif
            if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));
            var requestInfo = new RequestInformation(Method.PATCH, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            requestInfo.SetContentFromParsable(RequestAdapter, "application/json", body);
            return requestInfo;
        }
                public global::Qyl.Core.V1.Deployments.Item.WithDeploymentItemRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Deployments.Item.WithDeploymentItemRequestBuilder(rawUrl, RequestAdapter);
        }
    }
}
#pragma warning restore CS0618
