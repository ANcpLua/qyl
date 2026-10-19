import assert from "node:assert/strict";
import test from "node:test";
import { OAuthError, OAuthErrorCode, type AuthInfo, type OAuthTokenVerifier } from "@modelcontextprotocol/server";
import { createResourceAuthorization } from "./authorization.js";

const metadata = "https://mcp.example/.well-known/oauth-protected-resource/mcp";
const resource = new URL("https://mcp.example/mcp");
const verifier: OAuthTokenVerifier = {
  async verifyAccessToken(token) {
    return { token, clientId: "client", scopes: Buffer.from(token, "base64url").toString().split(" "), resource,
      expiresAt: Math.floor(Date.now() / 1000) + 300 };
  },
};
const bearer = (token: string, method = "POST") => new Request(resource, {
  method, headers: { Authorization: `Bearer ${Buffer.from(token).toString("base64url")}` },
});
const gateOptions = { expectedResource: resource, resourceMetadataUrl: metadata };

test("malformed or query credentials never reach the verifier, even alongside a valid header", async () => {
  let calls = 0;
  const gate = createResourceAuthorization({
    ...gateOptions,
    verifier: { async verifyAccessToken() { calls++; throw new Error("must not run"); } },
    requiredScopes: ["qyl:read"],
  });
  // authorization.md: a malformed or expired token is 401 invalid_token with the challenge.
  for (const headers of ["Bearer", "Bearer good, Bearer evil", "Bearer one two", "Basic abc"]) {
    const response = await gate(new Request(resource, { headers: { authorization: headers } }));
    assert(response instanceof Response);
    assert.equal(response.status, 401, headers);
    assert.match(response.headers.get("www-authenticate")!, /^Bearer error="invalid_token"/u, headers);
    assert.match(response.headers.get("www-authenticate")!, new RegExp(`resource_metadata="${metadata}"`, "u"));
  }
  const response = await gate(new Request(`${resource.href}?access_token=secret`, {
    headers: { authorization: "Bearer good" },
  }));
  assert(response instanceof Response);
  assert.equal(response.status, 400);
  // RFC 6750 §3: a refusal without a usable token still carries the challenge.
  assert.match(response.headers.get("www-authenticate")!, new RegExp(`^Bearer error="invalid_request", error_description="[^"]+", resource_metadata="${metadata}"$`, "u"));
  assert.equal((await response.json() as { error: string }).error, "invalid_request");
  assert.equal(calls, 0);
});

test("no credentials gets the bare sign-in challenge; a rejected token names invalid_token (RFC 6750 §3.1)", async () => {
  const scopes = { requiredScopes: ["qyl:read"], authorizationScopes: ["qyl:read", "qyl:write", "qyl:delete"] };
  const gate = createResourceAuthorization({ ...gateOptions, verifier, ...scopes });
  for (const method of ["GET", "POST"]) {
    const response = await gate(new Request(resource, { method }));
    assert(response instanceof Response);
    assert.equal(response.status, 401, method);
    assert.equal(response.headers.get("www-authenticate"), `Bearer scope="qyl:read qyl:write qyl:delete", resource_metadata="${metadata}"`);
    assert.equal(response.headers.get("cache-control"), "no-store");
    assert.equal(await response.text(), "");
  }
  const rejecting: OAuthTokenVerifier = {
    async verifyAccessToken() { throw new OAuthError(OAuthErrorCode.InvalidToken, 'Token "expired"'); },
  };
  const rejected = await createResourceAuthorization({ ...gateOptions, verifier: rejecting, ...scopes })(bearer("qyl:read"));
  assert(rejected instanceof Response);
  assert.equal(rejected.status, 401);
  assert.equal(rejected.headers.get("www-authenticate"),
    `Bearer error="invalid_token", error_description="Token \\"expired\\"", scope="qyl:read qyl:write qyl:delete", resource_metadata="${metadata}"`);
  const body = await rejected.json() as { error: string; error_description: string };
  assert.equal(body.error, "invalid_token");
  assert.equal(body.error_description, 'Token "expired"');
});

test("initial sign-in requests product scopes and step-up names only missing scopes", async () => {
  const gate = createResourceAuthorization({ ...gateOptions, verifier,
    requiredScopes: ["files:write", "files:read", "files:write"] });
  for (const request of [new Request(resource), bearer("files:read")]) {
    const response = await gate(request);
    assert(response instanceof Response);
    assert.equal(response.status, request.headers.has("authorization") ? 403 : 401);
    assert.match(response.headers.get("www-authenticate")!, request.headers.has("authorization")
      ? /scope="files:write"/u : /scope="files:read files:write"/u);
    assert.match(response.headers.get("www-authenticate")!, /resource_metadata=/u);
    assert.doesNotMatch(response.headers.get("www-authenticate")!, /offline_access/u);
    assert.equal(response.headers.get("cache-control"), "no-store");
  }
});

test("explicit scope hierarchies are transitive, cycle safe and preserve verified claims", async () => {
  const gate = createResourceAuthorization({ ...gateOptions, verifier,
    requiredScopes: ["files:read"], scopeImplications: {
      admin: ["files:write"], "files:write": ["files:read"], "files:read": ["files:write"],
    } });
  const auth = await gate(bearer("admin"));
  assert(!(auth instanceof Response));
  assert.deepEqual(auth.scopes, ["admin"]);
  for (const granted of ["unrelated", "files:*", "toString", "constructor"]) {
    const denied = await gate(bearer(granted));
    assert(denied instanceof Response);
    assert.equal(denied.status, 403);
  }
});

