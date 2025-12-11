from enum import Enum

class DeploymentStrategy(str, Enum):
    Rolling = "rolling",
    Blue_green = "blue_green",
    Canary = "canary",
    Recreate = "recreate",
    Ab_test = "ab_test",
    Shadow = "shadow",
    Feature_flag = "feature_flag",

