import {type Client, type ClientOptions, getClient} from "@typespec/ts-http-runtime";

export interface LogsApiClientContext extends Client {

}

export interface LogsApiClientOptions extends ClientOptions {
    endpoint?: string;
}

export function createLogsApiClientContext(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: LogsApiClientOptions,
): LogsApiClientContext {
    const params: Record<string, any> = {
        endpoint: options?.endpoint ?? "https://api.staging.qyl.dev"
    };
    const resolvedEndpoint = "{endpoint}".replace(/{([^}]+)}/g, (_, key) =>
        key in params ? String(params[key]) : (() => {
            throw new Error(`Missing parameter: ${key}`);
        })()
    );
    return getClient(resolvedEndpoint, {
        ...options
    })
}
