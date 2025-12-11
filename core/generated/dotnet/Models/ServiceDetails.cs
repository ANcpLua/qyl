
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Common;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ServiceDetails : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? AvgLatencyMs { get; set; }
                public double? ErrorRate { get; set; }
                public int? InstanceCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Common.InstrumentationScope>? InstrumentationLibraries { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Common.InstrumentationScope> InstrumentationLibraries { get; set; }
#endif
                public DateTimeOffset? LastSeen { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Name { get; set; }
#nullable restore
#else
        public string Name { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? NamespaceName { get; set; }
#nullable restore
#else
        public string NamespaceName { get; set; }
#endif
                public double? P99LatencyMs { get; set; }
                public double? RequestRate { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject>? ResourceAttributes { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject> ResourceAttributes { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Version { get; set; }
#nullable restore
#else
        public string Version { get; set; }
#endif
                public ServiceDetails()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.ServiceDetails CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.ServiceDetails();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "avg_latency_ms", n => { AvgLatencyMs = n.GetDoubleValue(); } },
                { "error_rate", n => { ErrorRate = n.GetDoubleValue(); } },
                { "instance_count", n => { InstanceCount = n.GetIntValue(); } },
                { "instrumentation_libraries", n => { InstrumentationLibraries = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.InstrumentationScope>(global::Qyl.Core.Models.Qyl.Common.InstrumentationScope.CreateFromDiscriminatorValue)?.AsList(); } },
                { "last_seen", n => { LastSeen = n.GetDateTimeOffsetValue(); } },
                { "name", n => { Name = n.GetStringValue(); } },
                { "namespace_name", n => { NamespaceName = n.GetStringValue(); } },
                { "p99_latency_ms", n => { P99LatencyMs = n.GetDoubleValue(); } },
                { "request_rate", n => { RequestRate = n.GetDoubleValue(); } },
                { "resource_attributes", n => { ResourceAttributes = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>(global::Qyl.Core.Models.Qyl.Common.AttributeObject.CreateFromDiscriminatorValue)?.AsList(); } },
                { "version", n => { Version = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteDoubleValue("avg_latency_ms", AvgLatencyMs);
            writer.WriteDoubleValue("error_rate", ErrorRate);
            writer.WriteIntValue("instance_count", InstanceCount);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.InstrumentationScope>("instrumentation_libraries", InstrumentationLibraries);
            writer.WriteDateTimeOffsetValue("last_seen", LastSeen);
            writer.WriteStringValue("name", Name);
            writer.WriteStringValue("namespace_name", NamespaceName);
            writer.WriteDoubleValue("p99_latency_ms", P99LatencyMs);
            writer.WriteDoubleValue("request_rate", RequestRate);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>("resource_attributes", ResourceAttributes);
            writer.WriteStringValue("version", Version);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
