from enum import Enum

class StreamEventType(str, Enum):
    Traces = "traces",
    Spans = "spans",
    Logs = "logs",
    Metrics = "metrics",
    Exceptions = "exceptions",
    Deployments = "deployments",
    All = "all",

