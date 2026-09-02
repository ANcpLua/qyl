import {useMemo, useState} from 'react';
import type {MetricAggregation, MetricBucket, MetricDescriptor} from '@ancplua/qyl-api-schema/types';
import {useMetricRange, useMetrics} from '@/hooks/use-telemetry';

const WINDOW_MINUTES = 60;

/** Percentiles read a distribution, so they are offered only where one exists. */
const SCALAR_AGGREGATIONS: MetricAggregation[] = ['avg', 'min', 'max', 'sum', 'last'];
const HISTOGRAM_AGGREGATIONS: MetricAggregation[] = [...SCALAR_AGGREGATIONS, 'p50', 'p95', 'p99'];

function isHistogram(kind: MetricDescriptor['kind']): boolean {
    return kind === 'histogram' || kind === 'exponential_histogram';
}

function bucketValue(bucket: MetricBucket): number | null {
    return typeof bucket.value === 'number' ? bucket.value : null;
}

/** Two significant-ish digits without dragging in a formatting dependency. */
function formatValue(value: number): string {
    const magnitude = Math.abs(value);
    if (magnitude !== 0 && (magnitude < 0.01 || magnitude >= 1_000_000)) return value.toExponential(2);
    return value.toLocaleString(undefined, {maximumFractionDigits: magnitude < 10 ? 2 : 0});
}

function formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, {hour: '2-digit', minute: '2-digit'});
}

export function MetricsPage() {
    const {data: metrics, isPending, error} = useMetrics();
    const [selectedName, setSelectedName] = useState<string>();
    const [aggregation, setAggregation] = useState<MetricAggregation>('avg');

    const selected = metrics?.find(metric => metric.name === selectedName) ?? metrics?.[0];
    const available = selected && isHistogram(selected.kind) ? HISTOGRAM_AGGREGATIONS : SCALAR_AGGREGATIONS;
    // A percentile stays selected only while the chosen metric can answer it.
    const effective = available.includes(aggregation) ? aggregation : 'avg';
    const {data: range, error: rangeError} = useMetricRange(selected?.name, effective, WINDOW_MINUTES);

    return (
        <div className="flex h-full gap-4 p-4">
            <aside className="w-80 shrink-0 overflow-y-auto border border-brutal-zinc bg-brutal-dark">
                <h2 className="border-b border-brutal-zinc px-3 py-2 text-xs tracking-widest text-muted-foreground">
                    METRICS
                </h2>
                {isPending && <p className="px-3 py-2 text-sm text-muted-foreground">Loading…</p>}
                {error && <p className="px-3 py-2 text-sm text-error">{error.message}</p>}
                {metrics?.length === 0 && (
                    <p className="px-3 py-2 text-sm text-muted-foreground">
                        No metrics recorded yet.
                    </p>
                )}
                <ul>
                    {metrics?.map(metric => (
                        <li key={metric.name}>
                            <button
                                type="button"
                                onClick={() => setSelectedName(metric.name)}
                                aria-current={metric.name === selected?.name}
                                className={`w-full border-b border-brutal-zinc px-3 py-2 text-left text-sm ${
                                    metric.name === selected?.name
                                        ? 'bg-brutal-carbon text-foreground'
                                        : 'text-muted-foreground hover:text-foreground'
                                }`}
                            >
                                <span className="block truncate font-mono">{metric.name}</span>
                                <span className="text-xs text-muted-foreground">
                                    {metric.kind}
                                    {metric.unit ? ` · ${metric.unit}` : ''}
                                    {` · ${metric.series_count} series`}
                                </span>
                            </button>
                        </li>
                    ))}
                </ul>
            </aside>

            <section className="flex min-w-0 flex-1 flex-col border border-brutal-zinc bg-brutal-dark">
                {!selected ? (
                    <p className="p-4 text-sm text-muted-foreground">Select a metric.</p>
                ) : (
                    <>
                        <header className="flex flex-wrap items-baseline gap-3 border-b border-brutal-zinc px-4 py-3">
                            <h1 className="font-mono text-sm text-foreground">{selected.name}</h1>
                            <span className="text-xs text-muted-foreground">
                                last {WINDOW_MINUTES} minutes
                            </span>
                            <div className="ml-auto flex gap-1">
                                {available.map(option => (
                                    <button
                                        key={option}
                                        type="button"
                                        onClick={() => setAggregation(option)}
                                        aria-pressed={option === effective}
                                        className={`border px-2 py-1 text-xs ${
                                            option === effective
                                                ? 'border-primary text-primary'
                                                : 'border-brutal-zinc text-muted-foreground hover:text-foreground'
                                        }`}
                                    >
                                        {option}
                                    </button>
                                ))}
                            </div>
                        </header>
                        {rangeError ? (
                            <p className="p-4 text-sm text-error">{rangeError.message}</p>
                        ) : (
                            <RangeChart
                                buckets={range?.series[0]?.buckets ?? []}
                                unit={range?.unit ?? selected.unit}
                            />
                        )}
                    </>
                )}
            </section>
        </div>
    );
}

