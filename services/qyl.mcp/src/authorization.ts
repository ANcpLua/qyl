import {
  bearerAuthChallengeResponse,
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

/**
 * All grants converge on this gate. Extensions never see an unverified token.
 *
 * The SDK's bearer core does the verification (authorization.md, "Require a bearer
 * token on a web-standard host"): it runs the verifier, compares the reported
 * resource with `expectedResource`, rejects a token without `exp` or past it, and
 * stamps `resourceMetadataUrl` onto the AuthInfo it returns, so per-tool scope
 * challenges advertise the same document. Every refusal is formatted by the SDK's
 * one challenge formatter, so the metadata URL is configured once, here.
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
  const refuse = (error: unknown, scopes: readonly string[]): Response => {
    const response = bearerAuthChallengeResponse(error, {
      requiredScopes: [...scopes],
      ...(bearer.resourceMetadataUrl === undefined ? {} : { resourceMetadataUrl: bearer.resourceMetadataUrl }),
    });
    response.headers.set("cache-control", "no-store");
    return response;
  };
  return async (request) => {
    if (new URL(request.url).searchParams.has("access_token")) {
      return refuse(new OAuthError(OAuthErrorCode.InvalidRequest, "Access tokens are accepted only in the Authorization header"), []);
    }
    const authorization = request.headers.get("authorization");
    // RFC 6750 permits several spaces after the scheme; the SDK core splits on one.
    // A header that is not exactly one Bearer token never reaches the verifier.
    const token = authorization === null ? null : /^Bearer +([A-Za-z0-9._~+/-]+=*)$/iu.exec(authorization);
    if (authorization !== null && token === null) {
      return refuse(new OAuthError(OAuthErrorCode.InvalidToken, "Malformed Authorization header; expected one Bearer token"), authorizationScopes);
    }
    let auth: AuthInfo;
    try {
      auth = await verifyBearerToken(token === null ? undefined : `Bearer ${token[1]}`, bearer);
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
