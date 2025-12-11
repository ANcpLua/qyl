
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Cicd
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum CicdEventName
    {
        [EnumMember(Value = "cicd.pipeline.start")]
        #pragma warning disable CS1591
        CicdPipelineStart,
        #pragma warning restore CS1591
        [EnumMember(Value = "cicd.pipeline.end")]
        #pragma warning disable CS1591
        CicdPipelineEnd,
        #pragma warning restore CS1591
        [EnumMember(Value = "cicd.task.start")]
        #pragma warning disable CS1591
        CicdTaskStart,
        #pragma warning restore CS1591
        [EnumMember(Value = "cicd.task.end")]
        #pragma warning disable CS1591
        CicdTaskEnd,
        #pragma warning restore CS1591
        [EnumMember(Value = "cicd.deployment.start")]
        #pragma warning disable CS1591
        CicdDeploymentStart,
        #pragma warning restore CS1591
        [EnumMember(Value = "cicd.deployment.end")]
        #pragma warning disable CS1591
        CicdDeploymentEnd,
        #pragma warning restore CS1591
    }
}
