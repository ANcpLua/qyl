
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions;
using Qyl.Core.V1.StreamNamespace.Traces.Item.Spans;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;
namespace Qyl.Core.V1.StreamNamespace.Traces.Item
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class WithTraceItemRequestBuilder : BaseRequestBuilder
    {
                public global::Qyl.Core.V1.StreamNamespace.Traces.Item.Spans.SpansRequestBuilder Spans
        {
            get => new global::Qyl.Core.V1.StreamNamespace.Traces.Item.Spans.SpansRequestBuilder(PathParameters, RequestAdapter);
        }
                public WithTraceItemRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream/traces/{traceId}", pathParameters)
        {
        }
                public WithTraceItemRequestBuilder(string rawUrl, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/v1/stream/traces/{traceId}", rawUrl)
        {
        }
    }
}
#pragma warning restore CS0618
