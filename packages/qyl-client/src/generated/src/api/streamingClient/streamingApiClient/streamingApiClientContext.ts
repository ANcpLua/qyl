import {type Client, type ClientOptions, getClient} from "@typespec/ts-http-runtime";

export interface StreamingApiClientContext extends Client {

}

export interface StreamingApiClientOptions extends ClientOptions {
    endpoint?: string;
}

export function createStreamingApiClientContext(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: StreamingApiClientOptions,
): StreamingApiClientContext {
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
