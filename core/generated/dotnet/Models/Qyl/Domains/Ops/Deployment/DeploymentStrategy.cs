
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Deployment
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum DeploymentStrategy
    {
        [EnumMember(Value = "rolling")]
        #pragma warning disable CS1591
        Rolling,
        #pragma warning restore CS1591
        [EnumMember(Value = "blue_green")]
        #pragma warning disable CS1591
        Blue_green,
        #pragma warning restore CS1591
        [EnumMember(Value = "canary")]
        #pragma warning disable CS1591
        Canary,
        #pragma warning restore CS1591
        [EnumMember(Value = "recreate")]
        #pragma warning disable CS1591
        Recreate,
        #pragma warning restore CS1591
        [EnumMember(Value = "ab_test")]
        #pragma warning disable CS1591
        Ab_test,
        #pragma warning restore CS1591
        [EnumMember(Value = "shadow")]
        #pragma warning disable CS1591
        Shadow,
        #pragma warning restore CS1591
        [EnumMember(Value = "feature_flag")]
        #pragma warning disable CS1591
        Feature_flag,
        #pragma warning restore CS1591
    }
}
