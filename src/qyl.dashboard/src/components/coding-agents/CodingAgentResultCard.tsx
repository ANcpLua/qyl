import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {Card, CardContent} from '@/components/ui/card';
import {type CodingAgentRun, getProviderButtonText, getProviderLabel} from '@/hooks/use-coding-agents';
import {ExternalLink, GitPullRequest} from 'lucide-react';

const statusVariant: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
    pending: 'outline',
    running: 'secondary',
    completed: 'default',
    failed: 'destructive',
};

export function CodingAgentResultCard({run}: { run: CodingAgentRun }) {
    const actionUrl = run.pr_url ?? run.agent_url;
    const buttonText = run.pr_url ? 'Open PR' : getProviderButtonText(run.provider);

    return (
        <Card>
            <CardContent className="py-3">
                <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                        <Badge variant={statusVariant[run.status] ?? 'outline'}>
                            {run.status}
                        </Badge>
                        <Badge variant="secondary">{getProviderLabel(run.provider)}</Badge>
                        {run.repo_full_name && (
                            <span className="text-sm text-muted-foreground">{run.repo_full_name}</span>
                        )}
                    </div>
                    <div className="flex items-center gap-2">
                        {run.pr_url && (
                            <Button variant="outline" size="sm" asChild>
                                <a href={run.pr_url} target="_blank" rel="noopener noreferrer">
                                    <GitPullRequest className="w-4 h-4 mr-1"/>
                                    Open PR
                                </a>
                            </Button>
                        )}
                        {actionUrl && !run.pr_url && (
                            <Button variant="outline" size="sm" asChild>
                                <a href={actionUrl} target="_blank" rel="noopener noreferrer">
                                    <ExternalLink className="w-4 h-4 mr-1"/>
                                    {buttonText}
                                </a>
                            </Button>
                        )}
                    </div>
                </div>
            </CardContent>
        </Card>
    );
}
