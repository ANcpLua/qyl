import { parse } from "uri-template";
import { OnboardingApiClientContext } from "./onboardingApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";
import { jsonHandshakeSessionEntityToApplicationTransform, jsonHandshakeStartRequestToTransportTransform, jsonHandshakeVerifyRequestToTransportTransform, jsonHandshakeVerifyResponseToApplicationTransform } from "../../models/internal/serializers.js";
import { type HandshakeSessionEntity, HandshakeStartRequest, HandshakeVerifyRequest, type HandshakeVerifyResponse } from "../../models/models.js";

export interface StartHandshakeOptions extends OperationOptions {}
/**
 * Start a new handshake session
 *
 * @param {OnboardingApiClientContext} client
 * @param {HandshakeStartRequest} request
 * @param {StartHandshakeOptions} [options]
 */
export async function startHandshake(
  client: OnboardingApiClientContext,
  request: HandshakeStartRequest,
  options?: StartHandshakeOptions,
): Promise<HandshakeSessionEntity> {
  const path = parse("/api/v1/onboarding/handshake").expand({});
  const httpRequestOptions = {
    headers: {},body: jsonHandshakeStartRequestToTransportTransform(request),
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonHandshakeSessionEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface VerifyHandshakeOptions extends OperationOptions {}
/**
 * Complete handshake verification
 *
 * @param {OnboardingApiClientContext} client
 * @param {string} sessionId
 * @param {HandshakeVerifyRequest} request
 * @param {VerifyHandshakeOptions} [options]
 */
export async function verifyHandshake(
  client: OnboardingApiClientContext,
  sessionId: string,
  request: HandshakeVerifyRequest,
  options?: VerifyHandshakeOptions,
): Promise<HandshakeVerifyResponse> {
  const path = parse("/api/v1/onboarding/handshake/{sessionId}/verify").expand({
    sessionId: sessionId
  });
  const httpRequestOptions = {
    headers: {},body: jsonHandshakeVerifyRequestToTransportTransform(request),
  };
  const response = await client.pathUnchecked(path).post(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonHandshakeVerifyResponseToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
export interface GetHandshakeOptions extends OperationOptions {}
/**
 * Get handshake session status
 *
 * @param {OnboardingApiClientContext} client
 * @param {string} sessionId
 * @param {GetHandshakeOptions} [options]
 */
export async function getHandshake(
  client: OnboardingApiClientContext,
  sessionId: string,
  options?: GetHandshakeOptions,
): Promise<HandshakeSessionEntity> {
  const path = parse("/api/v1/onboarding/handshake/{sessionId}").expand({
    sessionId: sessionId
  });
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && response.headers["content-type"]?.includes("application/json")) {
    return jsonHandshakeSessionEntityToApplicationTransform(response.body)!;
  }
  throw createRestError(response);
}
;
