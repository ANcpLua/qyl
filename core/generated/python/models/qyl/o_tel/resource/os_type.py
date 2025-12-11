from enum import Enum

class OsType(str, Enum):
    Windows = "windows",
    Linux = "linux",
    Darwin = "darwin",
    Freebsd = "freebsd",
    Netbsd = "netbsd",
    Openbsd = "openbsd",
    Dragonflybsd = "dragonflybsd",
    Hpux = "hpux",
    Aix = "aix",
    Solaris = "solaris",
    Z_os = "z_os",

