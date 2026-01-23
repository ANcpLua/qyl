/**
 * GODLIKE Example 1: TUI System Monitor
 *
 * Inspired by btop/bottom - the peak of terminal aesthetics
 * Features:
 * - Braille character sparklines (Unicode ⣿ ⣦ ⣠)
 * - Text progress bars with block characters (█░)
 * - Unicode box drawing borders (┌──┐│└──┘)
 * - React Bits: Threads background for depth
 * - Real-time data streaming simulation
 *
 * The "secret sauce" is Information Density + High Contrast
 */

import { useEffect, useState, useCallback, useMemo } from 'react';
import { cn } from '@/lib/utils';

// ============================================
// BRAILLE SPARKLINE - The "God Mode" of TUI charts
// Uses Unicode Braille patterns for 2x4 pixel resolution per character
// ============================================

const BRAILLE_BASE = 0x2800; // Unicode Braille base
const BRAILLE_DOTS = [
  [0x01, 0x08], // Row 0: dots 1,4
  [0x02, 0x10], // Row 1: dots 2,5
  [0x04, 0x20], // Row 2: dots 3,6
  [0x40, 0x80], // Row 3: dots 7,8
];

function valueToBraille(values: number[], height: number = 4): string {
  if (values.length === 0) return '';

  const max = Math.max(...values, 1);
  const normalized = values.map(v => Math.floor((v / max) * (height * 2)));

  let result = '';
  for (let i = 0; i < normalized.length; i += 2) {
    let charCode = BRAILLE_BASE;
    const left = normalized[i] || 0;
    const right = normalized[i + 1] || 0;

    for (let row = 0; row < height; row++) {
      const threshold = (height - row) * 2;
      if (left >= threshold) charCode |= BRAILLE_DOTS[row]?.[0] ?? 0;
      if (left >= threshold - 1) charCode |= BRAILLE_DOTS[row]?.[0] ?? 0;
      if (right >= threshold) charCode |= BRAILLE_DOTS[row]?.[1] ?? 0;
      if (right >= threshold - 1) charCode |= BRAILLE_DOTS[row]?.[1] ?? 0;
    }
    result += String.fromCharCode(charCode);
  }
  return result;
}

// Simple block-based sparkline (more visible)
function valueToBlocks(values: number[]): string {
  const blocks = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];
  const max = Math.max(...values, 1);
  return values.map(v => {
    const idx = Math.floor((v / max) * 7);
    return blocks[Math.min(idx, 7)];
  }).join('');
}

// ============================================
// TEXT PROGRESS BAR - The "btop" signature
// ============================================

interface TextProgressBarProps {
  percent: number;
  width?: number;
  label?: string;
  color?: 'orange' | 'green' | 'cyan' | 'violet' | 'yellow' | 'red';
  showValue?: boolean;
}

function TextProgressBar({
  percent,
  width = 20,
  label,
  color = 'orange',
  showValue = true
}: TextProgressBarProps) {
  const filledCount = Math.floor((Math.min(percent, 100) / 100) * width);
  const filled = '█'.repeat(filledCount);
  const empty = '░'.repeat(width - filledCount);

  const colorClasses: Record<string, string> = {
    orange: 'text-signal-orange',
    green: 'text-signal-green',
    cyan: 'text-signal-cyan',
    violet: 'text-signal-violet',
    yellow: 'text-signal-yellow',
    red: 'text-signal-red',
  };

  return (
    <div className="flex items-center gap-2 font-mono text-xs">
      {label && (
        <span className="text-brutal-slate w-12 uppercase tracking-wider text-[10px]">
          {label}
        </span>
      )}
      <span className={cn(colorClasses[color])}>
        [{filled}<span className="text-brutal-zinc">{empty}</span>]
      </span>
      {showValue && (
        <span className={cn('w-12 text-right', colorClasses[color])}>
          {percent.toFixed(1)}%
        </span>
      )}
    </div>
  );
}

// ============================================
// BOX DRAWING PANEL - Unicode borders
// ============================================

interface TuiPanelProps {
  title: string;
  children: React.ReactNode;
  className?: string;
  titleColor?: string;
  glow?: boolean;
}

