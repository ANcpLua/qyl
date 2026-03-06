import {useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {useQuery} from '@tanstack/react-query';
import {Brain, Database, GearSix, Keyboard, LinkBreak, Monitor, Moon, PaintBrush, Robot, SpinnerGap, Sun, Terminal, Trash,} from '@phosphor-icons/react';

function GitHubIcon({className}: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"
             strokeLinejoin="round" className={className}>
            <path
                d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.08-1.25-.27-2.48-1-3.5.28-1.15.28-2.35 0-3.5 0 0-1 0-3 1.5-2.64-.5-5.36-.5-8 0C6 2 5 2 5 2c-.3 1.15-.3 2.35 0 3.5A5.403 5.403 0 0 0 4 9c0 3.5 3 5.5 6 5.5-.39.49-.68 1.05-.85 1.65-.17.6-.22 1.23-.15 1.85v4"/>
            <path d="M9 18c-4.51 2-5-2-7-2"/>
        </svg>
    );
}
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {Separator} from '@/components/ui/separator';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue,} from '@/components/ui/select';
import {toast} from 'sonner';
import {type LlmProvider, LLM_PROVIDERS, useLlmConfig} from '@/hooks/use-llm-config';
import {useLlmStatus} from '@/hooks/use-llm-status';
import {LoomSettingsSection} from '@/components/coding-agents/LoomSettingsSection';
import {useClaudeCodeHooksStatus, useAttachClaudeCodeHooks, useDetachClaudeCodeHooks} from '@/hooks/use-claude-code-hooks';

const keyboardShortcuts = [
    {key: 'R', description: 'Go to Resources'},
    {key: 'T', description: 'Go to Traces'},
    {key: 'C', description: 'Go to Console / Logs'},
    {key: 'S', description: 'Go to Loom'},
    {key: 'M', description: 'Go to Metrics / GenAI'},
    {key: '/', description: 'Go to Search'},
    {key: 'A', description: 'Go to Agents'},
    {key: 'B', description: 'Go to Bot'},
    {key: ',', description: 'Open Settings'},
    {key: '?', description: 'Show keyboard shortcuts'},
    {key: 'Ctrl + /', description: 'Focus search'},
    {key: 'Escape', description: 'Close panel / Clear selection'},
];

