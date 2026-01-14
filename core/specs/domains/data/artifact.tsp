// =============================================================================
// QYL v2.0 - Artifact Semantic Conventions (OTel v1.39)
// =============================================================================
// Build artifact and package attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Data.Artifact;

// =============================================================================
// Artifact Attributes
// =============================================================================

@doc("Artifact/package attributes")
model ArtifactAttributes {
  @doc("Artifact filename")
  @encodedName("application/json", "artifact.filename")
  filename?: string;

  @doc("Artifact hash (SHA-256 or similar)")
  @encodedName("application/json", "artifact.hash")
  hash?: string;

  @doc("Hash algorithm used")
  @encodedName("application/json", "artifact.hash.algorithm")
  hashAlgorithm?: HashAlgorithm;

  @doc("Artifact size in bytes")
  @encodedName("application/json", "artifact.size")
  size?: int64;

  @doc("Artifact type")
  @encodedName("application/json", "artifact.type")
  type?: ArtifactType;

  @doc("Artifact version")
  @encodedName("application/json", "artifact.version")
  version?: string;

  @doc("Artifact attestation")
  @encodedName("application/json", "artifact.attestation")
  attestation?: string;

  @doc("Artifact purl (Package URL)")
  @encodedName("application/json", "artifact.purl")
  purl?: string;
}

// =============================================================================
// Artifact Enumerations
// =============================================================================

@doc("Hash algorithms")
enum HashAlgorithm {
  @doc("MD5")
  md5: "md5",

  @doc("SHA-1")
  sha1: "sha1",

  @doc("SHA-256")
  sha256: "sha256",

  @doc("SHA-384")
  sha384: "sha384",

  @doc("SHA-512")
  sha512: "sha512",

  @doc("BLAKE2b")
  blake2b: "blake2b",

  @doc("BLAKE2s")
  blake2s: "blake2s",

  @doc("BLAKE3")
  blake3: "blake3",
}

@doc("Artifact types")
enum ArtifactType {
  @doc("Container image")
  containerImage: "container_image",

  @doc("Binary executable")
  binary: "binary",

  @doc("Source archive")
  source: "source",

  @doc("Debian package")
  deb: "deb",

  @doc("RPM package")
  rpm: "rpm",

  @doc("NPM package")
  npm: "npm",

  @doc("PyPI package")
  pypi: "pypi",

  @doc("NuGet package")
  nuget: "nuget",

  @doc("Maven artifact")
  maven: "maven",

  @doc("Helm chart")
  helm: "helm",

  @doc("OCI artifact")
  oci: "oci",

  @doc("WASM module")
  wasm: "wasm",

  @doc("Documentation")
  docs: "docs",

  @doc("Other")
  other: "other",
}

// =============================================================================
// Package URL (purl) Model
// =============================================================================

@doc("Package URL components (purl spec)")
model PackageUrl {
  @doc("Package type (npm, pypi, maven, etc.)")
  type: string;

  @doc("Package namespace/scope")
  `namespace`?: string;

  @doc("Package name")
  name: string;

  @doc("Package version")
  version?: string;

  @doc("Qualifiers")
  qualifiers?: Record<string>;

  @doc("Subpath")
  subpath?: string;
}

// =============================================================================
// Software Bill of Materials (SBOM)
// =============================================================================

@doc("SBOM entry")
model SbomEntry {
  @doc("Package URL")
  purl: string;

  @doc("Package name")
  name: string;

  @doc("Package version")
  version: string;

  @doc("License")
  license?: string;

  @doc("Dependencies")
  dependencies?: string[];

  @doc("Vulnerabilities")
  vulnerabilities?: VulnerabilityRef[];
}

@doc("Vulnerability reference")
model VulnerabilityRef {
  @doc("CVE ID")
  @encodedName("application/json", "cve_id")
  cveId: string;

  @doc("Severity")
  severity: VulnerabilitySeverity;

  @doc("CVSS score")
  @encodedName("application/json", "cvss_score")
  cvssScore?: float64;

  @doc("Fixed in version")
  @encodedName("application/json", "fixed_in")
  fixedIn?: string;
}

@doc("Vulnerability severity levels")
enum VulnerabilitySeverity {
  @doc("Critical severity")
  critical: "critical",

  @doc("High severity")
  high: "high",

  @doc("Medium severity")
  medium: "medium",

  @doc("Low severity")
  low: "low",

  @doc("Informational")
  info: "info",

  @doc("Unknown severity")
  `unknown`: "unknown",
}
