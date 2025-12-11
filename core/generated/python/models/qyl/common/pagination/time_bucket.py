from enum import Enum

class TimeBucket(str, Enum):
    Onem = "1m",
    Fivem = "5m",
    OneFivem = "15m",
    Oneh = "1h",
    Oned = "1d",
    Onew = "1w",
    Auto = "auto",

