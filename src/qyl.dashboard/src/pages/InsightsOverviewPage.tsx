import {useState} from 'react';
import {AlertCircle, Clock, Loader2, Sparkles, Timer} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader, CardTitle} from '@/components/ui/card';
import type {InsightTierStatus} from '@/hooks/use-insights';
import {useInsights, useInsightTier} from '@/hooks/use-insights';

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatRelativeTime(iso: string | null): string {
    if (!iso) return 'never';
    const diff = Date.now() - new Date(iso).getTime();
    const seconds = Math.floor(diff / 1_000);
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes} min ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
}

function formatDuration(ms: number): string {
    if (ms < 1_000) return `${ms}ms`;
    return `${(ms / 1_000).toFixed(1)}s`;
}

type TierFreshness = 'fresh' | 'stale' | 'missing';

function getTierFreshness(materializedAt: string | null): TierFreshness {
    if (!materializedAt) return 'missing';
    const age = Date.now() - new Date(materializedAt).getTime();
    return age < 5 * 60 * 1_000 ? 'fresh' : 'stale';
}

const freshnessColors: Record<TierFreshness, string> = {
    fresh: 'bg-green-500',
    stale: 'bg-amber-500',
    missing: 'bg-red-500',
};

function formatTimestamp(iso: string | null): string {
    if (!iso) return '--';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
    });
}

// ── Markdown renderer ─────────────────────────────────────────────────────────

