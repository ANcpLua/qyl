// =============================================================================
// QYL v2.0 - OS Semantic Conventions (OTel v1.38)
// =============================================================================
// Operating system resource attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Infra.Os;

// =============================================================================
// OS Resource Attributes
// =============================================================================

@doc("Operating system resource attributes")
model OsAttributes {
  @doc("Operating system type")
  @encodedName("application/json", "os.type")
  type: OsType;

  @doc("Operating system description")
  @encodedName("application/json", "os.description")
  description?: string;

  @doc("Operating system name")
  @encodedName("application/json", "os.name")
  name?: string;

  @doc("Operating system version")
  @encodedName("application/json", "os.version")
  version?: string;

  @doc("Operating system build ID")
  @encodedName("application/json", "os.build_id")
  buildId?: string;
}

// =============================================================================
// OS Type Enumeration
// =============================================================================

@doc("Operating system types")
enum OsType {
  @doc("Microsoft Windows")
  windows: "windows",

  @doc("Linux")
  linux: "linux",

  @doc("Apple Darwin (macOS)")
  darwin: "darwin",

  @doc("FreeBSD")
  freebsd: "freebsd",

  @doc("NetBSD")
  netbsd: "netbsd",

  @doc("OpenBSD")
  openbsd: "openbsd",

  @doc("DragonFly BSD")
  dragonflybsd: "dragonflybsd",

  @doc("HP-UX")
  hpux: "hpux",

  @doc("AIX")
  aix: "aix",

  @doc("Oracle Solaris")
  solaris: "solaris",

  @doc("IBM z/OS")
  zos: "z_os",
}

// =============================================================================
// OS Entity
// =============================================================================

@doc("OS entity for resource detection")
model OsEntity {
  @doc("OS type")
  @key
  type: OsType;

  @doc("OS name")
  name: string;

  @doc("OS version")
  version?: string;

  @doc("OS description")
  description?: string;

  @doc("Build ID")
  @encodedName("application/json", "build_id")
  buildId?: string;

  @doc("Kernel version")
  @encodedName("application/json", "kernel_version")
  kernelVersion?: string;

  @doc("Architecture")
  arch?: HostArch;
}

@doc("Host architectures")
enum HostArch {
  @doc("AMD64/x86_64")
  amd64: "amd64",

  @doc("ARM32")
  arm32: "arm32",

  @doc("ARM64/AArch64")
  arm64: "arm64",

  @doc("Intel IA-64")
  ia64: "ia64",

  @doc("PowerPC 32-bit")
  ppc32: "ppc32",

  @doc("PowerPC 64-bit")
  ppc64: "ppc64",

  @doc("IBM S/390x")
  s390x: "s390x",

  @doc("x86 32-bit")
  x86: "x86",
}
