
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ErrorStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategoryStats>? ByCategory { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategoryStats> ByCategory { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorServiceStats>? ByService { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorServiceStats> ByService { get; set; }
#endif
                public double? ErrorRate { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTypeStats>? TopErrors { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTypeStats> TopErrors { get; set; }
#endif
                public long? TotalCount { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTrend? Trend { get; set; }
                public int? UniqueTypes { get; set; }
                public ErrorStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "by_category", n => { ByCategory = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategoryStats>(global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategoryStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "by_service", n => { ByService = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorServiceStats>(global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorServiceStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "error_rate", n => { ErrorRate = n.GetDoubleValue(); } },
                { "top_errors", n => { TopErrors = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTypeStats>(global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTypeStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "total_count", n => { TotalCount = n.GetLongValue(); } },
                { "trend", n => { Trend = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTrend>(); } },
                { "unique_types", n => { UniqueTypes = n.GetIntValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategoryStats>("by_category", ByCategory);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorServiceStats>("by_service", ByService);
            writer.WriteDoubleValue("error_rate", ErrorRate);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTypeStats>("top_errors", TopErrors);
            writer.WriteLongValue("total_count", TotalCount);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTrend>("trend", Trend);
            writer.WriteIntValue("unique_types", UniqueTypes);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
