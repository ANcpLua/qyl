/**
 * IIFE entry point for <script> tag usage.
 *
 * Usage:
 *   <script>window.qyl = { endpoint: 'http://localhost:5100' };</script>
 *   <script src="https://your-cdn/qyl.js"></script>
 *
 * The SDK auto-initializes from window.qyl config.
 */

import {init} from './core.js';
import type {QylConfig, QylSdk} from './types.js';

(function autoInit() {
    const existing = window.qyl;
    if (!existing || typeof existing !== 'object') return;

    // Already initialized (QylSdk has a flush method)
    if ('flush' in existing) return;

    // Treat as config
    const config = existing as Partial<QylConfig>;
    if (!config.endpoint) {
        console.warn('[qyl] Missing endpoint in window.qyl config. SDK not initialized.');
        return;
    }

    const sdk: QylSdk = init(config as QylConfig);
    window.qyl = sdk;
})();
