/**
 * GODLIKE Example 3: Command Center Grid
 *
 * Inspired by glances - the "single pane of glass" philosophy
 * Combined with Architech's modern dark UI aesthetic
 *
 * Features:
 * - Single viewport - NO SCROLLING
 * - Dense CSS Grid layout that adapts to fit
 * - Real-time service status table
 * - Animated text effects for dynamic data
 * - Keyboard navigation (j/k, gg, G, /)
 * - Selection highlight with details panel
 *
 * The key insight: Information density + keyboard-first = power user experience
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { cn } from '@/lib/utils';
import {
  Activity,
  AlertTriangle,
  CheckCircle,
  Clock,
  Cpu,
  Database,
  Globe,
  HardDrive,
  MemoryStick,
  Network,
  Server,
  XCircle,
  Zap,
} from 'lucide-react';

// ============================================
// TYPES
// ============================================

type ServiceStatus = 'healthy' | 'degraded' | 'down' | 'unknown';
type SpanKind = 'server' | 'client' | 'internal' | 'producer' | 'consumer';

interface Service {
  id: string;
  name: string;
  status: ServiceStatus;
  latencyP50: number;
  latencyP99: number;
  requestsPerSec: number;
  errorRate: number;
  lastSeen: number;
  spans: number;
  kind: SpanKind;
}

interface SystemMetric {
  label: string;
  value: number;
  max: number;
  unit: string;
  icon: typeof Cpu;
  color: string;
}

// ============================================
// ANIMATED NUMBER - Smooth value transitions
// ============================================

interface AnimatedNumberProps {
  value: number;
  decimals?: number;
  suffix?: string;
  className?: string;
}

function AnimatedNumber({ value, decimals = 0, suffix = '', className }: AnimatedNumberProps) {
  const [displayValue, setDisplayValue] = useState(value);
  const [isChanging, setIsChanging] = useState(false);

  useEffect(() => {
    if (Math.abs(value - displayValue) > 0.1) {
      setIsChanging(true);
      const timer = setTimeout(() => {
        setDisplayValue(value);
        setIsChanging(false);
      }, 150);
      return () => clearTimeout(timer);
    }
  }, [value, displayValue]);

  return (
    <span className={cn(
      'transition-all duration-150 font-mono',
      isChanging && 'text-signal-yellow scale-110',
      className
    )}>
      {displayValue.toFixed(decimals)}{suffix}
    </span>
  );
}

// ============================================
// DECRYPTED TEXT - Cyberpunk scramble effect
// ============================================

interface DecryptedTextProps {
  text: string;
  className?: string;
  scrambleDuration?: number;
}

function DecryptedText({ text, className, scrambleDuration = 500 }: DecryptedTextProps) {
  const [displayText, setDisplayText] = useState(text);
  const [isScrambling, setIsScrambling] = useState(false);
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#$%&*';

  useEffect(() => {
    if (text !== displayText && !isScrambling) {
      setIsScrambling(true);
      let iterations = 0;
      const maxIterations = 10;

      const interval = setInterval(() => {
        setDisplayText(
          text
            .split('')
            .map((char, index) => {
              if (index < iterations) return text[index];
              if (char === ' ') return ' ';
              return chars[Math.floor(Math.random() * chars.length)];
            })
            .join('')
        );

        iterations += text.length / maxIterations;

        if (iterations >= text.length) {
          clearInterval(interval);
          setDisplayText(text);
          setIsScrambling(false);
        }
      }, scrambleDuration / maxIterations);

      return () => clearInterval(interval);
    }
  }, [text, displayText, isScrambling, scrambleDuration]);

  return <span className={className}>{displayText}</span>;
}

// ============================================
// STATUS INDICATOR
// ============================================

interface StatusIndicatorProps {
  status: ServiceStatus;
  pulse?: boolean;
  size?: 'sm' | 'md';
}

function StatusIndicator({ status, pulse = true, size = 'sm' }: StatusIndicatorProps) {
  const colors: Record<ServiceStatus, string> = {
    healthy: 'bg-signal-green',
    degraded: 'bg-signal-yellow',
    down: 'bg-signal-red',
    unknown: 'bg-brutal-slate',
  };

  const sizes = {
    sm: 'w-2 h-2',
    md: 'w-3 h-3',
  };

  return (
    <div className={cn(
      sizes[size],
      colors[status],
      pulse && status !== 'unknown' && 'animate-pulse-live'
    )} />
  );
}

// ============================================
// METRIC CARD - Compact system metrics
// ============================================

interface MetricCardProps {
  metric: SystemMetric;
}

function MetricCard({ metric }: MetricCardProps) {
  const percent = (metric.value / metric.max) * 100;
  const Icon = metric.icon;

  return (
    <div className="bg-brutal-carbon border border-brutal-zinc p-3 flex flex-col">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          <Icon className={cn('w-4 h-4', metric.color)} />
          <span className="text-[10px] uppercase tracking-wider text-brutal-slate">
            {metric.label}
          </span>
        </div>
        <span className={cn('text-sm font-bold font-mono', metric.color)}>
          <AnimatedNumber value={metric.value} decimals={1} suffix={metric.unit} />
        </span>
      </div>
      {/* Mini progress bar */}
      <div className="h-1 bg-brutal-black w-full">
        <div
          className={cn('h-full transition-all duration-300', metric.color.replace('text-', 'bg-'))}
          style={{ width: `${Math.min(100, percent)}%` }}
        />
      </div>
    </div>
  );
}

