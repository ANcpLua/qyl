import {useCallback, useEffect, useRef, useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {Bot, Key, Send, Settings, Square, Trash2, X} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import type {CopilotMessage} from '@/hooks/use-copilot';
import {useCopilotChat} from '@/hooks/use-copilot';
import {type LlmProvider, LLM_PROVIDERS, useLlmConfig} from '@/hooks/use-llm-config';
import {useLlmStatus} from '@/hooks/use-llm-status';
import {CopilotSuggestions} from './CopilotSuggestions';
import {ToolCallBubble} from './ToolCallBubble';

interface CopilotPanelProps {
    open: boolean;
    onClose: () => void;
    username?: string;
}

export function CopilotPanel({open, onClose, username}: CopilotPanelProps) {
    const {messages, isStreaming, streamingContent, activeToolCalls, send, stop, clear} = useCopilotChat();
    const {config: llmConfig, setConfig: setLlmConfig, isConfigured: byokConfigured} = useLlmConfig();
    const {data: llmStatus} = useLlmStatus();
    const [input, setInput] = useState('');
    const messagesEndRef = useRef<HTMLDivElement>(null);

    const serverConfigured = llmStatus?.configured ?? false;
    const needsSetup = !serverConfigured && !byokConfigured;

    // Auto-scroll to bottom
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({behavior: 'smooth'});
    }, [messages, streamingContent]);

    const handleSend = useCallback(() => {
        if (!input.trim() || isStreaming) return;
        // Only pass BYOK config when server doesn't have its own LLM
        send(input.trim(), serverConfigured ? undefined : llmConfig);
        setInput('');
    }, [input, isStreaming, send, serverConfigured, llmConfig]);

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            handleSend();
        }
    };

    const handleSuggestionSelect = useCallback(
        (question: string) => {
            if (isStreaming) return;
            send(question, serverConfigured ? undefined : llmConfig);
        },
        [isStreaming, send, serverConfigured, llmConfig],
    );

    if (!open) return null;

    return (
        <div
            className="fixed bottom-20 right-4 w-96 h-[32rem] flex flex-col border-3 border-signal-purple bg-brutal-carbon z-50 shadow-[4px_4px_0_0_rgba(139,92,246,0.3)]">
            {/* Header */}
            <div className="flex items-center justify-between px-4 py-3 border-b-3 border-brutal-zinc bg-brutal-dark">
                <div className="flex items-center gap-2">
                    <Bot className="w-4 h-4 text-signal-purple"/>
                    <span className="text-xs font-bold tracking-[0.15em] text-signal-purple">
                        COPILOT
                    </span>
                    {username && (
                        <span className="text-[10px] text-brutal-slate">@{username}</span>
                    )}
                    {!serverConfigured && byokConfigured && (
                        <span className="text-[10px] text-signal-orange">BYOK</span>
                    )}
                </div>
                <div className="flex items-center gap-1">
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={clear}
                        className="h-6 w-6 text-brutal-slate hover:text-signal-orange"
                        aria-label="Clear conversation"
                    >
                        <Trash2 className="w-3.5 h-3.5"/>
                    </Button>
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={onClose}
                        className="h-6 w-6 text-brutal-slate hover:text-brutal-white"
                        aria-label="Close Copilot panel"
                    >
                        <X className="w-3.5 h-3.5"/>
                    </Button>
                </div>
            </div>

            {/* Messages */}
            <div className="flex-1 overflow-y-auto p-3 space-y-3">
                {needsSetup && messages.length === 0 && !isStreaming ? (
                    <InlineLlmSetup onSave={setLlmConfig}/>
                ) : (
                    <>
                        {messages.length === 0 && !isStreaming && (
                            <CopilotSuggestions onSelect={handleSuggestionSelect}/>
                        )}

                        {messages.map((msg) => (
                            <MessageBubble key={msg.id} message={msg}/>
                        ))}

                        {/* Active tool calls during streaming */}
                        {isStreaming && activeToolCalls.length > 0 && (
                            <div className="space-y-2">
                                {activeToolCalls.map((tc) => (
                                    <ToolCallBubble key={tc.id} toolCall={tc}/>
                                ))}
                            </div>
                        )}

                        {/* Streaming indicator */}
                        {isStreaming && streamingContent && (
                            <div className="bg-brutal-dark border-2 border-brutal-zinc p-3">
                                <div className="text-xs text-brutal-white whitespace-pre-wrap break-words">
                                    {streamingContent}
                                </div>
                                <div className="flex items-center gap-1.5 mt-2">
                                    <div className="w-1.5 h-1.5 rounded-full bg-signal-purple animate-pulse"/>
                                    <span className="text-[10px] text-brutal-slate">
                                        {activeToolCalls.some(tc => tc.status === 'calling')
                                            ? `Calling ${activeToolCalls.find(tc => tc.status === 'calling')?.name}...`
                                            : 'Streaming...'}
                                    </span>
                                </div>
                            </div>
                        )}

                        {isStreaming && !streamingContent && (
                            <div className="flex items-center gap-2 p-3">
                                <div className="w-1.5 h-1.5 rounded-full bg-signal-purple animate-pulse"/>
                                <span className="text-xs text-brutal-slate">
                                    {activeToolCalls.some(tc => tc.status === 'calling')
                                        ? `Calling ${activeToolCalls.find(tc => tc.status === 'calling')?.name}...`
                                        : 'Thinking...'}
                                </span>
                            </div>
                        )}
                    </>
                )}

                <div ref={messagesEndRef}/>
            </div>

            {/* Input */}
            <div className="border-t-3 border-brutal-zinc p-3">
                <div className="flex gap-2">
                    <Input
                        value={input}
                        onChange={(e) => setInput(e.target.value)}
                        onKeyDown={handleKeyDown}
                        placeholder={needsSetup ? 'Configure AI above to start...' : 'Ask Copilot...'}
                        disabled={isStreaming || needsSetup}
                        className="flex-1 bg-brutal-dark border-2 border-brutal-zinc text-brutal-white placeholder:text-brutal-slate text-xs font-bold tracking-wider focus:border-signal-purple"
                        aria-label="Message to Copilot"
                    />
                    {isStreaming ? (
                        <Button
                            variant="outline"
                            size="icon"
                            onClick={stop}
                            className="border-2 border-signal-red text-signal-red hover:bg-signal-red/10"
                            aria-label="Stop streaming"
                        >
                            <Square className="w-3.5 h-3.5"/>
                        </Button>
                    ) : (
                        <Button
                            variant="outline"
                            size="icon"
                            onClick={handleSend}
                            disabled={!input.trim() || needsSetup}
                            className="border-2 border-signal-purple text-signal-purple hover:bg-signal-purple/10 disabled:opacity-30"
                            aria-label="Send message"
                        >
                            <Send className="w-3.5 h-3.5"/>
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
}

/** Inline LLM setup form shown when no provider is configured */
function InlineLlmSetup({onSave}: { onSave: (config: { provider: LlmProvider; apiKey?: string; model?: string; endpoint?: string }) => void }) {
    const navigate = useNavigate();
    const [provider, setProvider] = useState<LlmProvider>('openai');
    const [apiKey, setApiKey] = useState('');

    const providerInfo = LLM_PROVIDERS.find(p => p.value === provider)!;

    const handleSave = () => {
        if (providerInfo.needsKey && !apiKey.trim()) return;
        onSave({
            provider,
            apiKey: apiKey.trim() || undefined,
            model: providerInfo.defaultModel || undefined,
            endpoint: providerInfo.defaultEndpoint,
        });
    };

    return (
        <div className="flex flex-col gap-4 p-2">
            <div className="flex items-center gap-2">
                <Key className="w-4 h-4 text-signal-orange"/>
                <span className="text-xs font-bold tracking-[0.15em] text-signal-orange">
                    AI SETUP
                </span>
            </div>

            <p className="text-[11px] text-brutal-slate leading-relaxed">
                No AI provider configured. Add your API key to enable chat.
                Keys stay in your browser — never sent to qyl servers.
            </p>

            <div className="space-y-3">
                <Select value={provider} onValueChange={(v) => setProvider(v as LlmProvider)}>
                    <SelectTrigger
                        className="bg-brutal-dark border-2 border-brutal-zinc text-brutal-white text-xs"
                        aria-label="AI provider"
                    >
                        <SelectValue/>
                    </SelectTrigger>
                    <SelectContent>
                        {LLM_PROVIDERS.map(p => (
                            <SelectItem key={p.value} value={p.value}>{p.label}</SelectItem>
                        ))}
                    </SelectContent>
                </Select>

                {providerInfo.needsKey && (
                    <Input
                        type="password"
                        value={apiKey}
                        onChange={(e) => setApiKey(e.target.value)}
                        placeholder={`${providerInfo.label} API key`}
                        className="bg-brutal-dark border-2 border-brutal-zinc text-brutal-white placeholder:text-brutal-slate text-xs"
                        aria-label="API key"
                        onKeyDown={(e) => {
                            if (e.key === 'Enter') handleSave();
                        }}
                    />
                )}

                <Button
                    onClick={handleSave}
                    disabled={providerInfo.needsKey && !apiKey.trim()}
                    className="w-full bg-signal-purple text-brutal-white border-2 border-signal-purple hover:bg-signal-purple/80 text-xs font-bold tracking-wider disabled:opacity-30"
                >
                    Connect
                </Button>
            </div>

            <button
                onClick={() => navigate('/settings')}
                className="flex items-center gap-1.5 text-[10px] text-brutal-slate hover:text-signal-purple transition-colors"
            >
                <Settings className="w-3 h-3"/>
                Advanced config in Settings
            </button>
        </div>
    );
}

function MessageBubble({message}: { message: CopilotMessage }) {
    const isUser = message.role === 'user';

    return (
        <div className={`flex flex-col ${isUser ? 'items-end' : 'items-start'} gap-2`}>
            {/* Tool calls rendered before the message content */}
            {!isUser && message.toolCalls && message.toolCalls.length > 0 && (
                <div className="w-full space-y-2">
                    {message.toolCalls.map((tc) => (
                        <ToolCallBubble key={tc.id} toolCall={tc}/>
                    ))}
                </div>
            )}
            <div
                className={`max-w-[85%] p-3 text-xs ${
                    isUser
                        ? 'bg-signal-purple/20 border-2 border-signal-purple text-brutal-white'
                        : 'bg-brutal-dark border-2 border-brutal-zinc text-brutal-white'
                }`}
            >
                <div className="whitespace-pre-wrap break-words">{message.content}</div>
                {message.outputTokens && message.outputTokens > 0 && (
                    <div className="mt-2 text-[10px] text-brutal-slate">
                        {message.outputTokens} tokens
                    </div>
                )}
            </div>
        </div>
    );
}
