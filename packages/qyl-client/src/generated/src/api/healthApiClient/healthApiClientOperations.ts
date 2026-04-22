import { parse } from "uri-template";
import { HealthApiClientContext } from "./healthApiClientContext.js";
import { createRestError } from "../../helpers/error.js";
import type { OperationOptions } from "../../helpers/interfaces.js";

export interface AliveOptions extends OperationOptions {}
/**
 * Liveness probe — runs live-tagged health checks.
 *
 * @param {HealthApiClientContext} client
 * @param {AliveOptions} [options]
 */
export async function alive(
  client: HealthApiClientContext,
  options?: AliveOptions,
): Promise<void> {
  const path = parse("/alive").expand({});
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && !response.body) {
    return;
  }
  if (+response.status === 503 && !response.body) {
    return;
  }
  throw createRestError(response);
}
;
export interface ReadyOptions extends OperationOptions {}
/**
 * Readiness probe — runs all ready-tagged health checks.
 *
 * @param {HealthApiClientContext} client
 * @param {ReadyOptions} [options]
 */
export async function ready(
  client: HealthApiClientContext,
  options?: ReadyOptions,
): Promise<void> {
  const path = parse("/health").expand({});
  const httpRequestOptions = {
    headers: {},
  };
  const response = await client.pathUnchecked(path).get(httpRequestOptions);


  if (typeof options?.operationOptions?.onResponse === "function") {
    options?.operationOptions?.onResponse(response);
  }
  if (+response.status === 200 && !response.body) {
    return;
  }
  if (+response.status === 503 && !response.body) {
    return;
  }
  throw createRestError(response);
}
;