function AiSettingsTab() {
    const {config, setConfig, isConfigured} = useLlmConfig();
    const {data: llmStatus} = useLlmStatus();
    const [provider, setProvider] = useState<LlmProvider>(config?.provider ?? 'openai');
    const [apiKey, setApiKey] = useState(config?.apiKey ?? '');
    const [model, setModel] = useState(config?.model ?? '');
    const [endpoint, setEndpoint] = useState(config?.endpoint ?? '');

    const providerInfo = LLM_PROVIDERS.find(p => p.value === provider)!;

    const handleSave = () => {
        if (providerInfo.needsKey && !apiKey.trim()) {
            toast.error('API key is required');
            return;
        }
        setConfig({
            provider,
            apiKey: apiKey.trim() || undefined,
            model: model.trim() || providerInfo.defaultModel || undefined,
            endpoint: endpoint.trim() || providerInfo.defaultEndpoint,
        });
        toast.success('AI provider saved');
    };

    const handleClear = () => {
        setConfig(null);
        setProvider('openai');
        setApiKey('');
        setModel('');
        setEndpoint('');
        toast.success('AI provider cleared');
    };

    return (
        <>
            {/* Server status */}
            {llmStatus?.configured && (
                <Card>
                    <CardContent className="py-4">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="font-medium">
                                    {llmStatus.provider === 'github-models'
                                        ? 'GitHub Models (free)'
                                        : 'Server AI configured'}
                                </p>
                                <p className="text-sm text-muted-foreground">
                                    {llmStatus.provider === 'github-models'
                                        ? llmStatus.model ?? 'gpt-4o-mini'
                                        : `${llmStatus.provider}${llmStatus.model ? ` / ${llmStatus.model}` : ''}`}
                                </p>
                            </div>
                            <Badge>{llmStatus.provider === 'github-models' ? 'Auto' : 'Active'}</Badge>
                        </div>
                        <p className="text-xs text-muted-foreground mt-2">
                            {llmStatus.provider === 'github-models'
                                ? 'Using your GitHub account for free AI inference. Rate-limited but zero-config.'
                                : 'The server has a built-in LLM. BYOK config below is optional and will be used as fallback.'}
                        </p>
                    </CardContent>
                </Card>
            )}

            {/* BYOK config */}
            <Card>
                <CardHeader>
                    <CardTitle className="text-base">Bring Your Own Key (BYOK)</CardTitle>
                    <CardDescription>
                        Use your own API key for AI chat. Keys stay in your browser only.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Provider</label>
                        <Select value={provider} onValueChange={(v) => setProvider(v as LlmProvider)}>
                            <SelectTrigger aria-label="AI provider">
                                <SelectValue/>
                            </SelectTrigger>
                            <SelectContent>
                                {LLM_PROVIDERS.map(p => (
                                    <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>

                    {providerInfo.needsKey && (
                        <div className="space-y-2">
                            <label className="text-sm font-medium">API Key</label>
                            <Input
                                type="password"
                                value={apiKey}
                                onChange={(e) => setApiKey(e.target.value)}
                                placeholder={`${providerInfo.label} API key`}
                                aria-label="API key"
                            />
                        </div>
                    )}

                    <div className="space-y-2">
                        <label className="text-sm font-medium">Model</label>
                        <Input
                            value={model}
                            onChange={(e) => setModel(e.target.value)}
                            placeholder={providerInfo.defaultModel || 'Model name'}
                            aria-label="Model name"
                        />
                    </div>

                    {(provider === 'ollama' || provider === 'openai-compatible') && (
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Endpoint</label>
                            <Input
                                value={endpoint}
                                onChange={(e) => setEndpoint(e.target.value)}
                                placeholder={providerInfo.defaultEndpoint ?? 'https://...'}
                                aria-label="API endpoint"
                            />
                        </div>
                    )}

                    <Separator/>

                    <div className="flex gap-2">
                        <Button onClick={handleSave} size="sm">
                            Save
                        </Button>
                        {isConfigured && (
                            <Button variant="outline" size="sm" onClick={handleClear}>
                                <Trash className="w-4 h-4 mr-2"/>
                                Clear
                            </Button>
                        )}
                    </div>

                    {isConfigured && (
                        <div className="flex items-center gap-2 text-sm">
                            <Badge variant="secondary">{config!.provider}</Badge>
                            {config!.model && <span className="text-muted-foreground">{config!.model}</span>}
                        </div>
                    )}
                </CardContent>
            </Card>
        </>
    );
}

export function SettingsPage() {
    const navigate = useNavigate();
    const [theme, setTheme] = useState<'dark' | 'light' | 'system'>('dark');
    const [refreshInterval, setRefreshInterval] = useState('5');
    const [maxLogLines, setMaxLogLines] = useState('1000');
    const [disconnecting, setDisconnecting] = useState(false);

    const {data: ghStatus, isLoading: ghLoading, refetch: refetchGh} = useQuery({
        queryKey: ['github-status'],
        queryFn: async () => {
            const res = await fetch('/api/v1/github/status');
            if (!res.ok) return {configured: false, user: null, authMethod: 'none'};
            return res.json() as Promise<{ configured: boolean; user: { login: string; name: string; avatarUrl: string } | null; authMethod: string }>;
        },
    });

    const {data: claudeHooksStatus, isLoading: claudeHooksLoading} = useClaudeCodeHooksStatus();
    const attachHooks = useAttachClaudeCodeHooks();
    const detachHooks = useDetachClaudeCodeHooks();

    const handleDisconnectGitHub = async () => {
        setDisconnecting(true);
        try {
            await fetch('/api/v1/github/token', {method: 'DELETE'});
            toast.success('GitHub disconnected');
            await refetchGh();
        } catch {
            toast.error('Failed to disconnect');
        } finally {
            setDisconnecting(false);
        }
    };

    return (
        <div className="p-6 max-w-4xl mx-auto space-y-6">
            <div>
                <h1 className="text-2xl font-bold">Settings</h1>
                <p className="text-muted-foreground">
                    Configure your qyl. dashboard preferences
                </p>
            </div>

            <Tabs defaultValue="general" className="space-y-6">
                <TabsList className="grid w-full grid-cols-7">
                    <TabsTrigger value="general">
                        <GearSix className="w-4 h-4 mr-2"/>
                        General
                    </TabsTrigger>
                    <TabsTrigger value="ai">
                        <Robot className="w-4 h-4 mr-2"/>
                        AI
                    </TabsTrigger>
                    <TabsTrigger value="Loom">
                        <Brain className="w-4 h-4 mr-2"/>
                        Loom
                    </TabsTrigger>
                    <TabsTrigger value="appearance">
                        <PaintBrush className="w-4 h-4 mr-2"/>
                        Appearance
                    </TabsTrigger>
                    <TabsTrigger value="shortcuts">
                        <Keyboard className="w-4 h-4 mr-2"/>
                        Shortcuts
                    </TabsTrigger>
                    <TabsTrigger value="data">
                        <Database className="w-4 h-4 mr-2"/>
                        Data
                    </TabsTrigger>
                    <TabsTrigger value="integrations">
                        <GitHubIcon className="w-4 h-4 mr-2"/>
                        Integrations
                    </TabsTrigger>
                </TabsList>

                {/* General Settings */}
                <TabsContent value="general" className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Notifications</CardTitle>
                            <CardDescription>
                                Configure alert notifications
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="font-medium">Error Alerts</p>
                                    <p className="text-sm text-muted-foreground">
                                        Show notification for new errors
                                    </p>
                                </div>
                                <Badge variant="secondary">Coming Soon</Badge>
                            </div>
                            <Separator/>
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="font-medium">Cost Alerts</p>
                                    <p className="text-sm text-muted-foreground">
                                        Alert when GenAI cost exceeds threshold
                                    </p>
                                </div>
                                <Badge variant="secondary">Coming Soon</Badge>
                            </div>
                        </CardContent>
                    </Card>
                </TabsContent>

                {/* AI Provider */}
                <TabsContent value="ai" className="space-y-4">
                    <AiSettingsTab/>
                </TabsContent>

                {/* Loom */}
                <TabsContent value="Loom" className="space-y-4">
                    <LoomSettingsSection/>
                </TabsContent>

                {/* Appearance */}
                <TabsContent value="appearance" className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Theme</CardTitle>
                            <CardDescription>
                                Choose your preferred color scheme
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="grid grid-cols-3 gap-4">
                                <Button
                                    variant={theme === 'dark' ? 'default' : 'outline'}
                                    className="justify-start"
                                    onClick={() => setTheme('dark')}
                                >
                                    <Moon className="w-4 h-4 mr-2"/>
                                    Dark
                                </Button>
                                <Button
                                    variant={theme === 'light' ? 'default' : 'outline'}
                                    className="justify-start"
                                    onClick={() => setTheme('light')}
                                >
                                    <Sun className="w-4 h-4 mr-2"/>
                                    Light
                                </Button>
                                <Button
                                    variant={theme === 'system' ? 'default' : 'outline'}
                                    className="justify-start"
                                    onClick={() => setTheme('system')}
                                >
                                    <Monitor className="w-4 h-4 mr-2"/>
                                    System
                                </Button>
                            </div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Density</CardTitle>
                            <CardDescription>
                                Adjust the information density
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <Select defaultValue="comfortable">
                                <SelectTrigger className="w-48" aria-label="Display density">
                                    <SelectValue/>
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="compact">Compact</SelectItem>
                                    <SelectItem value="comfortable">Comfortable</SelectItem>
                                    <SelectItem value="spacious">Spacious</SelectItem>
                                </SelectContent>
                            </Select>
                        </CardContent>
                    </Card>
                </TabsContent>

                {/* Keyboard Shortcuts */}
                <TabsContent value="shortcuts" className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Keyboard Shortcuts</CardTitle>
                            <CardDescription>
                                Quick navigation and actions. Press <kbd className="kbd">?</kbd> anywhere to see this
                                list.
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="space-y-2">
                                {keyboardShortcuts.map((shortcut) => (
                                    <div
                                        key={shortcut.key}
                                        className="flex items-center justify-between py-2 border-b border-border last:border-0"
                                    >
                                        <span className="text-sm">{shortcut.description}</span>
                                        <kbd className="kbd min-w-fit">{shortcut.key}</kbd>
                                    </div>
                                ))}
                            </div>
                        </CardContent>
                    </Card>
                </TabsContent>

                {/* Integrations */}
                <TabsContent value="integrations" className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">GitHub Integration</CardTitle>
                            <CardDescription>
                                Connect GitHub for repository discovery and Copilot integration
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            {ghLoading ? (
                                <div className="flex items-center gap-2 text-muted-foreground">
                                    <SpinnerGap className="w-4 h-4 animate-spin"/>
                                    Checking connection...
                                </div>
                            ) : ghStatus?.configured && ghStatus.user ? (
                                <div className="space-y-4">
                                    <div className="flex items-center justify-between">
                                        <div className="flex items-center gap-3">
                                            {ghStatus.user.avatarUrl && (
                                                <img
                                                    src={ghStatus.user.avatarUrl}
                                                    alt={ghStatus.user.login}
                                                    className="w-10 h-10 border-2 border-border"
                                                />
                                            )}
                                            <div>
                                                <p className="font-medium">{ghStatus.user.login}</p>
                                                {ghStatus.user.name && (
                                                    <p className="text-sm text-muted-foreground">{ghStatus.user.name}</p>
                                                )}
                                            </div>
                                        </div>
                                        <Badge variant="secondary">
                                            {ghStatus.authMethod === 'device_flow' ? 'Device Flow' : ghStatus.authMethod === 'pat' ? 'Personal Token' : ghStatus.authMethod === 'env' ? 'Environment' : ghStatus.authMethod}
                                        </Badge>
                                    </div>
                                    {ghStatus.authMethod !== 'env' && (
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={handleDisconnectGitHub}
                                            disabled={disconnecting}
                                        >
                                            {disconnecting ? <SpinnerGap className="w-4 h-4 animate-spin mr-2"/> : <LinkBreak className="w-4 h-4 mr-2"/>}
                                            Disconnect
                                        </Button>
                                    )}
                                </div>
                            ) : (
                                <div className="flex items-center justify-between">
                                    <div>
                                        <p className="font-medium">Not connected</p>
                                        <p className="text-sm text-muted-foreground">
                                            Connect GitHub to enable repository discovery
                                        </p>
                                    </div>
                                    <Button variant="outline" onClick={() => navigate('/onboarding')}>
                                        <GitHubIcon className="w-4 h-4 mr-2"/>
                                        Connect GitHub
                                    </Button>
                                </div>
                            )}
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Claude Code Observability</CardTitle>
                            <CardDescription>
                                Attach hooks to observe tool calls and agent lifecycle events
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            {claudeHooksLoading ? (
                                <div className="flex items-center gap-2 text-muted-foreground">
                                    <SpinnerGap className="w-4 h-4 animate-spin"/>
                                    Checking hook status...
                                </div>
                            ) : (
                                <div className="flex items-center justify-between">
                                    <div className="flex items-center gap-3">
                                        <Terminal className="w-5 h-5 text-muted-foreground"/>
                                        <div>
                                            <p className="font-medium">
                                                {claudeHooksStatus?.attached ? 'Hooks attached' : 'Hooks not attached'}
                                            </p>
                                            <p className="text-sm text-muted-foreground">
                                                PostToolUse, SubagentStart, SubagentStop
                                            </p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-3">
                                        <Badge variant={claudeHooksStatus?.attached ? 'secondary' : 'outline'}>
                                            {claudeHooksStatus?.attached ? 'Attached' : 'Detached'}
                                        </Badge>
                                        <Button
                                            variant={claudeHooksStatus?.attached ? 'outline' : 'default'}
                                            size="sm"
                                            disabled={attachHooks.isPending || detachHooks.isPending}
                                            onClick={() => {
                                                if (claudeHooksStatus?.attached) {
                                                    detachHooks.mutate(undefined, {
                                                        onSuccess: () => toast.success('Claude Code hooks detached'),
                                                        onError: () => toast.error('Failed to detach hooks'),
                                                    });
                                                } else {
                                                    attachHooks.mutate(undefined, {
                                                        onSuccess: () => toast.success('Claude Code hooks attached'),
                                                        onError: () => toast.error('Failed to attach hooks'),
                                                    });
                                                }
                                            }}
                                        >
                                            {(attachHooks.isPending || detachHooks.isPending) && (
                                                <SpinnerGap className="w-4 h-4 animate-spin mr-2"/>
                                            )}
                                            {claudeHooksStatus?.attached ? 'Stop Observing' : 'Start Observing'}
                                        </Button>
                                    </div>
                                </div>
                            )}
                            <p className="text-xs text-muted-foreground">
                                New Claude Code sessions will pick up hook changes. Existing sessions are unaffected.
                            </p>
                        </CardContent>
                    </Card>
                </TabsContent>

                {/* Data Settings */}
                <TabsContent value="data" className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Auto-Refresh</CardTitle>
                            <CardDescription>
                                Configure automatic data refresh interval
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="flex items-center gap-4">
                                <Select value={refreshInterval} onValueChange={setRefreshInterval}>
                                    <SelectTrigger className="w-48" aria-label="Auto-refresh interval">
                                        <SelectValue/>
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="1">Every 1 second</SelectItem>
                                        <SelectItem value="5">Every 5 seconds</SelectItem>
                                        <SelectItem value="10">Every 10 seconds</SelectItem>
                                        <SelectItem value="30">Every 30 seconds</SelectItem>
                                        <SelectItem value="60">Every 60 seconds</SelectItem>
                                        <SelectItem value="0">Disabled</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Log Buffer</CardTitle>
                            <CardDescription>
                                Maximum number of log lines to keep in memory
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="flex items-center gap-4">
                                <Input
                                    type="number"
                                    value={maxLogLines}
                                    onChange={(e) => setMaxLogLines(e.target.value)}
                                    className="w-32"
                                    aria-label="Maximum log lines"
                                />
                                <span className="text-sm text-muted-foreground">lines</span>
                            </div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader>
                            <CardTitle className="text-base">Storage</CardTitle>
                            <CardDescription>
                                DuckDB storage information
                            </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="grid grid-cols-2 gap-4 text-sm">
                                <div>
                                    <span className="text-muted-foreground">Database Path:</span>
                                    <code className="ml-2 font-mono text-primary">/data/qyl.duckdb</code>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Database Size:</span>
                                    <span className="ml-2 font-mono">--</span>
                                </div>
                            </div>
                            <Separator/>
                            <div className="flex gap-2">
                                <Button variant="outline" size="sm" disabled>
                                    Export Data
                                </Button>
                                <Button variant="destructive" size="sm" disabled>
                                    Clear All Data
                                </Button>
                            </div>
                            <p className="text-xs text-muted-foreground">
                                Data management features coming soon
                            </p>
                        </CardContent>
                    </Card>
                </TabsContent>
            </Tabs>

            {/* Version info */}
            <Card>
                <CardContent className="py-4">
                    <div className="flex items-center justify-between text-sm">
                        <div className="flex items-center gap-2">
                            <span className="text-2xl font-bold text-gradient">qyl.</span>
                            <span className="text-muted-foreground">AI Observability Dashboard</span>
                        </div>
                        <div className="text-muted-foreground">
                            v0.1.0 | Built with DuckDB + SSE
                        </div>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
