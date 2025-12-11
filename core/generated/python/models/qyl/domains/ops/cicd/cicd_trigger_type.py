from enum import Enum

class CicdTriggerType(str, Enum):
    Push = "push",
    Pull_request = "pull_request",
    Manual = "manual",
    Schedule = "schedule",
    Api = "api",
    Webhook = "webhook",
    Dependency = "dependency",
    Tag = "tag",
    Release = "release",

