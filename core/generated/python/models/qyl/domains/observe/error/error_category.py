from enum import Enum

class ErrorCategory(str, Enum):
    Client = "client",
    Server = "server",
    Network = "network",
    Timeout = "timeout",
    Validation = "validation",
    Authentication = "authentication",
    Authorization = "authorization",
    Rate_limit = "rate_limit",
    Not_found = "not_found",
    Conflict = "conflict",
    Internal = "internal",
    External = "external",
    Database = "database",
    Configuration = "configuration",
    Unknown = "unknown",