function renderMarkdownLine(line: string, index: number) {
    // Headings
    const h3Match = line.match(/^###\s+(.*)/);
    if (h3Match) {
        return <h3 key={index} className="text-sm font-bold text-brutal-white mt-4 mb-1">{h3Match[1]}</h3>;
    }
    const h2Match = line.match(/^##\s+(.*)/);
    if (h2Match) {
        return <h2 key={index} className="text-base font-bold text-brutal-white mt-5 mb-2">{h2Match[1]}</h2>;
    }
    const h1Match = line.match(/^#\s+(.*)/);
    if (h1Match) {
        return <h1 key={index} className="text-lg font-bold text-brutal-white mt-6 mb-2">{h1Match[1]}</h1>;
    }
    // Bullet points
    const bulletMatch = line.match(/^[-*]\s+(.*)/);
    if (bulletMatch) {
        return (
            <div key={index} className="flex gap-2 text-sm text-brutal-slate pl-2">
                <span className="text-signal-orange select-none">&bull;</span>
                <span>{applyInlineFormatting(bulletMatch[1])}</span>
            </div>
        );
    }
    // Empty line
    if (line.trim() === '') {
        return <div key={index} className="h-2"/>;
    }
    // Plain text
    return <p key={index} className="text-sm text-brutal-slate">{applyInlineFormatting(line)}</p>;
}

function applyInlineFormatting(text: string): React.ReactNode {
    // Bold
    const parts = text.split(/(\*\*[^*]+\*\*)/g);
    if (parts.length <= 1) return text;
    return parts.map((part, i) => {
        const boldMatch = part.match(/^\*\*([^*]+)\*\*$/);
        if (boldMatch) {
            return <strong key={i} className="text-brutal-white font-bold">{boldMatch[1]}</strong>;
        }
        return <span key={i}>{part}</span>;
    });
}

function MarkdownContent({markdown}: { markdown: string }) {
    const lines = markdown.split('\n');
    return (
        <div className="space-y-0.5 font-mono text-sm leading-relaxed">
            {lines.map((line, i) => renderMarkdownLine(line, i))}
        </div>
    );
}

// ── Tier Detail Panel ─────────────────────────────────────────────────────────

function TierDetail({tier}: { tier: string }) {
    const {data, isLoading, error} = useInsightTier(tier);

    if (isLoading) {
        return (
            <div className="flex items-center gap-2 py-4 px-4">
                <Loader2 className="w-4 h-4 text-signal-orange animate-spin"/>
                <span className="text-xs text-brutal-slate tracking-wider">LOADING TIER DATA...</span>
            </div>
        );
    }

    if (error || !data) {
        return (
            <div className="flex items-center gap-2 py-4 px-4">
                <AlertCircle className="w-4 h-4 text-red-500"/>
                <span className="text-xs text-brutal-slate">Failed to load tier data</span>
            </div>
        );
    }

    if (!data.markdown.trim()) {
        return (
            <div className="py-4 px-4 text-xs text-brutal-slate">No insights available for this tier yet.</div>
        );
    }

    return (
        <div className="px-4 pb-4 border-t border-brutal-zinc pt-3">
            <MarkdownContent markdown={data.markdown}/>
        </div>
    );
}

// ── Tier Card ─────────────────────────────────────────────────────────────────

function TierCard({tier, isExpanded, onToggle}: {
    tier: InsightTierStatus;
    isExpanded: boolean;
    onToggle: () => void;
}) {
    const freshness = getTierFreshness(tier.materializedAt);

    return (
        <Card
            className={cn(
                'cursor-pointer transition-colors hover:border-brutal-zinc/70',
                isExpanded && 'border-signal-orange/40',
            )}
            onClick={onToggle}
        >
            <CardContent className="pt-4">
                <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                        <div className={cn('w-2 h-2 rounded-full', freshnessColors[freshness])}/>
                        <span className="text-sm font-bold text-brutal-white tracking-wider uppercase">
                            {tier.tier}
                        </span>
                    </div>
                    <Sparkles className="w-3.5 h-3.5 text-brutal-zinc"/>
                </div>
                <div className="mt-3 space-y-1">
                    <div className="flex items-center gap-1.5 text-xs text-brutal-slate">
                        <Clock className="w-3 h-3"/>
                        <span>{formatRelativeTime(tier.materializedAt)}</span>
                    </div>
                    <div className="flex items-center gap-1.5 text-xs text-brutal-slate">
                        <Timer className="w-3 h-3"/>
                        <span>took {formatDuration(tier.durationMs)}</span>
                    </div>
                </div>
            </CardContent>
            {isExpanded && <TierDetail tier={tier.tier}/>}
        </Card>
    );
}

// ── Loading skeleton ──────────────────────────────────────────────────────────

function TierCardSkeleton() {
    return (
        <Card>
            <CardContent className="pt-4 animate-pulse">
                <div className="flex items-center gap-2">
                    <div className="w-2 h-2 rounded-full bg-brutal-zinc"/>
                    <div className="w-24 h-4 bg-brutal-zinc"/>
                </div>
                <div className="mt-3 space-y-1">
                    <div className="w-20 h-3 bg-brutal-zinc"/>
                    <div className="w-16 h-3 bg-brutal-zinc"/>
                </div>
            </CardContent>
        </Card>
    );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export function InsightsOverviewPage() {
    const {data, isLoading, error} = useInsights();
    const [expandedTier, setExpandedTier] = useState<string | null>(null);

    if (isLoading) {
        return (
            <div className="flex-1 p-6 space-y-6 overflow-auto">
                <div className="space-y-1">
                    <div className="w-48 h-6 bg-brutal-zinc animate-pulse"/>
                    <div className="w-32 h-4 bg-brutal-zinc animate-pulse"/>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                    {Array.from({length: 3}, (_, i) => <TierCardSkeleton key={i}/>)}
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="flex-1 flex items-center justify-center">
                <div className="text-center space-y-2">
                    <AlertCircle className="w-10 h-10 mx-auto text-red-500"/>
                    <p className="text-sm text-brutal-slate">Failed to load insights</p>
                    <p className="text-xs text-brutal-zinc font-mono">{error.message}</p>
                </div>
            </div>
        );
    }

    if (!data) {
        return (
            <div className="flex-1 flex items-center justify-center">
                <div className="text-center space-y-2">
                    <Sparkles className="w-10 h-10 mx-auto text-brutal-zinc"/>
                    <p className="text-sm font-bold text-brutal-slate tracking-wider">NO INSIGHTS AVAILABLE</p>
                    <p className="text-xs text-brutal-zinc">Insights will appear once telemetry is processed.</p>
                </div>
            </div>
        );
    }

    return (
        <div className="flex-1 p-6 space-y-6 overflow-auto">
            {/* Header */}
            <div className="space-y-1">
                <h1 className="text-lg font-bold text-brutal-white tracking-wider uppercase">
                    INSIGHTS OVERVIEW
                </h1>
                {data.lastUpdated && (
                    <p className="text-xs text-brutal-slate tracking-wider">
                        Last updated {formatTimestamp(data.lastUpdated)}
                    </p>
                )}
            </div>

            {/* Tier status cards */}
            {data.tiers.length > 0 ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                    {data.tiers.map((tier) => (
                        <TierCard
                            key={tier.tier}
                            tier={tier}
                            isExpanded={expandedTier === tier.tier}
                            onToggle={() => setExpandedTier(
                                expandedTier === tier.tier ? null : tier.tier,
                            )}
                        />
                    ))}
                </div>
            ) : (
                <Card>
                    <CardContent className="py-8 text-center">
                        <p className="text-xs font-bold text-brutal-slate tracking-widest">
                            NO TIERS DETECTED YET
                        </p>
                    </CardContent>
                </Card>
            )}

            {/* Markdown content area */}
            {data.markdown.trim() && (
                <Card>
                    <CardHeader>
                        <CardTitle className="text-xs font-bold text-brutal-slate tracking-widest uppercase">
                            ANALYSIS
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <MarkdownContent markdown={data.markdown}/>
                    </CardContent>
                </Card>
            )}
        </div>
    );
}
