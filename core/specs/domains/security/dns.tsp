// =============================================================================
// QYL v2.0 - DNS Semantic Conventions (OTel v1.38)
// =============================================================================
// DNS resolution telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Security.Dns;

// =============================================================================
// DNS Span Attributes
// =============================================================================

@doc("DNS-related span attributes")
model DnsAttributes {
  @doc("The name being queried")
  @encodedName("application/json", "dns.question.name")
  questionName: string;

  @doc("The type of DNS record being queried")
  @encodedName("application/json", "dns.question.type")
  questionType?: DnsRecordType;

  @doc("The class of DNS query (usually IN for Internet)")
  @encodedName("application/json", "dns.question.class")
  questionClass?: DnsClass;

  @doc("Resolved IP addresses (comma-separated)")
  @encodedName("application/json", "dns.answers")
  answers?: string[];

  @doc("Response code from DNS server")
  @encodedName("application/json", "dns.response_code")
  responseCode?: DnsResponseCode;

  @doc("Whether the response is from cache")
  @encodedName("application/json", "dns.cache_hit")
  cacheHit?: boolean;

  @doc("Time-to-live of the DNS response in seconds")
  @encodedName("application/json", "dns.response_ttl")
  responseTtl?: int32;

  @doc("DNS server address used for resolution")
  @encodedName("application/json", "dns.server.address")
  serverAddress?: IpAddress;

  @doc("DNS server port (usually 53)")
  @encodedName("application/json", "dns.server.port")
  serverPort?: Port;
}

// =============================================================================
// DNS Enumerations
// =============================================================================

@doc("DNS record types")
enum DnsRecordType {
  @doc("Address record (IPv4)")
  A: "A",

  @doc("Address record (IPv6)")
  AAAA: "AAAA",

  @doc("Canonical name record")
  CNAME: "CNAME",

  @doc("Mail exchange record")
  MX: "MX",

  @doc("Name server record")
  NS: "NS",

  @doc("Pointer record (reverse DNS)")
  PTR: "PTR",

  @doc("Start of authority record")
  SOA: "SOA",

  @doc("Service location record")
  SRV: "SRV",

  @doc("Text record")
  TXT: "TXT",

  @doc("Certificate authority authorization")
  CAA: "CAA",

  @doc("DNS-based service discovery")
  DNSKEY: "DNSKEY",

  @doc("Delegation signer")
  DS: "DS",

  @doc("HTTPS service binding")
  HTTPS: "HTTPS",

  @doc("SVCB service binding")
  SVCB: "SVCB",

  @doc("NAPTR record")
  NAPTR: "NAPTR",

  @doc("Any record type")
  ANY: "ANY",
}

@doc("DNS query classes")
enum DnsClass {
  @doc("Internet class (most common)")
  IN: "IN",

  @doc("Chaos class")
  CH: "CH",

  @doc("Hesiod class")
  HS: "HS",

  @doc("Any class")
  ANY: "ANY",
}

@doc("DNS response codes (RCODE)")
enum DnsResponseCode {
  @doc("No error")
  NOERROR: 0,

  @doc("Format error in query")
  FORMERR: 1,

  @doc("Server failure")
  SERVFAIL: 2,

  @doc("Name does not exist (NXDOMAIN)")
  NXDOMAIN: 3,

  @doc("Not implemented")
  NOTIMP: 4,

  @doc("Query refused")
  REFUSED: 5,

  @doc("Name exists when it should not")
  YXDOMAIN: 6,

  @doc("RR set exists when it should not")
  YXRRSET: 7,

  @doc("RR set that should exist does not")
  NXRRSET: 8,

  @doc("Server not authoritative")
  NOTAUTH: 9,

  @doc("Name not in zone")
  NOTZONE: 10,
}

// =============================================================================
// DNS Resolution Model
// =============================================================================

@doc("Complete DNS resolution record")
model DnsResolution {
  @doc("Query name")
  @encodedName("application/json", "question_name")
  questionName: string;

  @doc("Query type")
  @encodedName("application/json", "question_type")
  questionType: DnsRecordType;

  @doc("Response code")
  @encodedName("application/json", "response_code")
  responseCode: DnsResponseCode;

  @doc("Resolved answers")
  answers: DnsAnswer[];

  @doc("Resolution duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;

  @doc("DNS server used")
  @encodedName("application/json", "server_address")
  serverAddress: IpAddress;

  @doc("Whether result was cached")
  @encodedName("application/json", "cache_hit")
  cacheHit: boolean;

  @doc("Resolution timestamp")
  timestamp: utcDateTime;
}

@doc("DNS answer record")
model DnsAnswer {
  @doc("Record type")
  type: DnsRecordType;

  @doc("Record name")
  name: string;

  @doc("Record value/data")
  data: string;

  @doc("Time-to-live in seconds")
  ttl: int32;
}
