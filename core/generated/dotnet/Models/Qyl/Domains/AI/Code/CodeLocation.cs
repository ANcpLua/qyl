
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.AI.Code
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class CodeLocation : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ClassName { get; set; }
#nullable restore
#else
        public string ClassName { get; set; }
#endif
                public int? ColumnNumber { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Filepath { get; set; }
#nullable restore
#else
        public string Filepath { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? FunctionName { get; set; }
#nullable restore
#else
        public string FunctionName { get; set; }
#endif
                public int? LineNumber { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Namespace { get; set; }
#nullable restore
#else
        public string Namespace { get; set; }
#endif
                public CodeLocation()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.AI.Code.CodeLocation CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.AI.Code.CodeLocation();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "class_name", n => { ClassName = n.GetStringValue(); } },
                { "column_number", n => { ColumnNumber = n.GetIntValue(); } },
                { "filepath", n => { Filepath = n.GetStringValue(); } },
                { "function_name", n => { FunctionName = n.GetStringValue(); } },
                { "line_number", n => { LineNumber = n.GetIntValue(); } },
                { "namespace", n => { Namespace = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("class_name", ClassName);
            writer.WriteIntValue("column_number", ColumnNumber);
            writer.WriteStringValue("filepath", Filepath);
            writer.WriteStringValue("function_name", FunctionName);
            writer.WriteIntValue("line_number", LineNumber);
            writer.WriteStringValue("namespace", Namespace);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
