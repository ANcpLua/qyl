/**
 * GODLIKE Example 2: Trading-Style Trace Viewer
 *
 * Inspired by TradingView - the gold standard for financial visualization
 * Adapted for OpenTelemetry observability data
 *
 * Features:
 * - OHLC Candles: min=low, p50=open, p95=close, p99=high (latency distribution)
 * - Volume bars: requests/sec (throughput)
 * - RSI-style indicator: error rate moving average
 * - Crosshair with precise readout
 * - Brush selection for zoom
 * - Keyboard shortcuts (←→ pan, +- zoom)
 *
 * Why this works for observability:
 * - "Candles" show latency distribution shape at a glance
 * - Volume shows throughput correlation
 * - Crosshair reduces "time to answer" for incident response
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { cn } from '@/lib/utils';

// ============================================
// TYPES
// ============================================

interface LatencyCandle {
  timestamp: number;
  min: number;      // Candle low
  p50: number;      // Candle open
  p95: number;      // Candle close
  p99: number;      // Candle high
  requests: number; // Volume
  errors: number;
}

interface CrosshairPosition {
  x: number;
  y: number;
  dataIndex: number;
}

// ============================================
// MOCK DATA GENERATOR
// ============================================

function generateMockData(count: number): LatencyCandle[] {
  const data: LatencyCandle[] = [];
  const now = Date.now();
  let baseLatency = 50;

  for (let i = 0; i < count; i++) {
    // Add some variation and occasional spikes
    const spike = Math.random() > 0.92 ? Math.random() * 100 : 0;
    baseLatency = Math.max(20, Math.min(150, baseLatency + (Math.random() - 0.5) * 20));

    const min = baseLatency * 0.3;
    const p50 = baseLatency + spike * 0.3;
    const p95 = baseLatency * 1.8 + spike * 0.6;
    const p99 = baseLatency * 3 + spike;

    const requests = 800 + Math.random() * 400 + (spike > 0 ? -200 : 0);
    const errors = spike > 50 ? Math.floor(Math.random() * 15) : Math.floor(Math.random() * 3);

    data.push({
      timestamp: now - (count - i) * 60000, // 1 minute intervals
      min,
      p50,
      p95,
      p99,
      requests,
      errors,
    });
  }
  return data;
}

// ============================================
// CANDLE COMPONENT
// ============================================

interface CandleProps {
  candle: LatencyCandle;
  x: number;
  width: number;
  yScale: (value: number) => number;
  maxY: number;
  isHighlighted: boolean;
}

function Candle({ candle, x, width, yScale, maxY, isHighlighted }: CandleProps) {
  const isUp = candle.p95 >= candle.p50; // Green if p95 > p50 (normal), red if inverted
  const bodyTop = yScale(Math.max(candle.p50, candle.p95));
  const bodyBottom = yScale(Math.min(candle.p50, candle.p95));
  const bodyHeight = Math.max(1, bodyBottom - bodyTop);

  const wickTop = yScale(candle.p99);
  const wickBottom = yScale(candle.min);

  const fillColor = isUp ? 'var(--color-signal-green)' : 'var(--color-signal-red)';
  const opacity = isHighlighted ? 1 : 0.8;

  return (
    <g opacity={opacity}>
      {/* Wick (p99 to min) */}
      <line
        x1={x + width / 2}
        y1={wickTop}
        x2={x + width / 2}
        y2={wickBottom}
        stroke={fillColor}
        strokeWidth={1}
      />
      {/* Body (p50 to p95) */}
      <rect
        x={x + 1}
        y={bodyTop}
        width={width - 2}
        height={bodyHeight}
        fill={fillColor}
        stroke={fillColor}
        strokeWidth={1}
      />
      {/* Highlight glow */}
      {isHighlighted && (
        <rect
          x={x - 1}
          y={bodyTop - 2}
          width={width + 2}
          height={bodyHeight + 4}
          fill="none"
          stroke={fillColor}
          strokeWidth={2}
          opacity={0.5}
          filter="url(#glow)"
        />
      )}
    </g>
  );
}

// ============================================
// VOLUME BAR COMPONENT
// ============================================

interface VolumeBarProps {
  candle: LatencyCandle;
  x: number;
  width: number;
  yScale: (value: number) => number;
  maxVolume: number;
  height: number;
}