// ============================================
// SERVICE ROW - Table row for services
// ============================================

interface ServiceRowProps {
  service: Service;
  isSelected: boolean;
  onSelect: () => void;
}

function ServiceRow({ service, isSelected, onSelect }: ServiceRowProps) {
  const latencyStatus = service.latencyP99 > 500 ? 'error' : service.latencyP99 > 200 ? 'warn' : 'ok';
  const errorStatus = service.errorRate > 5 ? 'error' : service.errorRate > 1 ? 'warn' : 'ok';

  const statusColors = {
    ok: 'text-signal-green',
    warn: 'text-signal-yellow',
    error: 'text-signal-red',
  };

  const kindIcons: Record<SpanKind, typeof Server> = {
    server: Server,
    client: Globe,
    internal: Cpu,
    producer: Zap,
    consumer: Database,
  };
  const KindIcon = kindIcons[service.kind];

  return (
    <tr
      className={cn(
        'border-b border-brutal-zinc hover:bg-brutal-dark cursor-pointer transition-colors',
        isSelected && 'bg-signal-orange/10 border-l-2 border-l-signal-orange'
      )}
      onClick={onSelect}
    >
      <td className="py-2 px-3">
        <StatusIndicator status={service.status} />
      </td>
      <td className="py-2 px-3">
        <div className="flex items-center gap-2">
          <KindIcon className="w-3 h-3 text-brutal-slate" />
          <DecryptedText
            text={service.name}
            className="font-mono text-sm text-brutal-white"
          />
        </div>
      </td>
      <td className="py-2 px-3 text-right">
        <span className={cn('font-mono text-xs', statusColors[latencyStatus])}>
          <AnimatedNumber value={service.latencyP50} suffix="ms" />
        </span>
      </td>
      <td className="py-2 px-3 text-right">
        <span className={cn('font-mono text-xs', statusColors[latencyStatus])}>
          <AnimatedNumber value={service.latencyP99} suffix="ms" />
        </span>
      </td>
      <td className="py-2 px-3 text-right">
        <span className="font-mono text-xs text-signal-cyan">
          <AnimatedNumber value={service.requestsPerSec} decimals={1} suffix="/s" />
        </span>
      </td>
      <td className="py-2 px-3 text-right">
        <span className={cn('font-mono text-xs', statusColors[errorStatus])}>
          <AnimatedNumber value={service.errorRate} decimals={2} suffix="%" />
        </span>
      </td>
      <td className="py-2 px-3 text-right">
        <span className="font-mono text-xs text-brutal-slate">
          {service.spans.toLocaleString()}
        </span>
      </td>
    </tr>
  );
}

