
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.Models.Qyl.Common.Errors;
using Qyl.Core.Models.Qyl.Domains.Ops.Deployment;
using Qyl.Core.Models;
using Qyl.Core.V1.Deployments.Item;
using Qyl.Core.V1.Deployments.Metrics;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
namespace Qyl.Core.V1.Deployments
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class DeploymentsRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.Deployments.Metrics.MetricsRequestBuilder Metrics
        {
            get => new global::Qyl.Core.V1.Deployments.Metrics.MetricsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Deployments.Item.WithDeploymentItemRequestBuilder this[string position]
        {
            get
            {
                var urlTplParams = new Dictionary<string, object>(PathParameters);
                urlTplParams.Add("deploymentId", position);
                return new global::Qyl.Core.V1.Deployments.Item.WithDeploymentItemRequestBuilder(urlTplParams, RequestAdapter);
            }
        }
                public DeploymentsRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments{?cursor,endTime,environment,limit,serviceName,startTime,status}", pathParameters)
        {
        }
                public DeploymentsRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments{?cursor,endTime,environment,limit,serviceName,startTime,status}", rawUrl)
        {
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.V1.Deployments.DeploymentsGetResponse?> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.V1.Deployments.DeploymentsGetResponse> GetAsync(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "400", global::Qyl.Core.Models.Qyl.Common.Errors.ValidationError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.V1.Deployments.DeploymentsGetResponse>(requestInfo, global::Qyl.Core.V1.Deployments.DeploymentsGetResponse.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public async Task<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity?> PostAsync(global::Qyl.Core.Models.DeploymentCreate body, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
#else
        public async Task<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity> PostAsync(global::Qyl.Core.Models.DeploymentCreate body, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#endif
            if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));
            var requestInfo = ToPostRequestInformation(body, requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "400", global::Qyl.Core.Models.Qyl.Common.Errors.ValidationError.CreateFromDiscriminatorValue },
                { "500", global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError.CreateFromDiscriminatorValue },
            };
            return await RequestAdapter.SendAsync<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity>(requestInfo, global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity.CreateFromDiscriminatorValue, errorMapping, cancellationToken).ConfigureAwait(false);
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToGetRequestInformation(Action<RequestConfiguration<global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder.DeploymentsRequestBuilderGetQueryParameters>> requestConfiguration = default)
        {
#endif
            var requestInfo = new RequestInformation(Method.GET, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            return requestInfo;
        }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public RequestInformation ToPostRequestInformation(global::Qyl.Core.Models.DeploymentCreate body, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default)
        {
#nullable restore
#else
        public RequestInformation ToPostRequestInformation(global::Qyl.Core.Models.DeploymentCreate body, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default)
        {
#endif
            if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));
            var requestInfo = new RequestInformation(Method.POST, UrlTemplate, PathParameters);
            requestInfo.Configure(requestConfiguration);
            requestInfo.Headers.TryAdd("Accept", "application/json");
            requestInfo.SetContentFromParsable(RequestAdapter, "application/json", body);
            return requestInfo;
        }
                public global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder WithUrl(string rawUrl)
        {
            return new global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder(rawUrl, RequestAdapter);
        }
                [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
        public partial class DeploymentsRequestBuilderGetQueryParameters
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
                        [QueryParameter("environment")]
            public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment? Environment { get; set; }
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
            public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStatus? Status { get; set; }
        }
    }
}
#pragma warning restore CS0618
