from enum import Enum

class HostArch(str, Enum):
    Amd64 = "amd64",
    Arm32 = "arm32",
    Arm64 = "arm64",
    Ia64 = "ia64",
    Ppc32 = "ppc32",
    Ppc64 = "ppc64",
    S390x = "s390x",
    X86 = "x86",

