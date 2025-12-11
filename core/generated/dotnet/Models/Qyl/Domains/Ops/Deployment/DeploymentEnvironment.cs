
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Deployment
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum DeploymentEnvironment
    {
        [EnumMember(Value = "development")]
        #pragma warning disable CS1591
        Development,
        #pragma warning restore CS1591
        [EnumMember(Value = "testing")]
        #pragma warning disable CS1591
        Testing,
        #pragma warning restore CS1591
        [EnumMember(Value = "staging")]
        #pragma warning disable CS1591
        Staging,
        #pragma warning restore CS1591
        [EnumMember(Value = "production")]
        #pragma warning disable CS1591
        Production,
        #pragma warning restore CS1591
        [EnumMember(Value = "preview")]
        #pragma warning disable CS1591
        Preview,
        #pragma warning restore CS1591
        [EnumMember(Value = "canary")]
        #pragma warning disable CS1591
        Canary,
        #pragma warning restore CS1591
    }
}
