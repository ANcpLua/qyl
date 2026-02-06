import {useCallback, useEffect, useRef, useState} from 'react';
import {Bot, Send, Square, Trash2, X} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {useCopilotChat} from '@/hooks/use-copilot';
import {CopilotSuggestions} from './CopilotSuggestions';
import {ToolCallBubble} from './ToolCallBubble';
import type {CopilotMessage} from '@/hooks/use-copilot';

interface CopilotPanelProps {
    open: boolean;
    onClose: () => void;
    username?: string;
}

export function CopilotPanel({open, onClose, username}: CopilotPanelProps) {
    const {messages, isStreaming, streamingContent, activeToolCalls, send, stop, clear} = useCopilotChat();
    const [input, setInput] = useState('');
    const messagesEndRef = useRef<HTMLDivElement>(null);

    // Auto-scroll to bottom
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({behavior: 'smooth'});
    }, [messages, streamingContent]);

    const handleSend = useCallback(() => {
        if (!input.trim() || isStreaming) return;
        send(input.trim());
        setInput('');
    }, [input, isStreaming, send]);

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            handleSend();
        }
    };

    const handleSuggestionSelect = useCallback(
        (question: string) => {
            if (isStreaming) return;
            send(question);
        },
        [isStreaming, send],
    );

    if (!open) return null;

    return (
        <div className="fixed bottom-20 right-4 w-96 h-[32rem] flex flex-col border-3 border-signal-purple bg-brutal-carbon z-50 shadow-[4px_4px_0_0_rgba(139,92,246,0.3)]">
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
                </div>
                <div className="flex items-center gap-1">
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={clear}
                        className="h-6 w-6 text-brutal-slate hover:text-signal-orange"
                        title="New conversation"
                    >
                        <Trash2 className="w-3.5 h-3.5"/>
                    </Button>
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={onClose}
                        className="h-6 w-6 text-brutal-slate hover:text-brutal-white"
                    >
                        <X className="w-3.5 h-3.5"/>
                    </Button>
                </div>
            </div>

            {/* Messages */}
            <div className="flex-1 overflow-y-auto p-3 space-y-3">
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

                <div ref={messagesEndRef}/>
            </div>

            {/* Input */}
            <div className="border-t-3 border-brutal-zinc p-3">
                <div className="flex gap-2">
                    <Input
                        value={input}
                        onChange={(e) => setInput(e.target.value)}
                        onKeyDown={handleKeyDown}
                        placeholder="Ask Copilot..."
                        disabled={isStreaming}
                        className="flex-1 bg-brutal-dark border-2 border-brutal-zinc text-brutal-white placeholder:text-brutal-slate text-xs font-bold tracking-wider focus:border-signal-purple"
                    />
                    {isStreaming ? (
                        <Button
                            variant="outline"
                            size="icon"
                            onClick={stop}
                            className="border-2 border-signal-red text-signal-red hover:bg-signal-red/10"
                        >
                            <Square className="w-3.5 h-3.5"/>
                        </Button>
                    ) : (
                        <Button
                            variant="outline"
                            size="icon"
                            onClick={handleSend}
                            disabled={!input.trim()}
                            className="border-2 border-signal-purple text-signal-purple hover:bg-signal-purple/10 disabled:opacity-30"
                        >
                            <Send className="w-3.5 h-3.5"/>
                        </Button>
                    )}
                </div>
            </div>
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
