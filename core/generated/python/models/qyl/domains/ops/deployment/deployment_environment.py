from enum import Enum

class DeploymentEnvironment(str, Enum):
    Development = "development",
    Testing = "testing",
    Staging = "staging",
    Production = "production",
    Preview = "preview",
    Canary = "canary",