// ============================================
// SERVICE DETAILS PANEL
// ============================================

interface ServiceDetailsPanelProps {
  service: Service | null;
}

function ServiceDetailsPanel({ service }: ServiceDetailsPanelProps) {
  if (!service) {
    return (
      <div className="h-full flex items-center justify-center text-brutal-slate text-sm">
        Select a service to view details
      </div>
    );
  }

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center gap-3">
        <StatusIndicator status={service.status} size="md" />
        <div>
          <h3 className="text-lg font-bold text-brutal-white font-mono">
            {service.name}
          </h3>
          <span className="text-xs text-brutal-slate uppercase tracking-wider">
            {service.kind} service
          </span>
        </div>
      </div>

      <div className="border-t border-brutal-zinc pt-4">
        <h4 className="text-[10px] uppercase tracking-wider text-brutal-slate mb-2">
          Performance
        </h4>
        <div className="grid grid-cols-2 gap-3 text-sm font-mono">
          <div>
            <span className="text-brutal-slate text-xs">P50 Latency</span>
            <div className="text-signal-green font-bold">{service.latencyP50.toFixed(0)}ms</div>
          </div>
          <div>
            <span className="text-brutal-slate text-xs">P99 Latency</span>
            <div className="text-signal-yellow font-bold">{service.latencyP99.toFixed(0)}ms</div>
          </div>
          <div>
            <span className="text-brutal-slate text-xs">Throughput</span>
            <div className="text-signal-cyan font-bold">{service.requestsPerSec.toFixed(1)}/s</div>
          </div>
          <div>
            <span className="text-brutal-slate text-xs">Error Rate</span>
            <div className={cn(
              'font-bold',
              service.errorRate > 5 ? 'text-signal-red' : 'text-signal-green'
            )}>
              {service.errorRate.toFixed(2)}%
            </div>
          </div>
        </div>
      </div>

      <div className="border-t border-brutal-zinc pt-4">
        <h4 className="text-[10px] uppercase tracking-wider text-brutal-slate mb-2">
          Actions
        </h4>
        <div className="flex flex-wrap gap-2">
          <button className="kbd text-xs hover:bg-signal-orange hover:text-brutal-black transition-colors">
            View Traces
          </button>
          <button className="kbd text-xs hover:bg-signal-orange hover:text-brutal-black transition-colors">
            View Logs
          </button>
          <button className="kbd text-xs hover:bg-signal-orange hover:text-brutal-black transition-colors">
            Metrics
          </button>
        </div>
      </div>
    </div>
  );
}

// ============================================
// MOCK DATA
// ============================================

function generateServices(): Service[] {
  const names = [
    'api-gateway',
    'auth-service',
    'user-service',
    'order-service',
    'payment-service',
    'notification-service',
    'genai-proxy',
    'cache-layer',
    'search-service',
    'analytics-worker',
  ];

  const kinds: SpanKind[] = ['server', 'client', 'internal', 'producer', 'consumer'];

  return names.map((name, i) => ({
    id: `svc-${i}`,
    name,
    status: Math.random() > 0.1 ? 'healthy' : Math.random() > 0.5 ? 'degraded' : 'down',
    latencyP50: 20 + Math.random() * 80,
    latencyP99: 100 + Math.random() * 400,
    requestsPerSec: 50 + Math.random() * 500,
    errorRate: Math.random() * 5,
    lastSeen: Date.now() - Math.random() * 60000,
    spans: Math.floor(1000 + Math.random() * 10000),
    kind: kinds[i % kinds.length],
  }));
}

// ============================================
// MAIN COMPONENT: Command Center
// ============================================

