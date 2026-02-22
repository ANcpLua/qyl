## Catalog Output Policy (Persistent)

<policy-ref id="@catalog-format-v1" path="/Users/ancplua/qyl/docs/policies/catalog-format-policy.md" />

When a user asks for a catalog/registry/matrix/taxonomy/inventory:

- MUST load and follow `@catalog-format-v1`.
- MUST produce the standard artifact set unless the user explicitly opts out.
- MUST keep IDs stable once assigned.
- MUST use commit-pinned source links when source links are requested.