test("authentication is checked on every HTTP method and request", async () => {
  let calls = 0;
  const countingVerifier = { async verifyAccessToken(token: string): Promise<AuthInfo> {
    calls++;
    return verifier.verifyAccessToken(token);
  } };
  const gate = createResourceAuthorization({ ...gateOptions, verifier: countingVerifier, requiredScopes: ["read"] });
  for (const method of ["GET", "POST", "DELETE"]) {
    assert(!((await gate(bearer("read", method))) instanceof Response));
    const missing = await gate(new Request(resource, { method }));
    assert(missing instanceof Response);
    assert.equal(missing.status, 401);
  }
  assert.equal(calls, 3);
});

test("Bearer is case-insensitive, permits multiple spaces, and leaves the MCP body unread", async () => {
  const gate = createResourceAuthorization({ ...gateOptions, verifier, requiredScopes: ["read"] });
  const request = new Request(resource, { method: "POST", body: "MCP body",
    headers: { authorization: `bEaReR   ${Buffer.from("read").toString("base64url")}` } });
  assert(!((await gate(request)) instanceof Response));
  assert.equal(request.bodyUsed, false);
  assert.equal(await request.text(), "MCP body");
});

test("unsafe or refresh-only resource scope configuration is refused", () => {
  for (const scope of ["offline_access", "read write", 'read"', "read\\", ""]) {
    assert.throws(() => createResourceAuthorization({ ...gateOptions, verifier, requiredScopes: [scope] }), /Resource scopes/u);
  }
  for (const resourceMetadataUrl of ["http://mcp.example/metadata", "https://user:secret@mcp.example/metadata",
    "https://mcp.example/metadata?secret=token", "https://mcp.example/metadata#fragment",
    "https://mcp.example/\nmetadata"]) {
    assert.throws(() => createResourceAuthorization({ ...gateOptions, verifier, resourceMetadataUrl, requiredScopes: [] }), /safe HTTPS/u);
  }
});

test("initial qyl sign-in requests read, write and delete while read-only tokens remain valid", async () => {
  const gate = createResourceAuthorization({ ...gateOptions, verifier,
    requiredScopes: ["qyl:read"], authorizationScopes: ["qyl:read", "qyl:write", "qyl:delete"] });
  const response = await gate(new Request(resource, { method: "POST" }));
  assert(response instanceof Response);
  assert.equal(response.status, 401);
  assert.match(response.headers.get("www-authenticate")!, /scope="qyl:read qyl:write qyl:delete"/u);
  assert(!((await gate(bearer("qyl:read"))) instanceof Response));
  const denied = await gate(bearer("qyl:write qyl:delete"));
  assert(denied instanceof Response);
  assert.equal(denied.status, 403);
  assert.match(denied.headers.get("www-authenticate")!, /scope="qyl:read"/u);
});

test("expectedResource accepts only tokens the verifier reports for this resource", async () => {
  const reportedFor = (reported: URL | undefined): OAuthTokenVerifier => ({
    async verifyAccessToken(token) {
      return { token, clientId: "client", scopes: ["qyl:read"], expiresAt: Math.floor(Date.now() / 1000) + 300,
        ...(reported === undefined ? {} : { resource: reported }) };
    },
  });
  for (const [reported, accepted] of [
    [resource, true],
    [new URL("https://mcp.example/mcp/"), true],
    [new URL("https://mcp.example/other"), false],
    [new URL("https://foreign.example/mcp"), false],
    [undefined, false],
  ] as const) {
    const gate = createResourceAuthorization({ ...gateOptions, verifier: reportedFor(reported), requiredScopes: ["qyl:read"] });
    const result = await gate(new Request(resource, { headers: { authorization: "Bearer token" } }));
    if (accepted) {
      assert(!(result instanceof Response), String(reported));
      // The gate stamps its metadata URL, so per-tool scope challenges reuse it unconfigured.
      assert.equal(result.resourceMetadataUrl, metadata);
      continue;
    }
    assert(result instanceof Response, String(reported));
    assert.equal(result.status, 401, String(reported));
    assert.match(result.headers.get("www-authenticate")!, /error="invalid_token"/u);
    assert.match(result.headers.get("www-authenticate")!, new RegExp(`resource_metadata="${metadata}"`, "u"));
  }
});

test("a rejected or expiry-less token is a 401 challenge, never a 500", async () => {
  const rejecting: OAuthTokenVerifier = {
    async verifyAccessToken() { throw new OAuthError(OAuthErrorCode.InvalidToken, "Access token verification failed"); },
  };
  const timeless: OAuthTokenVerifier = {
    async verifyAccessToken(token) { return { token, clientId: "client", scopes: ["qyl:read"], resource }; },
  };
  const expired: OAuthTokenVerifier = {
    async verifyAccessToken(token) {
      return { token, clientId: "client", scopes: ["qyl:read"], resource, expiresAt: Math.floor(Date.now() / 1000) - 1 };
    },
  };
  for (const candidate of [rejecting, timeless, expired]) {
    const gate = createResourceAuthorization({ ...gateOptions, verifier: candidate, requiredScopes: ["qyl:read"] });
    const response = await gate(new Request(resource, { headers: { authorization: "Bearer token" } }));
    assert(response instanceof Response);
    assert.equal(response.status, 401);
    assert.match(response.headers.get("www-authenticate")!, /^Bearer error="invalid_token"/u);
    assert.equal((await response.json() as { error: string }).error, "invalid_token");
  }
});
