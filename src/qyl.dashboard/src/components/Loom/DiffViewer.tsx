import {Card} from '@/components/ui/card';
import {ScrollArea} from '@/components/ui/scroll-area';
import {cn} from '@/lib/utils';

interface DiffViewerProps {
    diff: string;
    className?: string;
}

function classifyLine(line: string): string {
    if (line.startsWith('@@')) return 'bg-signal-cyan/10 text-signal-cyan';
    if (line.startsWith('+')) return 'bg-signal-green/10 text-signal-green';
    if (line.startsWith('-')) return 'bg-signal-red/10 text-signal-red';
    return 'text-brutal-slate';
}

export function DiffViewer({diff, className}: DiffViewerProps) {
    const lines = diff.split('\n');

    return (
        <Card className={cn('font-mono text-xs', className)}>
            <ScrollArea className="max-h-96">
                <pre className="p-4">
                    <code>
                        {lines.map((line, index) => (
                            <div
                                key={index}
                                className={cn('px-2 leading-6', classifyLine(line))}
                            >
                                {line || '\u00A0'}
                            </div>
                        ))}
                    </code>
                </pre>
            </ScrollArea>
        </Card>
    );
}
