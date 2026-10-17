# Attribute persistence

The Collector applies the generated SemanticConventions persistence policy before storing incoming OTLP attributes.
Exact-key and prefix denials take precedence over token exemptions. Credential keys, private resource fields,
request headers and baggage remain denied. An arbitrary key containing `message` remains denied; the exception is the
exact key `exception.message`. `exception.stacktrace` is also eligible. Neither key is permission to store its value raw.
Case variants and dotted descendants remain denied; only the two canonical spellings receive this eligibility.

Both exception fields pass through the same .NET sanitizer in the shared OTLP decoder, before constructing storage rows,
series identities or the original metric-point journal. It covers HTTP and gRPC exports, span attributes, events and
links, log attributes, resources, instrumentation scopes, metric points, exemplars, instrument metadata and nested maps
inside attributes or structured log bodies. Scope and instrument metadata follow the same key denials as other attributes.

Harmless exception text retains line breaks, indentation and stack frames. The sanitizer redacts:

- Connection-string credentials, including quoted and ODBC-braced passwords and user IDs.
- Labeled API keys, passwords, secrets and access, refresh or session tokens; bearer and Basic authorization values.
  Unquoted header-style values extend to the line end, including semicolons; connection-string assignments use the
  semicolon separator, with quoted and braced values parsed before that separator.
- JWTs identified by a JSON JOSE header and recognizable OpenAI, GitHub, npm and Slack token prefixes.
- URL user information, query strings and fragments. It conservatively removes the entire query and fragment, including
  unnamed secrets, while retaining the scheme, destination and path.

For each exception field, input processing is capped at 131,072 UTF-16 code units. Regular expressions use the linear-time
engine and a 100 ms timeout. After sanitization, the retained value is capped at 32,768 UTF-8 bytes, including an ellipsis
when truncated; truncation never splits a Unicode scalar. Redaction precedes truncation, so an oversized credential
cannot displace useful text that follows it. The existing 64 KiB aggregate log-body retention limit and independent
request/message size limits remain in effect. The log-body budget counts decoded UTF-8 strings and structural bytes;
JSON escaping may expand the stored representation, but cannot alone change a structured body into a string.

A non-string value, input over the processing cap, malformed URL or sanitizer failure replaces only the affected field
with `[SANITIZATION_FAILED]`. The record and its other attributes remain. The counter
`qyl.collector.attributes.sanitization.failed` reports only the canonical attribute key and a fixed `error.type` category.
It never includes the value, captured credentials or an exception message from the sanitizer.

This is a key-specific credential sanitizer, not general PII detection. It cannot recognize an unlabeled secret with an
unknown format, credentials hidden in encoded or encrypted data, or sensitive text in unrelated attribute names, plain
log bodies or span status descriptions. Renamed keys do not inherit the exception exemption. Historical records are not
rewritten; the policy applies when the Collector decodes new exports.

## Metric identity

Resource, scope and point attributes participate in metric identity before persistence
filtering. The Collector hashes canonical typed attributes, sorting map keys and preserving
array order. Only the resulting instrument and series identifiers are stored for withheld
values. A denied `service.instance.id`, for example, remains absent from returned attributes
while two service instances remain separate streams. These identifiers are not anonymization
guarantees; they express identity, not the underlying value.

Existing series retain their identifiers. Values removed by an earlier Collector cannot be
recovered, so new complete identities stay separate from historical incomplete identities.