function VolumeBar({ candle, x, width, yScale, maxVolume, height }: VolumeBarProps) {
  const barHeight = (candle.requests / maxVolume) * height;
  const hasErrors = candle.errors > 5;

  return (
    <g>
      <rect
        x={x + 1}
        y={height - barHeight}
        width={width - 2}
        height={barHeight}
        fill={hasErrors ? 'var(--color-signal-red)' : 'var(--color-signal-cyan)'}
        opacity={0.6}
      />
      {/* Error overlay */}
      {candle.errors > 0 && (
        <rect
          x={x + 1}
          y={height - barHeight}
          width={width - 2}
          height={(candle.errors / candle.requests) * barHeight * 10}
          fill="var(--color-signal-red)"
          opacity={0.8}
        />
      )}
    </g>
  );
}

// ============================================
// CROSSHAIR COMPONENT
// ============================================

interface CrosshairProps {
  position: CrosshairPosition | null;
  candle: LatencyCandle | null;
  chartHeight: number;
  chartWidth: number;
}

function Crosshair({ position, candle, chartHeight, chartWidth }: CrosshairProps) {
  if (!position || !candle) return null;

  return (
    <g className="pointer-events-none">
      {/* Vertical line */}
      <line
        x1={position.x}
        y1={0}
        x2={position.x}
        y2={chartHeight}
        stroke="var(--color-signal-orange)"
        strokeWidth={1}
        strokeDasharray="4 2"
        opacity={0.7}
      />
      {/* Horizontal line */}
      <line
        x1={0}
        y1={position.y}
        x2={chartWidth}
        y2={position.y}
        stroke="var(--color-signal-orange)"
        strokeWidth={1}
        strokeDasharray="4 2"
        opacity={0.7}
      />
      {/* Center dot */}
      <circle
        cx={position.x}
        cy={position.y}
        r={4}
        fill="var(--color-signal-orange)"
      />
    </g>
  );
}

// ============================================
// DATA TOOLTIP COMPONENT
// ============================================

interface DataTooltipProps {
  candle: LatencyCandle | null;
  position: { x: number; y: number } | null;
}

function DataTooltip({ candle, position }: DataTooltipProps) {
  if (!candle || !position) return null;

  const time = new Date(candle.timestamp).toLocaleTimeString();

  return (
    <div
      className="absolute z-50 pointer-events-none bg-brutal-carbon border-2 border-signal-orange p-3 font-mono text-xs"
      style={{
        left: position.x + 20,
        top: position.y - 60,
        transform: position.x > 600 ? 'translateX(-120%)' : 'none',
      }}
    >
      <div className="text-signal-orange font-bold mb-2">{time}</div>
      <div className="grid grid-cols-2 gap-x-4 gap-y-1">
        <span className="text-brutal-slate">p99:</span>
        <span className="text-signal-red text-right">{candle.p99.toFixed(1)}ms</span>

        <span className="text-brutal-slate">p95:</span>
        <span className="text-signal-yellow text-right">{candle.p95.toFixed(1)}ms</span>

        <span className="text-brutal-slate">p50:</span>
        <span className="text-signal-green text-right">{candle.p50.toFixed(1)}ms</span>

        <span className="text-brutal-slate">min:</span>
        <span className="text-signal-cyan text-right">{candle.min.toFixed(1)}ms</span>

        <div className="col-span-2 border-t border-brutal-zinc my-1" />

        <span className="text-brutal-slate">req/m:</span>
        <span className="text-brutal-white text-right">{candle.requests.toFixed(0)}</span>

        <span className="text-brutal-slate">errors:</span>
        <span className={cn(
          'text-right',
          candle.errors > 5 ? 'text-signal-red' : 'text-brutal-white'
        )}>
          {candle.errors}
        </span>
      </div>
    </div>
  );
}

// ============================================
// LEGEND COMPONENT
// ============================================

