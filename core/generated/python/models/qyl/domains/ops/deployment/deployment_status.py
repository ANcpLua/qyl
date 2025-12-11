from enum import Enum

class DeploymentStatus(str, Enum):
    Pending = "pending",
    In_progress = "in_progress",
    Success = "success",
    Failed = "failed",
    Rolled_back = "rolled_back",
    Cancelled = "cancelled",

