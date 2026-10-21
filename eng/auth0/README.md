# qyl Auth0 tenant

`tenant.json` records what the hosted MCP server needs from the Auth0 tenant `qyl-eu`
(`qyl-eu.eu.auth0.com`). It holds no credentials and no user or client identifiers.

- The API `https://mcp.qyl.at/mcp`, with RBAC and the scopes `qyl:read`, `qyl:write` and
  `qyl:delete`, each with the description the consent screen shows. The server accepts only RS256
  access tokens for it.
- Grants of all three scopes for seven named user clients.
- The three permissions for every user that the deployment's `MCP_COLLECTOR_PROJECTS` maps to a
  project.
- The API's third-party default grant, kept empty: Dynamic Client Registration is open to anyone.
- The tenant flag `use_scope_descriptions_for_consent`, so consent shows the scope descriptions.
- A non-interactive Management API client with the read-only scopes `read:users`, `read:clients`,
  `read:client_grants` and `read:grants`.

## Check and apply

With an authenticated Auth0 CLI, and `MCP_COLLECTOR_PROJECTS` in the environment from Vitals or
Railway, never logged:

```bash
python3 eng/auth0/reconcile.py           # check
python3 eng/auth0/reconcile.py --apply   # reconcile, then check again
```

Exit code `0` means the tenant matches (or was reconciled), `1` drift, and `2` a missing or
ambiguous identity, an invalid mapping or an API error.

The reconciler creates no applications or users; every named client and mapped user must exist. It
adds missing scopes and restores their descriptions, turns on RBAC, adds missing client and user
permissions, empties the third-party default grant and sets the consent flag. It keeps every other
scope, grant, user, application setting and tenant setting, prints no API response, subject
identifier or secret, and triggers no deployment. The Management API client's credential stays in
Vitals and Railway; the reconciler never rotates it. Which user owns a project is set in
`MCP_COLLECTOR_PROJECTS`, not here.
