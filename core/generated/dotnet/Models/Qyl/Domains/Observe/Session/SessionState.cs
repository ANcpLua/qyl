
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Session
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum SessionState
    {
        [EnumMember(Value = "active")]
        #pragma warning disable CS1591
        Active,
        #pragma warning restore CS1591
        [EnumMember(Value = "idle")]
        #pragma warning disable CS1591
        Idle,
        #pragma warning restore CS1591
        [EnumMember(Value = "ended")]
        #pragma warning disable CS1591
        Ended,
        #pragma warning restore CS1591
        [EnumMember(Value = "timed_out")]
        #pragma warning disable CS1591
        Timed_out,
        #pragma warning restore CS1591
        [EnumMember(Value = "invalidated")]
        #pragma warning disable CS1591
        Invalidated,
        #pragma warning restore CS1591
    }
}
