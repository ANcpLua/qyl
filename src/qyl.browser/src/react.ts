/**
 * React integration for @qyl/browser.
 *
 * Usage:
 *   import { QylProvider } from '@qyl/browser/react';
 *   <QylProvider config={{ endpoint: 'http://localhost:5100' }}>
 *     <App />
 *   </QylProvider>
 */

import { createElement, useEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import { init } from './core.js';
import type { QylConfig, QylSdk } from './types.js';

interface QylProviderProps {
  config: QylConfig;
  children: ReactNode;
}

/**
 * React provider that initializes the qyl browser SDK on mount
 * and shuts it down on unmount.
 */
export function QylProvider({ config, children }: QylProviderProps): ReactNode {
  const sdkRef = useRef<QylSdk | null>(null);
  const configKey = config.endpoint + (config.serviceName ?? '');

  useEffect(() => {
    sdkRef.current = init(config);
    return () => {
      sdkRef.current?.shutdown();
      sdkRef.current = null;
    };
  }, [configKey]);

  return createElement('span', { 'data-qyl': '' }, children);
}

export type { QylConfig, QylSdk };
