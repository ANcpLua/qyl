
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum ErrorStatus
    {
        [EnumMember(Value = "new")]
        #pragma warning disable CS1591
        New,
        #pragma warning restore CS1591
        [EnumMember(Value = "acknowledged")]
        #pragma warning disable CS1591
        Acknowledged,
        #pragma warning restore CS1591
        [EnumMember(Value = "in_progress")]
        #pragma warning disable CS1591
        In_progress,
        #pragma warning restore CS1591
        [EnumMember(Value = "resolved")]
        #pragma warning disable CS1591
        Resolved,
        #pragma warning restore CS1591
        [EnumMember(Value = "ignored")]
        #pragma warning disable CS1591
        Ignored,
        #pragma warning restore CS1591
        [EnumMember(Value = "regressed")]
        #pragma warning disable CS1591
        Regressed,
        #pragma warning restore CS1591
        [EnumMember(Value = "wont_fix")]
        #pragma warning disable CS1591
        Wont_fix,
        #pragma warning restore CS1591
    }
}
