// =============================================================================
// QYL v2.0 - TLS Semantic Conventions (OTel v1.39)
// =============================================================================
// Transport Layer Security telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Security.Tls;

// =============================================================================
// TLS Span Attributes
// =============================================================================

@doc("TLS/SSL span attributes")
model TlsAttributes {
  @doc("TLS protocol version (e.g., '1.2', '1.3')")
  @encodedName("application/json", "tls.protocol.version")
  protocolVersion?: TlsProtocolVersion;

  @doc("Negotiated cipher suite (e.g., TLS_AES_256_GCM_SHA384)")
  @encodedName("application/json", "tls.cipher")
  cipher?: string;

  @doc("TLS curve used for key exchange")
  @encodedName("application/json", "tls.curve")
  curve?: TlsCurve;

  @doc("Whether the connection was resumed from a previous session")
  @encodedName("application/json", "tls.resumed")
  resumed?: boolean;

  @doc("TLS session ID")
  @encodedName("application/json", "tls.session_id")
  sessionId?: string;

  @doc("ALPN negotiated protocol")
  @encodedName("application/json", "tls.next_protocol")
  nextProtocol?: string;

  @doc("Whether client certificate was requested")
  @encodedName("application/json", "tls.client_certificate_required")
  clientCertificateRequired?: boolean;

  @doc("Server certificate chain")
  @encodedName("application/json", "tls.server.certificate")
  serverCertificate?: string;

  @doc("Server certificate chain (PEM)")
  @encodedName("application/json", "tls.server.certificate_chain")
  serverCertificateChain?: string[];

  @doc("Server certificate hash (SHA-256)")
  @encodedName("application/json", "tls.server.hash.sha256")
  serverHashSha256?: Sha256Hash;

  @doc("Server certificate issuer")
  @encodedName("application/json", "tls.server.issuer")
  serverIssuer?: string;

  @doc("Server certificate subject")
  @encodedName("application/json", "tls.server.subject")
  serverSubject?: string;

  @doc("Server certificate not_before date")
  @encodedName("application/json", "tls.server.not_before")
  serverNotBefore?: utcDateTime;

  @doc("Server certificate not_after date")
  @encodedName("application/json", "tls.server.not_after")
  serverNotAfter?: utcDateTime;

  @doc("Server certificate serial number")
  @encodedName("application/json", "tls.server.serial_number")
  serverSerialNumber?: string;

  @doc("Client certificate hash (SHA-256)")
  @encodedName("application/json", "tls.client.hash.sha256")
  clientHashSha256?: Sha256Hash;

  @doc("Client certificate issuer")
  @encodedName("application/json", "tls.client.issuer")
  clientIssuer?: string;

  @doc("Client certificate subject")
  @encodedName("application/json", "tls.client.subject")
  clientSubject?: string;

  @doc("SNI hostname")
  @encodedName("application/json", "tls.server_name")
  serverName?: string;

  @doc("Whether certificate verification was successful")
  @encodedName("application/json", "tls.established")
  established?: boolean;
}

// =============================================================================
// TLS Enumerations
// =============================================================================

@doc("TLS protocol versions")
enum TlsProtocolVersion {
  @doc("SSL 2.0 (insecure, deprecated)")
  ssl2: "ssl2",

  @doc("SSL 3.0 (insecure, deprecated)")
  ssl3: "ssl3",

  @doc("TLS 1.0 (deprecated)")
  tls10: "1.0",

  @doc("TLS 1.1 (deprecated)")
  tls11: "1.1",

  @doc("TLS 1.2")
  tls12: "1.2",

  @doc("TLS 1.3 (recommended)")
  tls13: "1.3",
}

@doc("TLS key exchange curves")
enum TlsCurve {
  @doc("NIST P-256 / secp256r1")
  secp256r1: "secp256r1",

  @doc("NIST P-384 / secp384r1")
  secp384r1: "secp384r1",

  @doc("NIST P-521 / secp521r1")
  secp521r1: "secp521r1",

  @doc("X25519 (Curve25519)")
  x25519: "x25519",

  @doc("X448 (Curve448)")
  x448: "x448",

  @doc("Ed25519")
  ed25519: "ed25519",

  @doc("Ed448")
  ed448: "ed448",

  @doc("Kyber768 (post-quantum)")
  kyber768: "kyber768",
}

@doc("TLS handshake states")
enum TlsHandshakeState {
  @doc("Handshake not started")
  notStarted: "not_started",

  @doc("Client hello sent")
  clientHello: "client_hello",

  @doc("Server hello received")
  serverHello: "server_hello",

  @doc("Certificate exchange")
  certificate: "certificate",

  @doc("Key exchange")
  keyExchange: "key_exchange",

  @doc("Finished")
  finished: "finished",

  @doc("Failed")
  failed: "failed",
}

@doc("Certificate validation errors")
enum TlsCertificateError {
  @doc("Certificate expired")
  expired: "expired",

  @doc("Certificate not yet valid")
  notYetValid: "not_yet_valid",

  @doc("Certificate revoked")
  revoked: "revoked",

  @doc("Unknown issuer / untrusted root")
  unknownIssuer: "unknown_issuer",

  @doc("Hostname mismatch")
  hostnameMismatch: "hostname_mismatch",

  @doc("Self-signed certificate")
  selfSigned: "self_signed",

  @doc("Chain too long")
  chainTooLong: "chain_too_long",

  @doc("Weak signature algorithm")
  weakSignature: "weak_signature",

  @doc("Key too small")
  keyTooSmall: "key_too_small",

  @doc("Invalid usage")
  invalidUsage: "invalid_usage",
}

// =============================================================================
// TLS Connection Model
// =============================================================================

@doc("TLS connection details")
model TlsConnection {
  @doc("Protocol version")
  @encodedName("application/json", "protocol_version")
  protocolVersion: TlsProtocolVersion;

  @doc("Cipher suite")
  @encodedName("application/json", "cipher_suite")
  cipherSuite: string;

  @doc("Key exchange curve")
  curve?: TlsCurve;

  @doc("Whether session was resumed")
  resumed: boolean;

  @doc("ALPN protocol")
  @encodedName("application/json", "alpn_protocol")
  alpnProtocol?: string;

  @doc("Server certificate info")
  @encodedName("application/json", "server_certificate")
  serverCertificate?: TlsCertificate;

  @doc("Client certificate info")
  @encodedName("application/json", "client_certificate")
  clientCertificate?: TlsCertificate;

  @doc("Handshake duration in milliseconds")
  @encodedName("application/json", "handshake_duration_ms")
  handshakeDurationMs: DurationMs;
}

@doc("TLS certificate information")
model TlsCertificate {
  @doc("Subject common name")
  @encodedName("application/json", "subject_cn")
  subjectCn: string;

  @doc("Full subject")
  subject: string;

  @doc("Issuer common name")
  @encodedName("application/json", "issuer_cn")
  issuerCn: string;

  @doc("Full issuer")
  issuer: string;

  @doc("Serial number")
  @encodedName("application/json", "serial_number")
  serialNumber: string;

  @doc("Not valid before")
  @encodedName("application/json", "not_before")
  notBefore: utcDateTime;

  @doc("Not valid after")
  @encodedName("application/json", "not_after")
  notAfter: utcDateTime;

  @doc("SHA-256 fingerprint")
  @encodedName("application/json", "fingerprint_sha256")
  fingerprintSha256: Sha256Hash;

  @doc("Subject alternative names")
  san?: string[];

  @doc("Days until expiry")
  @encodedName("application/json", "days_until_expiry")
  daysUntilExpiry?: int32;
}
