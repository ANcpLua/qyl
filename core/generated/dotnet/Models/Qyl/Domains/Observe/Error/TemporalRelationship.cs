
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum TemporalRelationship
    {
        [EnumMember(Value = "concurrent")]
        #pragma warning disable CS1591
        Concurrent,
        #pragma warning restore CS1591
        [EnumMember(Value = "precedes")]
        #pragma warning disable CS1591
        Precedes,
        #pragma warning restore CS1591
        [EnumMember(Value = "follows")]
        #pragma warning disable CS1591
        Follows,
        #pragma warning restore CS1591
        [EnumMember(Value = "unrelated")]
        #pragma warning disable CS1591
        Unrelated,
        #pragma warning restore CS1591
    }
}
