from enum import Enum

class CicdPipelineStatus(str, Enum):
    Pending = "pending",
    Running = "running",
    Success = "success",
    Failed = "failed",
    Cancelled = "cancelled",
    Skipped = "skipped",

