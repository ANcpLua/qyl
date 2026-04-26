import {type Client, type ClientOptions, getClient} from "@typespec/ts-http-runtime";

export interface ServicesApiClientContext extends Client {

}

export interface ServicesApiClientOptions extends ClientOptions {
    endpoint?: string;
}

export function createServicesApiClientContext(
    endpoint: "https://api.staging.qyl.dev" | "https://api.qyl.dev" | string,
    options?: ServicesApiClientOptions,
): ServicesApiClientContext {
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
