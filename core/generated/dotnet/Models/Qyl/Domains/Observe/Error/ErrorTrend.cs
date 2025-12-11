
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum ErrorTrend
    {
        [EnumMember(Value = "increasing")]
        #pragma warning disable CS1591
        Increasing,
        #pragma warning restore CS1591
        [EnumMember(Value = "decreasing")]
        #pragma warning disable CS1591
        Decreasing,
        #pragma warning restore CS1591
        [EnumMember(Value = "stable")]
        #pragma warning disable CS1591
        Stable,
        #pragma warning restore CS1591
        [EnumMember(Value = "spike")]
        #pragma warning disable CS1591
        Spike,
        #pragma warning restore CS1591
    }
}
