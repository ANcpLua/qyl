from enum import Enum

class HealthStatus(str, Enum):
    Healthy = "healthy",
    Degraded = "degraded",
    Unhealthy = "unhealthy",

