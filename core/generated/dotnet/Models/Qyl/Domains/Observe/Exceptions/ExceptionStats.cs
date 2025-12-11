
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Exceptions
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ExceptionStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionServiceStats>? ByService { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionServiceStats> ByService { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTypeStats>? ByType { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTypeStats> ByType { get; set; }
#endif
                public long? TotalCount { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTrend? Trend { get; set; }
                public int? UniqueTypes { get; set; }
                public ExceptionStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "by_service", n => { ByService = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionServiceStats>(global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionServiceStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "by_type", n => { ByType = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTypeStats>(global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTypeStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "total_count", n => { TotalCount = n.GetLongValue(); } },
                { "trend", n => { Trend = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTrend>(); } },
                { "unique_types", n => { UniqueTypes = n.GetIntValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionServiceStats>("by_service", ByService);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTypeStats>("by_type", ByType);
            writer.WriteLongValue("total_count", TotalCount);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTrend>("trend", Trend);
            writer.WriteIntValue("unique_types", UniqueTypes);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
