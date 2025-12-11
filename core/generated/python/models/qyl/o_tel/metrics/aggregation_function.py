from enum import Enum

class AggregationFunction(str, Enum):
    Sum = "sum",
    Avg = "avg",
    Min = "min",
    Max = "max",
    Count = "count",
    Last = "last",
    Rate = "rate",
    Increase = "increase",

