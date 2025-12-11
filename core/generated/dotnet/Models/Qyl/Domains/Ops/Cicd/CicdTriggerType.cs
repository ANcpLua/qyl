
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Cicd
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum CicdTriggerType
    {
        [EnumMember(Value = "push")]
        #pragma warning disable CS1591
        Push,
        #pragma warning restore CS1591
        [EnumMember(Value = "pull_request")]
        #pragma warning disable CS1591
        Pull_request,
        #pragma warning restore CS1591
        [EnumMember(Value = "manual")]
        #pragma warning disable CS1591
        Manual,
        #pragma warning restore CS1591
        [EnumMember(Value = "schedule")]
        #pragma warning disable CS1591
        Schedule,
        #pragma warning restore CS1591
        [EnumMember(Value = "api")]
        #pragma warning disable CS1591
        Api,
        #pragma warning restore CS1591
        [EnumMember(Value = "webhook")]
        #pragma warning disable CS1591
        Webhook,
        #pragma warning restore CS1591
        [EnumMember(Value = "dependency")]
        #pragma warning disable CS1591
        Dependency,
        #pragma warning restore CS1591
        [EnumMember(Value = "tag")]
        #pragma warning disable CS1591
        Tag,
        #pragma warning restore CS1591
        [EnumMember(Value = "release")]
        #pragma warning disable CS1591
        Release,
        #pragma warning restore CS1591
    }
}
