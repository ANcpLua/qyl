from enum import Enum

class DeviceType(str, Enum):
    Desktop = "desktop",
    Mobile = "mobile",
    Tablet = "tablet",
    Tv = "tv",
    Console = "console",
    Wearable = "wearable",
    Iot = "iot",
    Bot = "bot",
    Unknown = "unknown",

