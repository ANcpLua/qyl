/**
 * Shared HTTP helpers for dashboard hooks.
 * All requests include credentials for session-based auth.
 */

export async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {credentials: 'include', ...init});
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
