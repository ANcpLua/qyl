import {Badge} from '@/components/ui/badge';
import {cn} from '@/lib/utils';

interface FixabilityBadgeProps {
    score?: number;
    automationLevel?: 'auto' | 'assisted' | 'manual' | 'skip';
    className?: string;
}

function resolveLevel(score?: number, automationLevel?: FixabilityBadgeProps['automationLevel']): 'auto' | 'assisted' | 'manual' | 'skip' {
    if (automationLevel) return automationLevel;
    if (score === undefined) return 'skip';
    if (score >= 0.8) return 'auto';
    if (score >= 0.5) return 'assisted';
    if (score >= 0.2) return 'manual';
    return 'skip';
}

const levelStyles: Record<string, string> = {
    auto: 'bg-green-500/20 text-green-400 border-green-500/40',
    assisted: 'bg-amber-500/20 text-amber-400 border-amber-500/40',
    manual: 'bg-brutal-zinc/20 text-brutal-slate border-brutal-zinc/40',
    skip: 'bg-red-500/20 text-red-400 border-red-500/40',
};

const levelLabels: Record<string, string> = {
    auto: 'Auto',
    assisted: 'Assisted',
    manual: 'Manual',
    skip: 'Skip',
};

export function FixabilityBadge({score, automationLevel, className}: FixabilityBadgeProps) {
    const level = resolveLevel(score, automationLevel);
    const percentage = score !== undefined ? `${Math.round(score * 100)}%` : null;

    return (
        <Badge
            variant="outline"
            className={cn(levelStyles[level], className)}
        >
            {percentage && <span className="mr-1">{percentage}</span>}
            {levelLabels[level]}
        </Badge>
    );
}