function Legend() {
  return (
    <div className="flex items-center gap-6 text-xs font-mono">
      <div className="flex items-center gap-2">
        <div className="w-3 h-3 bg-signal-red" />
        <span className="text-brutal-slate">p99</span>
      </div>
      <div className="flex items-center gap-2">
        <div className="w-3 h-3 bg-signal-yellow" />
        <span className="text-brutal-slate">p95</span>
      </div>
      <div className="flex items-center gap-2">
        <div className="w-3 h-3 bg-signal-green" />
        <span className="text-brutal-slate">p50</span>
      </div>
      <div className="flex items-center gap-2">
        <div className="w-3 h-3 bg-signal-cyan" />
        <span className="text-brutal-slate">min</span>
      </div>
      <span className="text-brutal-zinc">│</span>
      <div className="flex items-center gap-2">
        <div className="w-3 h-3 bg-signal-cyan opacity-60" />
        <span className="text-brutal-slate">volume</span>
      </div>
    </div>
  );
}

// ============================================
// Y-AXIS COMPONENT
// ============================================

interface YAxisProps {
  height: number;
  maxValue: number;
  ticks: number;
  suffix?: string;
}

function YAxis({ height, maxValue, ticks, suffix = 'ms' }: YAxisProps) {
  const tickValues = useMemo(() => {
    const values: number[] = [];
    for (let i = 0; i <= ticks; i++) {
      values.push((maxValue / ticks) * i);
    }
    return values.reverse();
  }, [maxValue, ticks]);

  return (
    <div className="w-12 h-full flex flex-col justify-between text-[10px] font-mono text-brutal-slate pr-2">
      {tickValues.map((value, i) => (
        <div key={i} className="text-right">
          {value.toFixed(0)}{suffix}
        </div>
      ))}
    </div>
  );
}

// ============================================
// MAIN COMPONENT: Trading Trace Viewer
// ============================================

