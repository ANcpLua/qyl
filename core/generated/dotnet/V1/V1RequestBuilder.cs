
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.V1.Deployments;
using Qyl.Core.V1.Errors;
using Qyl.Core.V1.Exceptions;
using Qyl.Core.V1.Logs;
using Qyl.Core.V1.Metrics;
using Qyl.Core.V1.Pipelines;
using Qyl.Core.V1.Services;
using Qyl.Core.V1.Sessions;
using Qyl.Core.V1.StreamNamespace;
using Qyl.Core.V1.Traces;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;
namespace Qyl.Core.V1
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class V1RequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder Deployments
        {
            get => new global::Qyl.Core.V1.Deployments.DeploymentsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Errors.ErrorsRequestBuilder Errors
        {
            get => new global::Qyl.Core.V1.Errors.ErrorsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Exceptions.ExceptionsRequestBuilder Exceptions
        {
            get => new global::Qyl.Core.V1.Exceptions.ExceptionsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Logs.LogsRequestBuilder Logs
        {
            get => new global::Qyl.Core.V1.Logs.LogsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Metrics.MetricsRequestBuilder Metrics
        {
            get => new global::Qyl.Core.V1.Metrics.MetricsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder Pipelines
        {
            get => new global::Qyl.Core.V1.Pipelines.PipelinesRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Services.ServicesRequestBuilder Services
        {
            get => new global::Qyl.Core.V1.Services.ServicesRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Sessions.SessionsRequestBuilder Sessions
        {
            get => new global::Qyl.Core.V1.Sessions.SessionsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.StreamNamespace.StreamRequestBuilder Stream
        {
            get => new global::Qyl.Core.V1.StreamNamespace.StreamRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.Traces.TracesRequestBuilder Traces
        {
            get => new global::Qyl.Core.V1.Traces.TracesRequestBuilder(PathParameters, RequestAdapter);
        }
                public V1RequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1", pathParameters)
        {
        }
                public V1RequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1", rawUrl)
        {
        }
    }
}
#pragma warning restore CS0618
