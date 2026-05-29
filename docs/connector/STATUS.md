# qyl Connector Directory — Submission Status

> Live tracker for the qyl MCP connector's Anthropic Connector Directory
> submission. Update inline as gates close.

| Stage | Status | Date | Notes |
|---|---|---|---|
| E3.a — Tool annotations audit | ✅ DONE | 2026-05-26 | 124 tools classified; see [`tool-annotations-audit.md`](./tool-annotations-audit.md). |
| E3.b.1 — Privacy policy drafted | ✅ DONE | 2026-05-26 | [`privacy-policy.md`](./privacy-policy.md). Pending public hosting. |
| E3.b.2 — Manifest drafted | ✅ DONE | 2026-05-26 | [`manifest.yaml`](./manifest.yaml). Pending public hosting + assets. |
| E3.b.3 — Public hosting | ⏳ PENDING | — | Requires qyl.ai DNS + CDN setup. URLs blocked: `privacy_policy_url`, `terms_of_service_url`, all under `assets:` and screenshots. |
| E3.b.4 — Logo + screenshots | ⏳ PENDING | — | 64×64 + 512×512 PNG logos + ≥3 in-Claude screenshots. |
| E3.c.1 — Form submitted | ⏳ PENDING | — | claude.com/docs/connectors/building/submission.md. Blocked on all E3.b.x being live. |
| E3.c.2 — Acknowledgment received | ⏳ PENDING | — | Anthropic review queue email/dashboard. |
| E3.c.3 — Listing published | ⏳ PENDING | — | Public URL captured here on acceptance. |

## What can move forward today

Nothing in E3.b.3 / E3.b.4 / E3.c.* can move forward without out-of-repo
work (DNS, design assets, the submission form itself). E3.a + the E3.b
drafts are the codebase-side contribution complete; the remaining gates
are operational.

## What blocks the submission

1. **DNS for `qyl.ai`** — every URL in the manifest needs to resolve and
   return 200. Without this, the manifest is dead-link bait and Anthropic
   will reject the submission outright.
2. **Logo + screenshots** — Anthropic's listing card and detail page
   require these to render. Without them the listing looks broken even
   if approved.
3. **`/mcp/{tenant}` live with Keycloak JWT bearer validation** — the collector no
   longer hosts `/auth/*`; external MCP clients authenticate against Keycloak.
4. **Keycloak realm provisioned** — real OAuth round-trip needs a live
   identity provider with MCP clients registered for direct access-token issuance.
