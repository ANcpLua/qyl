from enum import Enum

class CicdEventName(str, Enum):
    CicdPipelineStart = "cicd.pipeline.start",
    CicdPipelineEnd = "cicd.pipeline.end",
    CicdTaskStart = "cicd.task.start",
    CicdTaskEnd = "cicd.task.end",
    CicdDeploymentStart = "cicd.deployment.start",
    CicdDeploymentEnd = "cicd.deployment.end",

