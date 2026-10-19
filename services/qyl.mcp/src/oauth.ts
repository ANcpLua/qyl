import {
  createRemoteJWKSet,
  jwtVerify,
  type JWTPayload,
  type JWTVerifyGetKey,
} from "jose";
import {
  OAuthError,
  OAuthErrorCode,
  type AuthInfo,
  type OAuthMetadata,
  type OAuthTokenVerifier,
} from "@modelcontextprotocol/server";

import { McpScopeValues } from "./contracts/generated/api.js";

import {
  fetchAuthorizationServerMetadata,
  secureMetadataUrl,
  validateAuthorizationServerCapabilities,
} from "./oauth-metadata.js";
import {
  validateAuthorizationExtensions,
  type AuthorizationExtension,
} from "./auth-extensions.js";

export const QYL_MCP_ISSUER = "https://qyl-eu.eu.auth0.com/";
export const QYL_MCP_RESOURCE = "https://mcp.qyl.at/mcp";
/** Every hosted request carries it: the read tools and Events. */
export const QYL_MCP_SCOPE = McpScopeValues.read;
/** The create and update tools add it: annotations and saved queries. */
export const QYL_MCP_WRITE_SCOPE = McpScopeValues.write;
/** The one delete tool adds it. No scope deletes telemetry. */
export const QYL_MCP_DELETE_SCOPE = McpScopeValues.delete;
/** What the protected-resource metadata advertises in `scopes_supported`. */
export const QYL_MCP_SCOPES_SUPPORTED: readonly string[] = [
  QYL_MCP_SCOPE,
  QYL_MCP_WRITE_SCOPE,
  QYL_MCP_DELETE_SCOPE,
];

export interface HostedOAuth {
  readonly requiredScopes: string[];
  readonly scopesSupported: string[];
  readonly oauthMetadata: OAuthMetadata;
  readonly verifier: OAuthTokenVerifier;
  readonly extensions: readonly AuthorizationExtension[];
}

function invalidToken(): OAuthError {
  return new OAuthError(OAuthErrorCode.InvalidToken, "Access token verification failed");
}

async function verifyJwt(
  key: JWTVerifyGetKey,
  token: string,
  issuer: string,
): Promise<JWTPayload> {
  try {
    // The audience is not compared here: the verifier reports it as
    // AuthInfo.resource and the gate's expectedResource compares it, so one
    // place decides whether a token was issued for this server.
    const { payload } = await jwtVerify(token, key, {
      issuer,
      algorithms: ["RS256"],
      typ: "at+jwt",
      requiredClaims: ["iss", "sub", "aud", "exp", "iat", "jti", "client_id"],
    });
    return payload;
  } catch {
    throw invalidToken();
  }
}

/**
 * RFC 9068 access tokens to the SDK's AuthInfo (authorization.md, "Verify tokens
 * your way"): every rejection is OAuthError(InvalidToken), which the gate answers
 * with 401 and its challenge; `expiresAt` comes from `exp`; `resource` is the
 * token's `aud` entry on this server's origin, absent when it has none, and the
 * gate's `expectedResource` decides whether that is this server.
 */
export function createJwtTokenVerifier(params: {
  issuer: string;
  /** This server's resource identifier; its origin selects the `aud` entry to report. */
  resource: URL;
  key: JWTVerifyGetKey;
}): OAuthTokenVerifier {
  return {
    async verifyAccessToken(token: string): Promise<AuthInfo> {
      const payload = await verifyJwt(params.key, token, params.issuer);
      if (
        typeof payload.sub !== "string"
        || payload.sub.length === 0
        || typeof payload.client_id !== "string"
        || payload.client_id.length === 0
        || typeof payload.exp !== "number"
        || typeof payload.jti !== "string" || payload.jti.length === 0
        || typeof payload.iat !== "number" || payload.iat > Math.floor(Date.now() / 1_000)
        || (payload.scope !== undefined && (typeof payload.scope !== "string"
          || !/^[\x21\x23-\x5B\x5D-\x7E]+(?: [\x21\x23-\x5B\x5D-\x7E]+)*$/u.test(payload.scope)))
      ) {
        throw invalidToken();
      }
      // `aud` is one value or a list: report the entry on this server's origin, if any.
      const audience = [payload.aud ?? []].flat()
        .find((aud) => URL.canParse(aud) && new URL(aud).origin === params.resource.origin);
      return {
        token,
        clientId: payload.client_id,
        scopes: typeof payload.scope === "string"
          ? payload.scope.split(" ").filter(Boolean)
          : [],
        expiresAt: payload.exp,
        ...(audience === undefined ? {} : { resource: new URL(audience) }),
        extra: { subject: payload.sub },
      };
    },
  };
}

/** Auth0 remains the authorization server; every optional grant ends here as
 * an audience-bound access token, never an ID token, ID-JAG or client assertion.
 */
export async function loadHostedOAuth(
  resourceServerUrl: URL,
  options: { extensions?: readonly AuthorizationExtension[] } = {},
): Promise<HostedOAuth> {
  const issuer = new URL(QYL_MCP_ISSUER);
  const oauthMetadata = await fetchAuthorizationServerMetadata(issuer);
  validateAuthorizationServerCapabilities(oauthMetadata);
  const extensions = options.extensions ?? [];
  validateAuthorizationExtensions(oauthMetadata, extensions);
  const jwksUrl = secureMetadataUrl(oauthMetadata.jwks_uri, "jwks_uri");
  return {
    // Only reads are required of every request; the write tools ask for their
    // own scope through a step-up challenge (tool-scopes.ts).
    requiredScopes: [QYL_MCP_SCOPE],
    scopesSupported: [...QYL_MCP_SCOPES_SUPPORTED],
    oauthMetadata,
    extensions,
    verifier: createJwtTokenVerifier({
      issuer: oauthMetadata.issuer,
      resource: resourceServerUrl,
      key: createRemoteJWKSet(jwksUrl),
    }),
  };
}
