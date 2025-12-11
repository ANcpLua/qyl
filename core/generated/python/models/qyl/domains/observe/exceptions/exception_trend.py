from enum import Enum

class ExceptionTrend(str, Enum):
    Up = "up",
    Down = "down",
    Stable = "stable",

