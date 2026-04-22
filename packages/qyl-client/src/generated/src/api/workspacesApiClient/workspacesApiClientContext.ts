import { type Client, type ClientOptions, getClient } from "@typespec/ts-http-runtime";

export interface WorkspacesApiClientContext extends Client {

}export interface WorkspacesApiClientOptions extends ClientOptions {
  endpoint?: string;
}export function createWorkspacesApiClientContext(
  endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
  options?: WorkspacesApiClientOptions,
): WorkspacesApiClientContext {
  const params: Record<string, any> = {
    endpoint: options?.endpoint ?? "https://api.staging.qyl.dev"
  };
  const resolvedEndpoint = "{endpoint}".replace(/{([^}]+)}/g, (_, key) =>
    key in params ? String(params[key]) : (() => { throw new Error(`Missing parameter: ${key}`); })()
  );;return getClient(resolvedEndpoint,{
    ...options
  })
}
