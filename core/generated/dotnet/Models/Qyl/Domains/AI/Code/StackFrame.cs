
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.AI.Code
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class StackFrame : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public int? Index { get; set; }
                public bool? IsNative { get; set; }
                public bool? IsUserCode { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.AI.Code.CodeLocation? Location { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.AI.Code.CodeLocation Location { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ModuleName { get; set; }
#nullable restore
#else
        public string ModuleName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ModuleVersion { get; set; }
#nullable restore
#else
        public string ModuleVersion { get; set; }
#endif
                public StackFrame()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackFrame CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackFrame();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "index", n => { Index = n.GetIntValue(); } },
                { "is_native", n => { IsNative = n.GetBoolValue(); } },
                { "is_user_code", n => { IsUserCode = n.GetBoolValue(); } },
                { "location", n => { Location = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.AI.Code.CodeLocation>(global::Qyl.Core.Models.Qyl.Domains.AI.Code.CodeLocation.CreateFromDiscriminatorValue); } },
                { "module_name", n => { ModuleName = n.GetStringValue(); } },
                { "module_version", n => { ModuleVersion = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteIntValue("index", Index);
            writer.WriteBoolValue("is_native", IsNative);
            writer.WriteBoolValue("is_user_code", IsUserCode);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.AI.Code.CodeLocation>("location", Location);
            writer.WriteStringValue("module_name", ModuleName);
            writer.WriteStringValue("module_version", ModuleVersion);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
