import { type Client, type ClientOptions, getClient } from "@typespec/ts-http-runtime";

export interface SearchApiClientContext extends Client {

}export interface SearchApiClientOptions extends ClientOptions {
  endpoint?: string;
}export function createSearchApiClientContext(
  endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
  options?: SearchApiClientOptions,
): SearchApiClientContext {
  const params: Record<string, any> = {
    endpoint: options?.endpoint ?? "https://api.staging.qyl.dev"
  };
  const resolvedEndpoint = "{endpoint}".replace(/{([^}]+)}/g, (_, key) =>
    key in params ? String(params[key]) : (() => { throw new Error(`Missing parameter: ${key}`); })()
  );;return getClient(resolvedEndpoint,{
    ...options
  })
}
