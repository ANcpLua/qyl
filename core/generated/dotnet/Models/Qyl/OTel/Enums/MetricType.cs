
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.OTel.Enums
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum MetricType
    {
        [EnumMember(Value = "gauge")]
        #pragma warning disable CS1591
        Gauge,
        #pragma warning restore CS1591
        [EnumMember(Value = "sum")]
        #pragma warning disable CS1591
        Sum,
        #pragma warning restore CS1591
        [EnumMember(Value = "histogram")]
        #pragma warning disable CS1591
        Histogram,
        #pragma warning restore CS1591
        [EnumMember(Value = "exponential_histogram")]
        #pragma warning disable CS1591
        Exponential_histogram,
        #pragma warning restore CS1591
        [EnumMember(Value = "summary")]
        #pragma warning disable CS1591
        Summary,
        #pragma warning restore CS1591
    }
}
