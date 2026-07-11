/**
 * Shared HTTP helpers for dashboard hooks.
 *
 * When the collector runs in ApiKey mode (QYL_OTLP_AUTH_MODE=ApiKey), the read API requires the
 * collector credential. The dashboard reads it from localStorage under `qyl:api-key` and sends
 * it as the `x-otlp-api-key` header; EventSource consumers (the SSE log stream) append it as the
 * `api_key` query parameter instead, because EventSource cannot set headers. There is no key
 *-entry UI yet — set it via DevTools (`localStorage.setItem('qyl:api-key', '…')`); recorded as
 * the phase-5 scheme in the repo SSOT.
 */

const API_KEY_STORAGE = 'qyl:api-key';
const API_KEY_HEADER = 'x-otlp-api-key';

export function getApiKey(): string | null {
    try {
        return localStorage.getItem(API_KEY_STORAGE);
    } catch {
        return null;
    }
}

/** Appends the collector credential as `api_key` for header-less consumers (EventSource). */
export function withApiKeyQuery(url: string): string {
    const key = getApiKey();
    if (!key) return url;
    return `${url}${url.includes('?') ? '&' : '?'}api_key=${encodeURIComponent(key)}`;
}

function authHeaders(): Record<string, string> {
    const key = getApiKey();
    return key ? {[API_KEY_HEADER]: key} : {};
}

export async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        credentials: 'include',
        ...init,
        headers: {...authHeaders(), ...init?.headers},
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

export async function postJson<T>(url: string, body: unknown): Promise<T> {
    return fetchJson<T>(url, {
        method: 'POST',
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify(body),
    });
}
