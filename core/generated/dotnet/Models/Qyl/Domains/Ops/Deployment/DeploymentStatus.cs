
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Deployment
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum DeploymentStatus
    {
        [EnumMember(Value = "pending")]
        #pragma warning disable CS1591
        Pending,
        #pragma warning restore CS1591
        [EnumMember(Value = "in_progress")]
        #pragma warning disable CS1591
        In_progress,
        #pragma warning restore CS1591
        [EnumMember(Value = "success")]
        #pragma warning disable CS1591
        Success,
        #pragma warning restore CS1591
        [EnumMember(Value = "failed")]
        #pragma warning disable CS1591
        Failed,
        #pragma warning restore CS1591
        [EnumMember(Value = "rolled_back")]
        #pragma warning disable CS1591
        Rolled_back,
        #pragma warning restore CS1591
        [EnumMember(Value = "cancelled")]
        #pragma warning disable CS1591
        Cancelled,
        #pragma warning restore CS1591
    }
}