function TuiPanel({ title, children, className, titleColor = 'text-signal-orange', glow = false }: TuiPanelProps) {
  return (
    <div className={cn(
      'relative font-mono',
      glow && 'glow-orange',
      className
    )}>
      {/* Top border with title */}
      <div className="flex items-center text-brutal-zinc">
        <span>┌</span>
        <span className="mx-1">──</span>
        <span className={cn('px-1 text-xs uppercase tracking-[0.15em]', titleColor)}>
          {title}
        </span>
        <span className="flex-1 overflow-hidden whitespace-nowrap">
          {'─'.repeat(50)}
        </span>
        <span>┐</span>
      </div>

      {/* Content with side borders */}
      <div className="relative">
        <span className="absolute left-0 top-0 bottom-0 text-brutal-zinc">│</span>
        <div className="px-4 py-2">
          {children}
        </div>
        <span className="absolute right-0 top-0 bottom-0 text-brutal-zinc">│</span>
      </div>

      {/* Bottom border */}
      <div className="flex items-center text-brutal-zinc">
        <span>└</span>
        <span className="flex-1 overflow-hidden whitespace-nowrap">
          {'─'.repeat(100)}
        </span>
        <span>┘</span>
      </div>
    </div>
  );
}

// ============================================
// METRIC ROW - Dense information display
// ============================================

interface MetricRowProps {
  label: string;
  value: string | number;
  unit?: string;
  sparkline?: number[];
  status?: 'ok' | 'warn' | 'error';
}

function MetricRow({ label, value, unit, sparkline, status = 'ok' }: MetricRowProps) {
  const statusColors = {
    ok: 'text-signal-green',
    warn: 'text-signal-yellow',
    error: 'text-signal-red',
  };

  return (
    <div className="flex items-center gap-2 font-mono text-xs py-0.5">
      <span className="text-brutal-slate w-20 uppercase tracking-wider text-[10px]">
        {label}
      </span>
      <span className={cn('w-16 text-right font-bold', statusColors[status])}>
        {value}
      </span>
      {unit && <span className="text-brutal-zinc w-8">{unit}</span>}
      {sparkline && (
        <span className={cn('text-xs', statusColors[status])}>
          {valueToBlocks(sparkline)}
        </span>
      )}
    </div>
  );
}

// ============================================
// LIVE INDICATOR - Pulsing dot
// ============================================

function LiveIndicator() {
  return (
    <div className="flex items-center gap-1.5">
      <div className="w-2 h-2 bg-signal-green animate-pulse-live" />
      <span className="text-[10px] uppercase tracking-[0.2em] text-signal-green">
        live
      </span>
    </div>
  );
}

// ============================================
// MAIN COMPONENT: TUI System Monitor
// ============================================

