
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum ErrorCategory
    {
        [EnumMember(Value = "client")]
        #pragma warning disable CS1591
        Client,
        #pragma warning restore CS1591
        [EnumMember(Value = "server")]
        #pragma warning disable CS1591
        Server,
        #pragma warning restore CS1591
        [EnumMember(Value = "network")]
        #pragma warning disable CS1591
        Network,
        #pragma warning restore CS1591
        [EnumMember(Value = "timeout")]
        #pragma warning disable CS1591
        Timeout,
        #pragma warning restore CS1591
        [EnumMember(Value = "validation")]
        #pragma warning disable CS1591
        Validation,
        #pragma warning restore CS1591
        [EnumMember(Value = "authentication")]
        #pragma warning disable CS1591
        Authentication,
        #pragma warning restore CS1591
        [EnumMember(Value = "authorization")]
        #pragma warning disable CS1591
        Authorization,
        #pragma warning restore CS1591
        [EnumMember(Value = "rate_limit")]
        #pragma warning disable CS1591
        Rate_limit,
        #pragma warning restore CS1591
        [EnumMember(Value = "not_found")]
        #pragma warning disable CS1591
        Not_found,
        #pragma warning restore CS1591
        [EnumMember(Value = "conflict")]
        #pragma warning disable CS1591
        Conflict,
        #pragma warning restore CS1591
        [EnumMember(Value = "internal")]
        #pragma warning disable CS1591
        Internal,
        #pragma warning restore CS1591
        [EnumMember(Value = "external")]
        #pragma warning disable CS1591
        External,
        #pragma warning restore CS1591
        [EnumMember(Value = "database")]
        #pragma warning disable CS1591
        Database,
        #pragma warning restore CS1591
        [EnumMember(Value = "configuration")]
        #pragma warning disable CS1591
        Configuration,
        #pragma warning restore CS1591
        [EnumMember(Value = "unknown")]
        #pragma warning disable CS1591
        Unknown,
        #pragma warning restore CS1591
    }
}