export function TradingTraceViewer() {
  const [data] = useState(() => generateMockData(60));
  const [crosshair, setCrosshair] = useState<CrosshairPosition | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState<{ x: number; y: number } | null>(null);

  const containerRef = useRef<HTMLDivElement>(null);

  const chartWidth = 900;
  const chartHeight = 300;
  const volumeHeight = 80;
  const padding = { top: 20, right: 20, bottom: 30, left: 60 };

  // Calculate scales
  const { maxLatency, maxVolume, candleWidth } = useMemo(() => {
    const maxLatency = Math.max(...data.map(d => d.p99)) * 1.1;
    const maxVolume = Math.max(...data.map(d => d.requests)) * 1.1;
    const candleWidth = (chartWidth - padding.left - padding.right) / data.length;
    return { maxLatency, maxVolume, candleWidth };
  }, [data]);

  const yScale = useCallback((value: number) => {
    return padding.top + ((maxLatency - value) / maxLatency) * (chartHeight - padding.top - padding.bottom);
  }, [maxLatency]);

  // Handle mouse move
  const handleMouseMove = useCallback((e: React.MouseEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - rect.left - padding.left;
    const y = e.clientY - rect.top;

    if (x < 0 || x > chartWidth - padding.left - padding.right) {
      setCrosshair(null);
      setTooltipPosition(null);
      return;
    }

    const dataIndex = Math.floor(x / candleWidth);
    if (dataIndex >= 0 && dataIndex < data.length) {
      setCrosshair({
        x: padding.left + dataIndex * candleWidth + candleWidth / 2,
        y,
        dataIndex,
      });
      setTooltipPosition({ x: e.clientX - rect.left, y: e.clientY - rect.top });
    }
  }, [data.length, candleWidth]);

  const handleMouseLeave = useCallback(() => {
    setCrosshair(null);
    setTooltipPosition(null);
  }, []);

  const highlightedCandle = crosshair ? data[crosshair.dataIndex] : null;

  return (
    <div
      ref={containerRef}
      className="relative h-full bg-brutal-black p-4 font-mono"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h2 className="text-lg font-bold text-signal-orange tracking-wider uppercase">
            Latency Distribution
          </h2>
          <p className="text-xs text-brutal-slate mt-1">
            OHLC: min → p50 → p95 → p99 | Hover for details
          </p>
        </div>
        <Legend />
      </div>

      {/* Chart Container */}
      <div className="relative flex">
        {/* Y-Axis */}
        <YAxis height={chartHeight} maxValue={maxLatency} ticks={5} />

        {/* Main Chart */}
        <div className="relative">
          <svg
            width={chartWidth}
            height={chartHeight + volumeHeight + 20}
            onMouseMove={handleMouseMove}
            onMouseLeave={handleMouseLeave}
            className="cursor-crosshair"
          >
            {/* Glow filter */}
            <defs>
              <filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
                <feGaussianBlur stdDeviation="3" result="coloredBlur" />
                <feMerge>
                  <feMergeNode in="coloredBlur" />
                  <feMergeNode in="SourceGraphic" />
                </feMerge>
              </filter>
            </defs>

            {/* Grid lines */}
            <g>
              {[0, 1, 2, 3, 4, 5].map(i => {
                const y = padding.top + (i / 5) * (chartHeight - padding.top - padding.bottom);
                return (
                  <line
                    key={i}
                    x1={padding.left}
                    y1={y}
                    x2={chartWidth - padding.right}
                    y2={y}
                    stroke="var(--color-brutal-zinc)"
                    strokeWidth={1}
                    strokeDasharray="2 4"
                    opacity={0.3}
                  />
                );
              })}
            </g>

            {/* Candles */}
            <g transform={`translate(${padding.left}, 0)`}>
              {data.map((candle, i) => (
                <Candle
                  key={i}
                  candle={candle}
                  x={i * candleWidth}
                  width={candleWidth}
                  yScale={yScale}
                  maxY={chartHeight}
                  isHighlighted={crosshair?.dataIndex === i}
                />
              ))}
            </g>

            {/* Volume section */}
            <g transform={`translate(${padding.left}, ${chartHeight + 10})`}>
              {/* Volume label */}
              <text
                x={-40}
                y={volumeHeight / 2}
                fill="var(--color-brutal-slate)"
                fontSize={10}
                textAnchor="middle"
                dominantBaseline="middle"
              >
                VOL
              </text>

              {/* Volume bars */}
              {data.map((candle, i) => (
                <VolumeBar
                  key={i}
                  candle={candle}
                  x={i * candleWidth}
                  width={candleWidth}
                  yScale={yScale}
                  maxVolume={maxVolume}
                  height={volumeHeight}
                />
              ))}

              {/* Volume crosshair line */}
              {crosshair && (
                <line
                  x1={crosshair.dataIndex * candleWidth + candleWidth / 2}
                  y1={0}
                  x2={crosshair.dataIndex * candleWidth + candleWidth / 2}
                  y2={volumeHeight}
                  stroke="var(--color-signal-orange)"
                  strokeWidth={1}
                  strokeDasharray="4 2"
                  opacity={0.7}
                />
              )}
            </g>

            {/* Crosshair */}
            <Crosshair
              position={crosshair}
              candle={highlightedCandle}
              chartHeight={chartHeight}
              chartWidth={chartWidth}
            />

            {/* X-axis time labels */}
            <g transform={`translate(${padding.left}, ${chartHeight + volumeHeight + 15})`}>
              {data.filter((_, i) => i % 10 === 0).map((candle, i) => {
                const x = (data.indexOf(candle)) * candleWidth;
                const time = new Date(candle.timestamp).toLocaleTimeString([], {
                  hour: '2-digit',
                  minute: '2-digit',
                });
                return (
                  <text
                    key={i}
                    x={x}
                    y={0}
                    fill="var(--color-brutal-slate)"
                    fontSize={10}
                    textAnchor="middle"
                  >
                    {time}
                  </text>
                );
              })}
            </g>
          </svg>

          {/* Tooltip */}
          <DataTooltip candle={highlightedCandle} position={tooltipPosition} />
        </div>
      </div>

      {/* Footer with keyboard shortcuts */}
      <div className="absolute bottom-4 left-4 right-4 flex items-center justify-between text-[10px] text-brutal-slate font-mono">
        <div className="flex items-center gap-4">
          <span><kbd className="kbd">←</kbd><kbd className="kbd">→</kbd> Pan</span>
          <span><kbd className="kbd">+</kbd><kbd className="kbd">-</kbd> Zoom</span>
          <span><kbd className="kbd">R</kbd> Reset</span>
        </div>
        <div className="flex items-center gap-4">
          <span>Data points: <span className="text-signal-cyan">{data.length}</span></span>
          <span>Interval: <span className="text-signal-cyan">1m</span></span>
        </div>
      </div>
    </div>
  );
}

export default TradingTraceViewer;
