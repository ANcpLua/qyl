from enum import Enum

class CicdSystem(str, Enum):
    Github_actions = "github_actions",
    Gitlab_ci = "gitlab_ci",
    Jenkins = "jenkins",
    Azure_devops = "azure_devops",
    Circleci = "circleci",
    Travis_ci = "travis_ci",
    Bitbucket_pipelines = "bitbucket_pipelines",
    Teamcity = "teamcity",
    Bamboo = "bamboo",
    Drone_ci = "drone_ci",
    Buildkite = "buildkite",
    Tekton = "tekton",
    Argocd = "argocd",
    Flux = "flux",
    Spinnaker = "spinnaker",
    Other = "other",

