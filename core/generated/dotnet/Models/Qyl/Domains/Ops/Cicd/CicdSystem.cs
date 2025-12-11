
using System.Runtime.Serialization;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Cicd
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public enum CicdSystem
    {
        [EnumMember(Value = "github_actions")]
        #pragma warning disable CS1591
        Github_actions,
        #pragma warning restore CS1591
        [EnumMember(Value = "gitlab_ci")]
        #pragma warning disable CS1591
        Gitlab_ci,
        #pragma warning restore CS1591
        [EnumMember(Value = "jenkins")]
        #pragma warning disable CS1591
        Jenkins,
        #pragma warning restore CS1591
        [EnumMember(Value = "azure_devops")]
        #pragma warning disable CS1591
        Azure_devops,
        #pragma warning restore CS1591
        [EnumMember(Value = "circleci")]
        #pragma warning disable CS1591
        Circleci,
        #pragma warning restore CS1591
        [EnumMember(Value = "travis_ci")]
        #pragma warning disable CS1591
        Travis_ci,
        #pragma warning restore CS1591
        [EnumMember(Value = "bitbucket_pipelines")]
        #pragma warning disable CS1591
        Bitbucket_pipelines,
        #pragma warning restore CS1591
        [EnumMember(Value = "teamcity")]
        #pragma warning disable CS1591
        Teamcity,
        #pragma warning restore CS1591
        [EnumMember(Value = "bamboo")]
        #pragma warning disable CS1591
        Bamboo,
        #pragma warning restore CS1591
        [EnumMember(Value = "drone_ci")]
        #pragma warning disable CS1591
        Drone_ci,
        #pragma warning restore CS1591
        [EnumMember(Value = "buildkite")]
        #pragma warning disable CS1591
        Buildkite,
        #pragma warning restore CS1591
        [EnumMember(Value = "tekton")]
        #pragma warning disable CS1591
        Tekton,
        #pragma warning restore CS1591
        [EnumMember(Value = "argocd")]
        #pragma warning disable CS1591
        Argocd,
        #pragma warning restore CS1591
        [EnumMember(Value = "flux")]
        #pragma warning disable CS1591
        Flux,
        #pragma warning restore CS1591
        [EnumMember(Value = "spinnaker")]
        #pragma warning disable CS1591
        Spinnaker,
        #pragma warning restore CS1591
        [EnumMember(Value = "other")]
        #pragma warning disable CS1591
        Other,
        #pragma warning restore CS1591
    }
}
