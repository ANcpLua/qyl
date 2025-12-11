from enum import Enum

class ExceptionStatus(str, Enum):
    New = "new",
    Investigating = "investigating",
    In_progress = "in_progress",
    Resolved = "resolved",
    Ignored = "ignored",
    Regressed = "regressed",