/**
 * One series, so identity needs no legend — the header names it. The collector
 * already reduced the window into these buckets; this only draws them.
 */
function RangeChart({buckets, unit}: {buckets: MetricBucket[]; unit?: string}) {
    const [hovered, setHovered] = useState<number>();

    const plotted = useMemo(
        () => buckets
            .map((bucket, index) => ({index, bucket, value: bucketValue(bucket)}))
            .filter((point): point is {index: number; bucket: MetricBucket; value: number} =>
                point.value !== null),
        [buckets],
    );

    if (plotted.length === 0) {
        return <p className="p-4 text-sm text-muted-foreground">No points in this window.</p>;
    }

    const width = 720;
    const height = 240;
    const padding = {top: 16, right: 16, bottom: 28, left: 56};
    const values = plotted.map(point => point.value);
    const min = Math.min(...values, 0);
    const max = Math.max(...values);
    // A flat series would otherwise divide by zero and collapse onto the axis.
    const span = max - min || Math.abs(max) || 1;

    const x = (index: number) => plotted.length === 1
        ? padding.left + (width - padding.left - padding.right) / 2
        : padding.left + (index / (plotted.length - 1)) * (width - padding.left - padding.right);
    const y = (value: number) =>
        height - padding.bottom - ((value - min) / span) * (height - padding.top - padding.bottom);

    const path = plotted
        .map((point, index) => `${index === 0 ? 'M' : 'L'}${x(index).toFixed(2)},${y(point.value).toFixed(2)}`)
        .join(' ');
    const activeIndex = hovered !== undefined && hovered < plotted.length ? hovered : undefined;
    const active = activeIndex !== undefined ? plotted[activeIndex] : undefined;

    return (
        <figure className="m-0 flex min-h-0 flex-1 flex-col p-4">
            <svg
                viewBox={`0 0 ${width} ${height}`}
                className="h-full w-full"
                role="img"
                aria-label={`Range chart, ${plotted.length} buckets`}
                onMouseLeave={() => setHovered(undefined)}
            >
                <line
                    x1={padding.left} x2={width - padding.right}
                    y1={height - padding.bottom} y2={height - padding.bottom}
                    stroke="var(--color-border)" strokeWidth={1}
                />
                {[min, min + span / 2, min + span].map(tick => (
                    <g key={tick}>
                        <line
                            x1={padding.left} x2={width - padding.right}
                            y1={y(tick)} y2={y(tick)}
                            stroke="var(--color-border)" strokeWidth={1} opacity={0.35}
                        />
                        <text
                            x={padding.left - 8} y={y(tick) + 4}
                            textAnchor="end" fontSize={11} fill="var(--color-muted-foreground)"
                        >
                            {formatValue(tick)}
                        </text>
                    </g>
                ))}

                <path d={path} fill="none" stroke="var(--color-chart-1)" strokeWidth={2}
                      strokeLinejoin="round" strokeLinecap="round"/>

                {active && activeIndex !== undefined && (
                    <>
                        <line
                            x1={x(activeIndex)} x2={x(activeIndex)}
                            y1={padding.top} y2={height - padding.bottom}
                            stroke="var(--color-muted-foreground)" strokeWidth={1}
                        />
                        {/* A surface ring keeps the marker legible where it crosses the line. */}
                        <circle
                            cx={x(activeIndex)} cy={y(active.value)} r={4}
                            fill="var(--color-chart-1)"
                            stroke="var(--color-card)" strokeWidth={2}
                        />
                    </>
                )}

                {/* Hit targets are full-height columns so the pointer never has to find a 2px line. */}
                {plotted.map((point, index) => (
                    <rect
                        key={point.bucket.bucket_start}
                        x={x(index) - (width - padding.left - padding.right) / (2 * Math.max(1, plotted.length - 1))}
                        y={padding.top}
                        width={Math.max(6, (width - padding.left - padding.right) / Math.max(1, plotted.length - 1))}
                        height={height - padding.top - padding.bottom}
                        fill="transparent"
                        onMouseEnter={() => setHovered(index)}
                    />
                ))}

                <text
                    x={padding.left} y={height - 8}
                    fontSize={11} fill="var(--color-muted-foreground)"
                >
                    {formatTime(plotted[0].bucket.bucket_start)}
                </text>
                <text
                    x={width - padding.right} y={height - 8} textAnchor="end"
                    fontSize={11} fill="var(--color-muted-foreground)"
                >
                    {formatTime(plotted[plotted.length - 1].bucket.bucket_start)}
                </text>
            </svg>
            <figcaption className="pt-2 text-xs text-muted-foreground" aria-live="polite">
                {active
                    ? `${formatTime(active.bucket.bucket_start)} · ${formatValue(active.value)}${unit ? ` ${unit}` : ''} · ${active.bucket.point_count} points`
                    : `${plotted.length} buckets${unit ? ` · ${unit}` : ''}`}
            </figcaption>
        </figure>
    );
}
