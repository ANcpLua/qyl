import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {useUpdateLoomSettings} from '@/hooks/use-coding-agents';
import {toast} from 'sonner';
import {Loader2, Terminal} from 'lucide-react';

export function ClaudeCodeIntegrationCta({onEnabled}: { onEnabled?: () => void }) {
    const {mutate, isPending} = useUpdateLoomSettings();

    const handleEnable = () => {
        mutate({default_coding_agent: 'claude_code'}, {
            onSuccess: () => {
                toast.success('Claude Code agent enabled');
                onEnabled?.();
            },
            onError: () => toast.error('Failed to enable Claude Code'),
        });
    };

    return (
        <Card className="border-dashed">
            <CardHeader>
                <div className="flex items-center gap-2">
                    <Terminal className="w-5 h-5"/>
                    <CardTitle className="text-base">Claude Code Agent</CardTitle>
                    <Badge variant="secondary">Experimental</Badge>
                </div>
                <CardDescription>
                    Hand off autofix analysis to Claude Code for autonomous code generation.
                    Claude Code receives the root cause analysis and generates pull requests directly.
                </CardDescription>
            </CardHeader>
            <CardContent>
                <Button onClick={handleEnable} disabled={isPending} size="sm">
                    {isPending && <Loader2 className="w-4 h-4 mr-2 animate-spin"/>}
                    Enable Claude Code
                </Button>
            </CardContent>
        </Card>
    );
}
