
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Common.Pagination
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum TimeBucket
    {
        [EnumMember(Value = "1m")]
        #pragma warning disable CS1591
        Onem,
        #pragma warning restore CS1591
        [EnumMember(Value = "5m")]
        #pragma warning disable CS1591
        Fivem,
        #pragma warning restore CS1591
        [EnumMember(Value = "15m")]
        #pragma warning disable CS1591
        OneFivem,
        #pragma warning restore CS1591
        [EnumMember(Value = "1h")]
        #pragma warning disable CS1591
        Oneh,
        #pragma warning restore CS1591
        [EnumMember(Value = "1d")]
        #pragma warning disable CS1591
        Oned,
        #pragma warning restore CS1591
        [EnumMember(Value = "1w")]
        #pragma warning disable CS1591
        Onew,
        #pragma warning restore CS1591
        [EnumMember(Value = "auto")]
        #pragma warning disable CS1591
        Auto,
        #pragma warning restore CS1591
    }
}
