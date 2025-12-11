from enum import Enum

class MetricType(str, Enum):
    Gauge = "gauge",
    Sum = "sum",
    Histogram = "histogram",
    Exponential_histogram = "exponential_histogram",
    Summary = "summary",

