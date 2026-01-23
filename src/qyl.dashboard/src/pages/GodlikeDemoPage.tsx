/**
 * GODLIKE Demo Page
 *
 * Showcase of three TUI-inspired components that demonstrate
 * the fusion of terminal aesthetics with modern React/Tailwind.
 *
 * Switch between examples using tabs or keyboard shortcuts:
 * - 1: TUI System Monitor (btop-style)
 * - 2: Trading Trace Viewer (TradingView-style)
 * - 3: Command Center (glances-style)
 */

import { useCallback, useEffect, useState } from 'react';
import { cn } from '@/lib/utils';
import { TuiSystemMonitor, TradingTraceViewer, CommandCenter } from '@/components/godlike';

type DemoTab = 'tui' | 'trading' | 'command';

interface TabConfig {
  id: DemoTab;
  label: string;
  shortcut: string;
  description: string;
  inspiration: string;
}

const tabs: TabConfig[] = [
  {
    id: 'tui',
    label: 'TUI Monitor',
    shortcut: '1',
    description: 'Braille sparklines, text progress bars, Unicode box drawing',
    inspiration: 'btop / bottom',
  },
  {
    id: 'trading',
    label: 'Trading Viewer',
    shortcut: '2',
    description: 'OHLC candles for latency, crosshair, volume bars',
    inspiration: 'TradingView',
  },
  {
    id: 'command',
    label: 'Command Center',
    shortcut: '3',
    description: 'Single pane of glass, keyboard nav, animated text',
    inspiration: 'glances / Architech',
  },
];

export function GodlikeDemoPage() {
  const [activeTab, setActiveTab] = useState<DemoTab>('tui');

  // Keyboard shortcuts to switch tabs
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Don't capture if user is typing in an input
      if (e.target instanceof HTMLInputElement) return;

      switch (e.key) {
        case '1':
          setActiveTab('tui');
          break;
        case '2':
          setActiveTab('trading');
          break;
        case '3':
          setActiveTab('command');
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  return (
    <div className="h-full flex flex-col bg-brutal-black">
      {/* Tab Bar */}
      <div className="flex items-center border-b-2 border-brutal-zinc bg-brutal-carbon">
        {tabs.map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={cn(
              'px-4 py-3 text-sm font-mono transition-colors relative',
              activeTab === tab.id
                ? 'text-signal-orange bg-brutal-dark'
                : 'text-brutal-slate hover:text-brutal-white hover:bg-brutal-dark/50'
            )}
          >
            <div className="flex items-center gap-2">
              <kbd className={cn(
                'kbd text-[10px]',
                activeTab === tab.id && 'bg-signal-orange text-brutal-black'
              )}>
                {tab.shortcut}
              </kbd>
              <span className="uppercase tracking-wider">{tab.label}</span>
            </div>
            {/* Active indicator */}
            {activeTab === tab.id && (
              <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-signal-orange" />
            )}
          </button>
        ))}

        {/* Tab description */}
        <div className="flex-1 px-4 text-right">
          {tabs.filter(t => t.id === activeTab).map(tab => (
            <div key={tab.id} className="text-xs">
              <span className="text-brutal-slate">{tab.description}</span>
              <span className="text-brutal-zinc mx-2">│</span>
              <span className="text-signal-cyan">Inspired by {tab.inspiration}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Content Area */}
      <div className="flex-1 overflow-hidden">
        {activeTab === 'tui' && <TuiSystemMonitor />}
        {activeTab === 'trading' && <TradingTraceViewer />}
        {activeTab === 'command' && <CommandCenter />}
      </div>
    </div>
  );
}

export default GodlikeDemoPage;
