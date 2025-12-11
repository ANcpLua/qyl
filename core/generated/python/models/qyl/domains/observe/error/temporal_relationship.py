from enum import Enum

class TemporalRelationship(str, Enum):
    Concurrent = "concurrent",
    Precedes = "precedes",
    Follows = "follows",
    Unrelated = "unrelated",

