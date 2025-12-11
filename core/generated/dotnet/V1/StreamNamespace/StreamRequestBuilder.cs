
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.V1.StreamNamespace.Deployments;
using Qyl.Core.V1.StreamNamespace.Events;
using Qyl.Core.V1.StreamNamespace.Exceptions;
using Qyl.Core.V1.StreamNamespace.Logs;
using Qyl.Core.V1.StreamNamespace.Metrics;
using Qyl.Core.V1.StreamNamespace.Traces;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;
namespace Qyl.Core.V1.StreamNamespace
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class StreamRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder Deployments
        {
            get => new global::Qyl.Core.V1.StreamNamespace.Deployments.DeploymentsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.StreamNamespace.Events.EventsRequestBuilder Events
        {
            get => new global::Qyl.Core.V1.StreamNamespace.Events.EventsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.StreamNamespace.Exceptions.ExceptionsRequestBuilder Exceptions
        {
            get => new global::Qyl.Core.V1.StreamNamespace.Exceptions.ExceptionsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.StreamNamespace.Logs.LogsRequestBuilder Logs
        {
            get => new global::Qyl.Core.V1.StreamNamespace.Logs.LogsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.StreamNamespace.Metrics.MetricsRequestBuilder Metrics
        {
            get => new global::Qyl.Core.V1.StreamNamespace.Metrics.MetricsRequestBuilder(PathParameters, RequestAdapter);
        }
                public global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder Traces
        {
            get => new global::Qyl.Core.V1.StreamNamespace.Traces.TracesRequestBuilder(PathParameters, RequestAdapter);
        }
                public StreamRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream", pathParameters)
        {
        }
                public StreamRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream", rawUrl)
        {
        }
    }
}
#pragma warning restore CS0618
