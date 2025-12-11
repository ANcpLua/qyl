
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.OTel.Metrics
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum AggregationFunction
    {
        [EnumMember(Value = "sum")]
        #pragma warning disable CS1591
        Sum,
        #pragma warning restore CS1591
        [EnumMember(Value = "avg")]
        #pragma warning disable CS1591
        Avg,
        #pragma warning restore CS1591
        [EnumMember(Value = "min")]
        #pragma warning disable CS1591
        Min,
        #pragma warning restore CS1591
        [EnumMember(Value = "max")]
        #pragma warning disable CS1591
        Max,
        #pragma warning restore CS1591
        [EnumMember(Value = "count")]
        #pragma warning disable CS1591
        Count,
        #pragma warning restore CS1591
        [EnumMember(Value = "last")]
        #pragma warning disable CS1591
        Last,
        #pragma warning restore CS1591
        [EnumMember(Value = "rate")]
        #pragma warning disable CS1591
        Rate,
        #pragma warning restore CS1591
        [EnumMember(Value = "increase")]
        #pragma warning disable CS1591
        Increase,
        #pragma warning restore CS1591
    }
}
