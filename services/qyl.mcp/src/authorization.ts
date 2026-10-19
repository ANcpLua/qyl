import {
  OAuthError,
  OAuthErrorCode,
  verifyBearerToken,
  type AuthInfo,
  type OAuthTokenVerifier,
  type VerifyBearerTokenOptions,
} from "@modelcontextprotocol/server";

export interface ResourceAuthorizationOptions {
  readonly verifier: OAuthTokenVerifier;
  /**
   * The resource identifier tokens must be issued for: the verifier reports a
   * token's audience as `AuthInfo.resource`, and a token reported for another
   * resource, or for none, is answered `401 invalid_token`.
   */
  readonly expectedResource: URL;
  readonly resourceMetadataUrl: string;
  readonly requiredScopes: readonly string[];
  /** Scopes requested at initial sign-in; individual operations still enforce their own requirements. */
  readonly authorizationScopes?: readonly string[];
  /** Explicit implications only; a wildcard-looking string grants nothing by itself. */
  readonly scopeImplications?: Readonly<Record<string, readonly string[]>>;
}

function validateScopes(scopes: readonly string[]): void {
  for (const scope of scopes) {
    // RFC 6749 scope-token excludes quotes, backslashes and whitespace.
    if (!/^[\x21\x23-\x5B\x5D-\x7E]+$/u.test(scope) || scope === "offline_access") {
      throw new Error("Resource scopes must be valid OAuth scope tokens and cannot include offline_access");
    }
  }
}

/** A hierarchy is an explicit, transitive relation. Cycles terminate and never
 * grant a scope without a verified scope as their starting point.
 */
export function scopeClosure(
  granted: readonly string[],
  implications: ReadonlyMap<string, readonly string[]>,
): ReadonlySet<string> {
  const result = new Set(granted);
  const pending = [...granted];
  for (let i = 0; i < pending.length; i++) {
    for (const implied of implications.get(pending[i]) ?? []) {
      if (!result.has(implied)) {
        result.add(implied);
        pending.push(implied);
      }
    }
  }
  return result;
}

/** RFC 7230 quoted-string: the quote and the backslash escaped, the value kept printable. */
function quoted(value: string): string {
  return value.replaceAll(/[\\"]/gu, String.raw`\$&`).replaceAll(/[^\u0020-\u007E]/gu, " ");
}

/** RFC 6750 §3.1: the status each error answers with. */
function statusOf(error: OAuthError): number {
  switch (error.code) {
    case OAuthErrorCode.InvalidToken: return 401;
    case OAuthErrorCode.InsufficientScope: return 403;
    default: return 400;
  }
}

/**
 * All grants converge on this gate. Extensions never see an unverified token.
 *
 * The SDK's bearer core does the verification (authorization.md, "Require a bearer
 * token on a web-standard host"): it runs the verifier, compares the reported
 * resource with `expectedResource`, rejects a token without `exp` or past it, and
 * stamps `resourceMetadataUrl` onto the AuthInfo it returns, so per-tool scope
 * challenges advertise the same document. The refusals are this gate's, in the
 * SDK's challenge grammar, because RFC 6750 §3.1 asks for one the SDK does not
 * format: a request that presented no credentials gets the challenge without an
 * error code, only the scopes to sign in for and the metadata URL. A presented
 * credential that was rejected names its error: `invalid_request` (400) for a
 * token in the query string, `invalid_token` (401) for a malformed, invalid,
 * expired or foreign token, `insufficient_scope` (403) naming the missing scopes.
 */
export function createResourceAuthorization(
  options: ResourceAuthorizationOptions,
): (request: Request) => Promise<AuthInfo | Response> {
  const requiredScopes = [...new Set(options.requiredScopes)].sort();
  validateScopes(requiredScopes);
  const authorizationScopes = [...new Set(options.authorizationScopes ?? requiredScopes)];
  validateScopes(authorizationScopes);
  const implications = new Map(Object.entries(options.scopeImplications ?? {})
    .map(([scope, implied]) => {
      validateScopes([scope, ...implied]);
      return [scope, [...implied]] as const;
    }));
  const metadataUrl = new URL(options.resourceMetadataUrl);
  if (metadataUrl.protocol !== "https:" || metadataUrl.username || metadataUrl.password
    || metadataUrl.search || metadataUrl.hash || /["\\]/u.test(options.resourceMetadataUrl)
    || Array.from(options.resourceMetadataUrl).some((character) => character.charCodeAt(0) <= 0x20)) {
    throw new Error("Resource metadata must use a safe HTTPS URL");
  }
  const bearer: VerifyBearerTokenOptions = {
    verifier: options.verifier,
    resourceMetadataUrl: metadataUrl.href,
    expectedResource: options.expectedResource,
  };
  /** The challenge every refusal carries (RFC 6750 §3); without an error it invites the sign-in (§3.1). */
  const challenge = (status: number, error: OAuthError | undefined, scopes: readonly string[]): Response => {
    const parameters = [
      ...(error === undefined ? [] : [`error="${error.code}"`, `error_description="${quoted(error.message)}"`]),
      ...(scopes.length === 0 ? [] : [`scope="${scopes.join(" ")}"`]),
      `resource_metadata="${metadataUrl.href}"`,
    ];
    const headers = { "www-authenticate": `Bearer ${parameters.join(", ")}`, "cache-control": "no-store" };
    return error === undefined
      ? new Response(null, { status, headers })
      : Response.json(error.toResponseObject(), { status, headers });
  };
  const refuse = (error: unknown, scopes: readonly string[]): Response => (error instanceof OAuthError
    ? challenge(statusOf(error), error, scopes)
    : Response.json({ error: "server_error", error_description: "Internal Server Error" }, { status: 500, headers: { "cache-control": "no-store" } }));
  return async (request) => {
    if (new URL(request.url).searchParams.has("access_token")) {
      return refuse(new OAuthError(OAuthErrorCode.InvalidRequest, "Access tokens are accepted only in the Authorization header"), []);
    }
    const authorization = request.headers.get("authorization");
    // No credentials were presented: the challenge names the product scopes to sign in for.
    if (authorization === null) return challenge(401, undefined, authorizationScopes);
    // RFC 6750 permits several spaces after the scheme; the SDK core splits on one.
    // A header that is not exactly one Bearer token never reaches the verifier.
    const token = /^Bearer +([A-Za-z0-9._~+/-]+=*)$/iu.exec(authorization);
    if (token === null) {
      return refuse(new OAuthError(OAuthErrorCode.InvalidToken, "Malformed Authorization header; expected one Bearer token"), authorizationScopes);
    }
    let auth: AuthInfo;
    try {
      auth = await verifyBearerToken(`Bearer ${token[1]}`, bearer);
    } catch (error) {
      // Hosts use this scope list for sign-in. Request the complete product
      // grant here without requiring every scope for a read-only request.
      return refuse(error, error instanceof OAuthError && error.code === OAuthErrorCode.InvalidToken ? authorizationScopes : []);
    }
    const effectiveScopes = scopeClosure(auth.scopes, implications);
    const missing = requiredScopes.filter((scope) => !effectiveScopes.has(scope));
    return missing.length === 0 ? auth : refuse(new OAuthError(OAuthErrorCode.InsufficientScope, "Insufficient scope"), missing);
  };
}
