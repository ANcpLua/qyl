/**
 * JS error and unhandled promise rejection capture as OTLP log records.
 */

import type { OtlpLogRecord } from './types.js';
import { dateNowNano } from './context.js';
import type { Transport } from './transport.js';

// OTLP severity numbers
const SEVERITY_ERROR = 17;

function errorToLog(error: Error | string, source?: string): OtlpLogRecord {
  const isError = error instanceof Error;
  const message = isError ? error.message : String(error);
  const stack = isError ? error.stack ?? '' : '';
  const type = isError ? error.constructor.name : 'Error';

  return {
    timeUnixNano: dateNowNano(),
    severityNumber: SEVERITY_ERROR,
    severityText: 'ERROR',
    body: { stringValue: message },
    attributes: [
      { key: 'exception.type', value: { stringValue: type } },
      { key: 'exception.message', value: { stringValue: message } },
      { key: 'exception.stacktrace', value: { stringValue: stack } },
      { key: 'browser.url', value: { stringValue: location.href } },
      ...(source ? [{ key: 'error.source', value: { stringValue: source } }] : []),
    ],
  };
}

let errorHandler: ((event: ErrorEvent) => void) | null = null;
let rejectionHandler: ((event: PromiseRejectionEvent) => void) | null = null;

export function startErrorCapture(transport: Transport): void {
  errorHandler = (event: ErrorEvent) => {
    const err = event.error instanceof Error ? event.error : new Error(event.message);
    transport.addLog(errorToLog(err, 'window.onerror'));
  };

  rejectionHandler = (event: PromiseRejectionEvent) => {
    const reason = event.reason instanceof Error ? event.reason : new Error(String(event.reason));
    transport.addLog(errorToLog(reason, 'unhandledrejection'));
  };

  window.addEventListener('error', errorHandler);
  window.addEventListener('unhandledrejection', rejectionHandler);
}

export function stopErrorCapture(): void {
  if (errorHandler) {
    window.removeEventListener('error', errorHandler);
    errorHandler = null;
  }
  if (rejectionHandler) {
    window.removeEventListener('unhandledrejection', rejectionHandler);
    rejectionHandler = null;
  }
}
