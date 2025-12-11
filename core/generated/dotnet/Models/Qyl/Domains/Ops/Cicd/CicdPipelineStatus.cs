
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Cicd
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum CicdPipelineStatus
    {
        [EnumMember(Value = "pending")]
        #pragma warning disable CS1591
        Pending,
        #pragma warning restore CS1591
        [EnumMember(Value = "running")]
        #pragma warning disable CS1591
        Running,
        #pragma warning restore CS1591
        [EnumMember(Value = "success")]
        #pragma warning disable CS1591
        Success,
        #pragma warning restore CS1591
        [EnumMember(Value = "failed")]
        #pragma warning disable CS1591
        Failed,
        #pragma warning restore CS1591
        [EnumMember(Value = "cancelled")]
        #pragma warning disable CS1591
        Cancelled,
        #pragma warning restore CS1591
        [EnumMember(Value = "skipped")]
        #pragma warning disable CS1591
        Skipped,
        #pragma warning restore CS1591
    }
}
