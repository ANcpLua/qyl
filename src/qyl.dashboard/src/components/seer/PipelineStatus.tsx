import {cn} from '@/lib/utils';
import {Check, Circle, Loader2, X} from 'lucide-react';

interface PipelineStep {
    name: string;
    status: 'pending' | 'running' | 'completed' | 'failed';
}

interface PipelineStatusProps {
    steps: PipelineStep[];
    className?: string;
}

const stepIcon: Record<PipelineStep['status'], React.ReactNode> = {
    completed: <Check className="h-3.5 w-3.5"/>,
    running: <Loader2 className="h-3.5 w-3.5 animate-spin"/>,
    failed: <X className="h-3.5 w-3.5"/>,
    pending: <Circle className="h-3.5 w-3.5"/>,
};

const circleStyles: Record<PipelineStep['status'], string> = {
    completed: 'border-green-500 bg-green-500/20 text-green-400',
    running: 'border-amber-500 bg-amber-500/20 text-amber-400',
    failed: 'border-red-500 bg-red-500/20 text-red-400',
    pending: 'border-brutal-zinc bg-brutal-zinc/20 text-brutal-slate',
};

const connectorStyles: Record<PipelineStep['status'], string> = {
    completed: 'bg-green-500/60',
    running: 'bg-amber-500/40',
    failed: 'bg-red-500/40',
    pending: 'bg-brutal-zinc/40',
};

export function PipelineStatus({steps, className}: PipelineStatusProps) {
    return (
        <div className={cn('flex items-start', className)}>
            {steps.map((step, index) => (
                <div key={step.name} className="flex items-start">
                    <div className="flex flex-col items-center gap-1.5">
                        <div
                            className={cn(
                                'flex h-7 w-7 items-center justify-center border-2',
                                circleStyles[step.status],
                            )}
                        >
                            {stepIcon[step.status]}
                        </div>
                        <span className="text-[10px] uppercase tracking-wider text-brutal-slate max-w-16 text-center leading-tight">
                            {step.name}
                        </span>
                    </div>
                    {index < steps.length - 1 && (
                        <div
                            className={cn(
                                'mt-3.5 h-px w-8 shrink-0',
                                connectorStyles[step.status],
                            )}
                        />
                    )}
                </div>
            ))}
        </div>
    );
}
