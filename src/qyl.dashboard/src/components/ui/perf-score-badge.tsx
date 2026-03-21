import {cn} from '@/lib/utils';

type ScoreLevel = 'good' | 'meh' | 'poor' | 'unknown';

interface PerfScoreBadgeProps {
    score: number | null | undefined;
    className?: string;
    showLabel?: boolean;
}

function resolveLevel(score: number | null | undefined): ScoreLevel {
    if (score == null) return 'unknown';
    if (score >= 80) return 'good';
    if (score >= 50) return 'meh';
    return 'poor';
}

const levelStyles: Record<ScoreLevel, string> = {
    good: 'text-green-400',
    meh: 'text-amber-400',
    poor: 'text-red-400',
    unknown: 'text-brutal-slate',
};

const levelLabels: Record<ScoreLevel, string> = {
    good: 'Good',
    meh: 'Meh',
    poor: 'Poor',
    unknown: '—',
};

export function PerfScoreBadge({score, className, showLabel = true}: PerfScoreBadgeProps) {
    const level = resolveLevel(score);
    const displayScore = score != null ? Math.round(score) : null;

    return (
        <span
            className={cn('inline-flex items-center gap-1 font-mono text-xs font-bold', levelStyles[level], className)}>
            {showLabel && <span>{levelLabels[level]}</span>}
            {displayScore != null && <span>{displayScore}</span>}
        </span>
    );
}
