
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.V1.Deployments.Metrics.Dora;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;
namespace Qyl.Core.V1.Deployments.Metrics
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class MetricsRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder Dora
        {
            get => new global::Qyl.Core.V1.Deployments.Metrics.Dora.DoraRequestBuilder(PathParameters, RequestAdapter);
        }
                public MetricsRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments/metrics", pathParameters)
        {
        }
                public MetricsRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/deployments/metrics", rawUrl)
        {
        }
    }
}
#pragma warning restore CS0618
