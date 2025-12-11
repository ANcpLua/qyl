
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.AI.Code
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class StackTrace : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackFrame>? Frames { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackFrame> Frames { get; set; }
#endif
                public int? TotalFrames { get; set; }
                public bool? Truncated { get; set; }
                public StackTrace()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackTrace CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackTrace();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "frames", n => { Frames = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackFrame>(global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackFrame.CreateFromDiscriminatorValue)?.AsList(); } },
                { "total_frames", n => { TotalFrames = n.GetIntValue(); } },
                { "truncated", n => { Truncated = n.GetBoolValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackFrame>("frames", Frames);
            writer.WriteIntValue("total_frames", TotalFrames);
            writer.WriteBoolValue("truncated", Truncated);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
