from enum import Enum

class ErrorStatus(str, Enum):
    New = "new",
    Acknowledged = "acknowledged",
    In_progress = "in_progress",
    Resolved = "resolved",
    Ignored = "ignored",
    Regressed = "regressed",
    Wont_fix = "wont_fix",

