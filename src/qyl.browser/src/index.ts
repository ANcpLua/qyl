/**
 * @qyl/browser — Lightweight browser SDK for qyl observability.
 *
 * ESM entry point:
 *   import { init } from '@qyl/browser';
 *   const sdk = init({ endpoint: 'http://localhost:5100' });
 */

export {init} from './core.js';
export type {
    QylConfig,
    QylSdk,
    OtlpSpan,
    OtlpLogRecord,
    OtlpAttribute,
    OtlpAnyValue,
} from './types.js';
