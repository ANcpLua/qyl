from enum import Enum

class SessionState(str, Enum):
    Active = "active",
    Idle = "idle",
    Ended = "ended",
    Timed_out = "timed_out",
    Invalidated = "invalidated",

