
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Streaming
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum StreamEventType
    {
        [EnumMember(Value = "traces")]
        #pragma warning disable CS1591
        Traces,
        #pragma warning restore CS1591
        [EnumMember(Value = "spans")]
        #pragma warning disable CS1591
        Spans,
        #pragma warning restore CS1591
        [EnumMember(Value = "logs")]
        #pragma warning disable CS1591
        Logs,
        #pragma warning restore CS1591
        [EnumMember(Value = "metrics")]
        #pragma warning disable CS1591
        Metrics,
        #pragma warning restore CS1591
        [EnumMember(Value = "exceptions")]
        #pragma warning disable CS1591
        Exceptions,
        #pragma warning restore CS1591
        [EnumMember(Value = "deployments")]
        #pragma warning disable CS1591
        Deployments,
        #pragma warning restore CS1591
        [EnumMember(Value = "all")]
        #pragma warning disable CS1591
        All,
        #pragma warning restore CS1591
    }
}
