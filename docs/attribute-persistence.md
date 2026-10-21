# Attribute persistence

The Collector decides for every incoming OTLP attribute whether to store it, by the policy in its
generated semantic catalog (`services/qyl.collector.ingestion/Generated/CollectorSemanticAttributeCatalog.g.cs`,
regenerated with `./eng/build.sh GenerateCollectorSemanticAttributeCatalog`). The policy runs in the
shared OTLP decoder, for HTTP and gRPC exports alike, on every attribute map: resources,
instrumentation scopes, spans, span events and links, logs, metric points, exemplars, instrument
metadata, and maps nested in attributes or in structured log bodies.

## Which keys are stored

A key is checked in this order:

1. **Denied keys and prefixes** are dropped, together with their dotted children: credentials such
   as `authorization`, raw content such as `db.query.text`, `url.full`, `url.query` and
   `gen_ai.input.messages`, private resource fields such as `host.name`, `host.ip` and
   `service.instance.id`, and every key under `user.` or `enduser.`.
2. **`baggage.*`** is dropped.
3. **`exception.message` and `exception.stacktrace`** are stored after sanitization (below), under
   exactly these spellings; their case variants and dotted children are dropped.
4. **Captured HTTP headers and RPC metadata** are stored (below).
5. **Denied tokens:** any other key containing `message`, `token`, `secret`, `password`, `query`,
   `prompt`, `body`, `cookie`, `jwt` or another denied token is dropped, unless the catalog exempts
   it by exact name, such as `messaging.message.id` or `gen_ai.usage.input_tokens`, or it is a
   pass-through tag of a known instrumentation library.

`session.id`, `mcp.session.id` and `gen_ai.conversation.id` set the record's session and are not
stored as attributes. The decoder counts every key it drops on `qyl.collector.attributes.dropped`,
by namespace.

## Exception text

`exception.message` and `exception.stacktrace` pass one sanitizer before any storage row, series
identity or original metric-point record is built. Harmless text keeps its line breaks, indentation
and stack frames. The sanitizer redacts:

- connection-string credentials, including quoted and ODBC-braced passwords and user IDs;
- labeled credentials (API keys, passwords, secrets, access, refresh and session tokens) and
  `Bearer` and `Basic` authorization values;
- JWTs, recognized by a JSON JOSE header, and tokens with the prefixes `sk-`, `sk_live_`,
  `sk_test_`, `ghp_`, `gho_`, `ghu_`, `ghs_`, `ghr_`, `github_pat_`, `npm_`, `xoxa-`, `xoxb-`,
  `xoxp-`, `xoxr-` and `xoxs-`;
- URL user information, query strings and fragments: the whole query and fragment go, named or
  not, while the scheme, host and path stay.

Input longer than 131,072 UTF-16 code units is not processed. Every pattern runs on .NET's
non-backtracking regular expression engine, in time linear in that bounded input and without a
timeout. After redaction the value is cut to 32,768 UTF-8 bytes, including a trailing `…`, and never
inside a Unicode scalar; redaction comes first, so an oversized credential cannot push useful text
out. Log bodies keep their own 64 KiB limit.

A non-string value, an input over the limit, invalid text, a malformed URL or a sanitizer error
replaces only that field with `[SANITIZATION_FAILED]`; the record and its other attributes stay. The
counter `qyl.collector.attributes.sanitization.failed` records the canonical key
(`qyl.attribute.key`) and a fixed `error.type` category, never the value, a captured credential or a
sanitizer exception message.

This is a credential sanitizer for two keys, not PII detection. It cannot recognize an unlabeled
secret of an unknown format, credentials inside encoded or encrypted data, or sensitive text in other
attributes, plain log bodies or span status descriptions; a renamed key does not get the exception
treatment.

## Metric identity

Resource, scope and point attributes form a metric's identity before the policy drops any of them:
the Collector hashes the canonical typed attributes, with map keys sorted and array order kept, and
stores only the resulting instrument and series identifiers for dropped values. A denied
`service.instance.id`, for example, is absent from returned attributes, while two service instances
stay separate series. The identifiers express identity; they are no anonymization.

## Captured HTTP headers and RPC metadata

Instrumentation chooses which headers and metadata to capture; the Collector needs no second
allowlist. The four case-insensitive families are `http.request.header.<name>`,
`http.response.header.<name>`, `rpc.request.metadata.<name>` and `rpc.response.metadata.<name>`. A
bare family key, an empty name or an invalid name is dropped: HTTP names follow RFC 9110 field-name
tokens, and RPC names allow ASCII letters, digits, `_`, `-` and `.`.

After the denials of step 1, every valid name is stored with its string-array value, including
`authorization`, `cookie`, `x-api-key`, `x-token-count`, `mcp-param-*` and `-bin` metadata names.
The capturing producer decides what is kept. The MCP server returns these captured arrays without
name- or value-based redaction; every other field, and a malformed capture shape, keeps its normal
redaction.