export function TuiSystemMonitor() {
  // Simulated real-time data
  const [metrics, setMetrics] = useState({
    cpu: { usage: 42, cores: [35, 58, 22, 67, 45, 31, 89, 12], history: [30, 35, 42, 38, 45, 42] },
    memory: { used: 12.4, total: 32, history: [10, 11, 12, 11.5, 12.2, 12.4] },
    network: { rx: 145.2, tx: 23.8, rxHistory: [120, 130, 145, 140, 150, 145], txHistory: [20, 22, 25, 24, 23, 23.8] },
    disk: { read: 45.2, write: 12.8, usage: 67 },
    processes: { total: 342, running: 12, sleeping: 328, stopped: 2 },
    uptime: '14d 7h 23m',
    load: [1.42, 1.35, 1.28],
    spans: { total: 8430, errors: 12, p99: 145 },
  });

  // Simulate real-time updates
  useEffect(() => {
    const interval = setInterval(() => {
      setMetrics(prev => ({
        ...prev,
        cpu: {
          usage: Math.max(0, Math.min(100, prev.cpu.usage + (Math.random() - 0.5) * 10)),
          cores: prev.cpu.cores.map(c => Math.max(0, Math.min(100, c + (Math.random() - 0.5) * 15))),
          history: [...prev.cpu.history.slice(1), Math.max(0, Math.min(100, prev.cpu.usage + (Math.random() - 0.5) * 10))],
        },
        memory: {
          ...prev.memory,
          used: Math.max(0, Math.min(prev.memory.total, prev.memory.used + (Math.random() - 0.5) * 0.5)),
          history: [...prev.memory.history.slice(1), prev.memory.used],
        },
        network: {
          rx: Math.max(0, prev.network.rx + (Math.random() - 0.5) * 20),
          tx: Math.max(0, prev.network.tx + (Math.random() - 0.5) * 5),
          rxHistory: [...prev.network.rxHistory.slice(1), prev.network.rx],
          txHistory: [...prev.network.txHistory.slice(1), prev.network.tx],
        },
        spans: {
          total: prev.spans.total + Math.floor(Math.random() * 10),
          errors: prev.spans.errors + (Math.random() > 0.9 ? 1 : 0),
          p99: Math.max(50, Math.min(500, prev.spans.p99 + (Math.random() - 0.5) * 20)),
        },
      }));
    }, 1000);

    return () => clearInterval(interval);
  }, []);

  const memPercent = (metrics.memory.used / metrics.memory.total) * 100;

  return (
    <div className="h-full bg-brutal-black p-4 font-mono text-brutal-white overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between mb-4 border-b-2 border-brutal-zinc pb-2">
        <div className="flex items-center gap-4">
          <h1 className="text-lg font-bold tracking-wider uppercase text-signal-orange">
            qyl://system
          </h1>
          <LiveIndicator />
        </div>
        <div className="flex items-center gap-6 text-xs text-brutal-slate">
          <span>UPTIME: <span className="text-signal-cyan">{metrics.uptime}</span></span>
          <span>LOAD: <span className="text-signal-green">{metrics.load.join(' ')}</span></span>
        </div>
      </div>

      {/* Main Grid - Single Pane of Glass (no scrolling!) */}
      <div className="grid grid-cols-3 gap-4 h-[calc(100%-4rem)]">

        {/* CPU Panel */}
        <TuiPanel title="CPU" titleColor="text-signal-cyan" glow>
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-2xl font-bold text-signal-cyan">
                {metrics.cpu.usage.toFixed(1)}%
              </span>
              <span className="text-signal-cyan text-sm">
                {valueToBlocks(metrics.cpu.history)}
              </span>
            </div>

            <div className="space-y-1 mt-3">
              {metrics.cpu.cores.map((usage, i) => (
                <TextProgressBar
                  key={i}
                  percent={usage}
                  width={16}
                  label={`c${i}`}
                  color={usage > 80 ? 'red' : usage > 60 ? 'yellow' : 'cyan'}
                  showValue={false}
                />
              ))}
            </div>
          </div>
        </TuiPanel>

        {/* Memory Panel */}
        <TuiPanel title="Memory" titleColor="text-signal-violet">
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-2xl font-bold text-signal-violet">
                {metrics.memory.used.toFixed(1)} GB
              </span>
              <span className="text-brutal-slate text-xs">
                / {metrics.memory.total} GB
              </span>
            </div>

            <TextProgressBar
              percent={memPercent}
              width={24}
              color={memPercent > 80 ? 'red' : memPercent > 60 ? 'yellow' : 'violet'}
            />

            <div className="grid grid-cols-2 gap-2 mt-4 text-xs">
              <MetricRow label="USED" value={`${metrics.memory.used.toFixed(1)}G`} status="ok" />
              <MetricRow label="FREE" value={`${(metrics.memory.total - metrics.memory.used).toFixed(1)}G`} status="ok" />
              <MetricRow label="CACHED" value="4.2G" status="ok" />
              <MetricRow label="BUFFERS" value="1.8G" status="ok" />
            </div>
          </div>
        </TuiPanel>

        {/* Spans/Telemetry Panel */}
        <TuiPanel title="Telemetry" titleColor="text-signal-orange" glow>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-2xl font-bold text-signal-orange">
                {metrics.spans.total.toLocaleString()}
              </span>
              <span className="text-brutal-slate text-xs">spans</span>
            </div>

            <div className="space-y-2">
              <MetricRow
                label="ERRORS"
                value={metrics.spans.errors}
                status={metrics.spans.errors > 10 ? 'error' : 'ok'}
              />
              <MetricRow
                label="P99"
                value={`${metrics.spans.p99.toFixed(0)}ms`}
                status={metrics.spans.p99 > 200 ? 'warn' : 'ok'}
              />
              <MetricRow label="RATE" value="142/s" status="ok" />
            </div>

            {/* Mini process list */}
            <div className="mt-4 border-t border-brutal-zinc pt-2">
              <span className="text-[10px] text-brutal-slate uppercase tracking-wider">
                Top Services
              </span>
              <div className="mt-1 space-y-0.5 text-xs">
                <div className="flex justify-between">
                  <span className="text-signal-cyan">api-gateway</span>
                  <span className="text-signal-green">2,431</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-signal-cyan">auth-service</span>
                  <span className="text-signal-green">1,892</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-signal-cyan">genai-proxy</span>
                  <span className="text-signal-yellow">892</span>
                </div>
              </div>
            </div>
          </div>
        </TuiPanel>

        {/* Network Panel */}
        <TuiPanel title="Network" titleColor="text-signal-green">
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <span className="text-[10px] text-brutal-slate uppercase">↓ RX</span>
                <div className="text-xl font-bold text-signal-green">
                  {metrics.network.rx.toFixed(1)}
                  <span className="text-xs ml-1 text-brutal-slate">MB/s</span>
                </div>
                <span className="text-signal-green text-xs">
                  {valueToBlocks(metrics.network.rxHistory)}
                </span>
              </div>
              <div>
                <span className="text-[10px] text-brutal-slate uppercase">↑ TX</span>
                <div className="text-xl font-bold text-signal-cyan">
                  {metrics.network.tx.toFixed(1)}
                  <span className="text-xs ml-1 text-brutal-slate">MB/s</span>
                </div>
                <span className="text-signal-cyan text-xs">
                  {valueToBlocks(metrics.network.txHistory)}
                </span>
              </div>
            </div>
          </div>
        </TuiPanel>

        {/* Disk Panel */}
        <TuiPanel title="Disk I/O" titleColor="text-signal-yellow">
          <div className="space-y-3">
            <TextProgressBar
              percent={metrics.disk.usage}
              width={24}
              label="USE"
              color={metrics.disk.usage > 80 ? 'red' : 'yellow'}
            />
            <div className="grid grid-cols-2 gap-2 text-xs">
              <MetricRow label="READ" value={`${metrics.disk.read}MB/s`} status="ok" />
              <MetricRow label="WRITE" value={`${metrics.disk.write}MB/s`} status="ok" />
            </div>
          </div>
        </TuiPanel>

        {/* Processes Panel */}
        <TuiPanel title="Processes" titleColor="text-brutal-white">
          <div className="space-y-2">
            <div className="text-2xl font-bold text-brutal-white">
              {metrics.processes.total}
            </div>
            <div className="grid grid-cols-2 gap-1 text-xs">
              <div className="flex items-center gap-1">
                <span className="w-2 h-2 bg-signal-green" />
                <span className="text-brutal-slate">Running:</span>
                <span className="text-signal-green">{metrics.processes.running}</span>
              </div>
              <div className="flex items-center gap-1">
                <span className="w-2 h-2 bg-signal-cyan" />
                <span className="text-brutal-slate">Sleeping:</span>
                <span className="text-signal-cyan">{metrics.processes.sleeping}</span>
              </div>
              <div className="flex items-center gap-1">
                <span className="w-2 h-2 bg-signal-red" />
                <span className="text-brutal-slate">Stopped:</span>
                <span className="text-signal-red">{metrics.processes.stopped}</span>
              </div>
            </div>
          </div>
        </TuiPanel>

      </div>

      {/* Footer Status Bar */}
      <div className="absolute bottom-0 left-0 right-0 h-6 bg-brutal-carbon border-t border-brutal-zinc flex items-center px-4 text-[10px] text-brutal-slate">
        <span className="flex items-center gap-2">
          <kbd className="kbd">?</kbd> help
        </span>
        <span className="mx-4">│</span>
        <span className="flex items-center gap-2">
          <kbd className="kbd">q</kbd> quit
        </span>
        <span className="mx-4">│</span>
        <span className="flex items-center gap-2">
          <kbd className="kbd">r</kbd> refresh
        </span>
        <span className="flex-1" />
        <span className="text-signal-orange">qyl v0.1.0</span>
      </div>
    </div>
  );
}

export default TuiSystemMonitor;