export function CommandCenter() {
  const [services, setServices] = useState<Service[]>(() => generateServices());
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [searchQuery, setSearchQuery] = useState('');
  const [isSearching, setIsSearching] = useState(false);
  const searchInputRef = useRef<HTMLInputElement>(null);

  const [systemMetrics, setSystemMetrics] = useState<SystemMetric[]>([
    { label: 'CPU', value: 42, max: 100, unit: '%', icon: Cpu, color: 'text-signal-cyan' },
    { label: 'Memory', value: 12.4, max: 32, unit: 'GB', icon: MemoryStick, color: 'text-signal-violet' },
    { label: 'Network', value: 145, max: 1000, unit: 'MB/s', icon: Network, color: 'text-signal-green' },
    { label: 'Disk', value: 67, max: 100, unit: '%', icon: HardDrive, color: 'text-signal-yellow' },
  ]);

  // Filter services
  const filteredServices = useMemo(() => {
    if (!searchQuery) return services;
    return services.filter(s =>
      s.name.toLowerCase().includes(searchQuery.toLowerCase())
    );
  }, [services, searchQuery]);

  const selectedService = filteredServices[selectedIndex] || null;

  // Simulate real-time updates
  useEffect(() => {
    const interval = setInterval(() => {
      setServices(prev => prev.map(service => ({
        ...service,
        latencyP50: Math.max(10, service.latencyP50 + (Math.random() - 0.5) * 10),
        latencyP99: Math.max(50, service.latencyP99 + (Math.random() - 0.5) * 30),
        requestsPerSec: Math.max(10, service.requestsPerSec + (Math.random() - 0.5) * 20),
        errorRate: Math.max(0, Math.min(10, service.errorRate + (Math.random() - 0.5) * 0.5)),
        spans: service.spans + Math.floor(Math.random() * 10),
        status: Math.random() > 0.98
          ? (Math.random() > 0.5 ? 'degraded' : 'down')
          : service.status === 'down' && Math.random() > 0.8
            ? 'healthy'
            : service.status,
      })));

      setSystemMetrics(prev => prev.map(m => ({
        ...m,
        value: Math.max(0, Math.min(m.max, m.value + (Math.random() - 0.5) * (m.max * 0.05))),
      })));
    }, 2000);

    return () => clearInterval(interval);
  }, []);

  // Keyboard navigation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (isSearching) {
        if (e.key === 'Escape') {
          setIsSearching(false);
          setSearchQuery('');
        }
        return;
      }

      switch (e.key) {
        case 'j':
        case 'ArrowDown':
          e.preventDefault();
          setSelectedIndex(prev => Math.min(prev + 1, filteredServices.length - 1));
          break;
        case 'k':
        case 'ArrowUp':
          e.preventDefault();
          setSelectedIndex(prev => Math.max(prev - 1, 0));
          break;
        case 'g':
          if (e.shiftKey) {
            setSelectedIndex(filteredServices.length - 1);
          } else {
            setSelectedIndex(0);
          }
          break;
        case '/':
          e.preventDefault();
          setIsSearching(true);
          setTimeout(() => searchInputRef.current?.focus(), 0);
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [filteredServices.length, isSearching]);

  // Calculate summary stats
  const summary = useMemo(() => ({
    total: services.length,
    healthy: services.filter(s => s.status === 'healthy').length,
    degraded: services.filter(s => s.status === 'degraded').length,
    down: services.filter(s => s.status === 'down').length,
    totalSpans: services.reduce((acc, s) => acc + s.spans, 0),
    avgLatency: services.reduce((acc, s) => acc + s.latencyP50, 0) / services.length,
  }), [services]);

  return (
    <div className="h-full bg-brutal-black font-mono overflow-hidden flex flex-col">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b-2 border-brutal-zinc bg-brutal-carbon">
        <div className="flex items-center gap-4">
          <h1 className="text-lg font-bold tracking-wider uppercase text-signal-orange">
            qyl://command-center
          </h1>
          <div className="flex items-center gap-1">
            <div className="w-2 h-2 bg-signal-green animate-pulse-live" />
            <span className="text-[10px] uppercase tracking-[0.2em] text-signal-green">
              live
            </span>
          </div>
        </div>

        {/* Summary badges */}
        <div className="flex items-center gap-4 text-xs">
          <div className="flex items-center gap-2">
            <CheckCircle className="w-4 h-4 text-signal-green" />
            <span className="text-signal-green">{summary.healthy}</span>
          </div>
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-signal-yellow" />
            <span className="text-signal-yellow">{summary.degraded}</span>
          </div>
          <div className="flex items-center gap-2">
            <XCircle className="w-4 h-4 text-signal-red" />
            <span className="text-signal-red">{summary.down}</span>
          </div>
          <span className="text-brutal-zinc">│</span>
          <div className="flex items-center gap-2">
            <Activity className="w-4 h-4 text-signal-cyan" />
            <span className="text-signal-cyan">{summary.totalSpans.toLocaleString()} spans</span>
          </div>
        </div>
      </div>

      {/* Main Grid - Single Pane of Glass */}
      <div className="flex-1 grid grid-cols-[1fr_300px] overflow-hidden">
        {/* Left: Services Table + Metrics */}
        <div className="flex flex-col border-r border-brutal-zinc overflow-hidden">
          {/* System Metrics Row */}
          <div className="grid grid-cols-4 gap-2 p-3 border-b border-brutal-zinc">
            {systemMetrics.map((metric, i) => (
              <MetricCard key={i} metric={metric} />
            ))}
          </div>

          {/* Search Bar */}
          {isSearching && (
            <div className="px-3 py-2 border-b border-brutal-zinc bg-brutal-carbon">
              <div className="flex items-center gap-2">
                <span className="text-signal-orange">/</span>
                <input
                  ref={searchInputRef}
                  type="text"
                  value={searchQuery}
                  onChange={e => setSearchQuery(e.target.value)}
                  className="flex-1 bg-transparent border-none outline-none text-sm text-brutal-white"
                  placeholder="Search services..."
                  onBlur={() => !searchQuery && setIsSearching(false)}
                />
                <span className="text-[10px] text-brutal-slate">ESC to close</span>
              </div>
            </div>
          )}

          {/* Services Table */}
          <div className="flex-1 overflow-auto">
            <table className="w-full">
              <thead className="sticky top-0 bg-brutal-carbon z-10">
                <tr className="border-b-2 border-brutal-zinc text-[10px] uppercase tracking-wider text-brutal-slate">
                  <th className="py-2 px-3 text-left w-8"></th>
                  <th className="py-2 px-3 text-left">Service</th>
                  <th className="py-2 px-3 text-right">P50</th>
                  <th className="py-2 px-3 text-right">P99</th>
                  <th className="py-2 px-3 text-right">RPS</th>
                  <th className="py-2 px-3 text-right">Err%</th>
                  <th className="py-2 px-3 text-right">Spans</th>
                </tr>
              </thead>
              <tbody>
                {filteredServices.map((service, i) => (
                  <ServiceRow
                    key={service.id}
                    service={service}
                    isSelected={i === selectedIndex}
                    onSelect={() => setSelectedIndex(i)}
                  />
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Right: Details Panel */}
        <div className="bg-brutal-carbon overflow-auto">
          <div className="border-b border-brutal-zinc px-4 py-2">
            <span className="text-[10px] uppercase tracking-wider text-brutal-slate">
              Service Details
            </span>
          </div>
          <ServiceDetailsPanel service={selectedService} />
        </div>
      </div>

      {/* Footer Status Bar */}
      <div className="h-6 bg-brutal-carbon border-t border-brutal-zinc flex items-center px-4 text-[10px] text-brutal-slate">
        <span className="flex items-center gap-2">
          <kbd className="kbd">j</kbd><kbd className="kbd">k</kbd> navigate
        </span>
        <span className="mx-3">│</span>
        <span className="flex items-center gap-2">
          <kbd className="kbd">/</kbd> search
        </span>
        <span className="mx-3">│</span>
        <span className="flex items-center gap-2">
          <kbd className="kbd">g</kbd><kbd className="kbd">G</kbd> top/bottom
        </span>
        <span className="flex-1" />
        <span>
          <Clock className="w-3 h-3 inline mr-1" />
          Last update: <span className="text-signal-cyan">just now</span>
        </span>
      </div>
    </div>
  );
}

export default CommandCenter;
