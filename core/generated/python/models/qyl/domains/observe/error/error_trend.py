from enum import Enum

class ErrorTrend(str, Enum):
    Increasing = "increasing",
    Decreasing = "decreasing",
    Stable = "stable",
    Spike = "spike",

