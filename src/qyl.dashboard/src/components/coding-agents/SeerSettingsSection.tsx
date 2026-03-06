import {useState} from 'react';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {Separator} from '@/components/ui/separator';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import {CODING_AGENT_PROVIDERS, useLoomSettings, useUpdateLoomSettings} from '@/hooks/use-coding-agents';
import {ClaudeCodeIntegrationCta} from './ClaudeCodeIntegrationCta';
import {toast} from 'sonner';
import {Loader2, Trash2} from 'lucide-react';

const AUTOMATION_LEVELS = [
    {value: 'off', label: 'Off', description: 'No automatic actions'},
    {value: 'low', label: 'Low', description: 'Summarize only'},
    {value: 'medium', label: 'Medium', description: 'Summarize + suggest fixes'},
    {value: 'high', label: 'High', description: 'Auto-fix high-confidence issues'},
] as const;

export function LoomSettingsSection() {
    const {data: settings, isLoading} = useLoomSettings();
    const {mutate, isPending} = useUpdateLoomSettings();

    const [agent, setAgent] = useState(settings?.default_coding_agent ?? 'Loom');
    const [tuning, setTuning] = useState(settings?.automation_tuning ?? 'medium');

    // Sync state when settings load
    if (settings && agent === 'Loom' && settings.default_coding_agent !== 'Loom') {
        setAgent(settings.default_coding_agent);
        setTuning(settings.automation_tuning);
    }

    const handleSave = () => {
        mutate({default_coding_agent: agent, automation_tuning: tuning}, {
            onSuccess: () => toast.success('Loom settings saved'),
            onError: () => toast.error('Failed to save settings'),
        });
    };

    const handleReset = () => {
        mutate({default_coding_agent: 'Loom', automation_tuning: 'medium'}, {
            onSuccess: () => {
                setAgent('Loom');
                setTuning('medium');
                toast.success('Loom settings reset');
            },
        });
    };

    if (isLoading) {
        return (
            <Card>
                <CardContent className="py-8 flex justify-center">
                    <Loader2 className="w-5 h-5 animate-spin text-muted-foreground"/>
                </CardContent>
            </Card>
        );
    }

    return (
        <div className="space-y-4">
            <Card>
                <CardHeader>
                    <CardTitle className="text-base">Loom Automation</CardTitle>
                    <CardDescription>
                        Configure how qyl's AI debugging agent handles issues automatically.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Default Coding Agent</label>
                        <Select value={agent} onValueChange={setAgent}>
                            <SelectTrigger aria-label="Default coding agent">
                                <SelectValue/>
                            </SelectTrigger>
                            <SelectContent>
                                {CODING_AGENT_PROVIDERS.map(p => (
                                    <SelectItem key={p.value} value={p.value}>
                                        <span>{p.label}</span>
                                        <span className="ml-2 text-xs text-muted-foreground">{p.description}</span>
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>

                    <div className="space-y-2">
                        <label className="text-sm font-medium">Automation Tuning</label>
                        <Select value={tuning} onValueChange={setTuning}>
                            <SelectTrigger aria-label="Automation tuning level">
                                <SelectValue/>
                            </SelectTrigger>
                            <SelectContent>
                                {AUTOMATION_LEVELS.map(l => (
                                    <SelectItem key={l.value} value={l.value}>
                                        <span>{l.label}</span>
                                        <span className="ml-2 text-xs text-muted-foreground">{l.description}</span>
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>

                    <Separator/>

                    <div className="flex gap-2">
                        <Button onClick={handleSave} disabled={isPending} size="sm">
                            {isPending && <Loader2 className="w-4 h-4 mr-2 animate-spin"/>}
                            Save
                        </Button>
                        {settings?.default_coding_agent !== 'Loom' && (
                            <Button variant="outline" size="sm" onClick={handleReset}>
                                <Trash2 className="w-4 h-4 mr-2"/>
                                Reset
                            </Button>
                        )}
                    </div>

                    {settings && (
                        <div className="flex items-center gap-2 text-sm">
                            <Badge variant="secondary">{settings.default_coding_agent}</Badge>
                            <span className="text-muted-foreground">/ {settings.automation_tuning}</span>
                        </div>
                    )}
                </CardContent>
            </Card>

            {agent === 'claude_code' && (
                <ClaudeCodeIntegrationCta onEnabled={() => toast.success('Claude Code is now the default agent')}/>
            )}
        </div>
    );
}
