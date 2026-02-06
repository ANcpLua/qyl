import {useCallback, useEffect, useRef, useState} from 'react';
import {useQuery} from '@tanstack/react-query';

// Types
export interface CopilotAuthStatus {
    isAuthenticated: boolean;
    authMethod?: string;
    username?: string;
    capabilities?: string[];
    error?: string;
}

export interface ToolCallEvent {
    id: string;
    name: string;
    arguments?: string;
    result?: string;
    status: 'calling' | 'completed' | 'error';
}

export interface CopilotMessage {
    id: string;
    role: 'user' | 'assistant';
    content: string;
    timestamp: Date;
    outputTokens?: number;
    toolCalls?: ToolCallEvent[];
}

export const copilotKeys = {
    status: ['copilot', 'status'] as const,
};

// Fetch copilot status
async function fetchCopilotStatus(): Promise<CopilotAuthStatus> {
    const res = await fetch('/api/v1/copilot/status', {credentials: 'include'});
    if (!res.ok) throw new Error('Copilot status check failed');
    return res.json();
}

/**
 * Poll Copilot authentication status.
 * Returns auth state so the UI can show/hide the Copilot button.
 */
export function useCopilotStatus() {
    return useQuery({
        queryKey: copilotKeys.status,
        queryFn: fetchCopilotStatus,
        retry: false,
        staleTime: 5 * 60 * 1000, // 5 minutes (matches server cache)
        refetchInterval: 5 * 60 * 1000,
    });
}

/**
 * Manages Copilot chat: message history, SSE streaming, send function.
 */
export function useCopilotChat() {
    const [messages, setMessages] = useState<CopilotMessage[]>([]);
    const [isStreaming, setIsStreaming] = useState(false);
    const [streamingContent, setStreamingContent] = useState('');
    const [activeToolCalls, setActiveToolCalls] = useState<ToolCallEvent[]>([]);
    const abortRef = useRef<AbortController | null>(null);
    const messageIdCounter = useRef(0);
    const toolCallIdCounter = useRef(0);

    const nextId = () => `msg-${++messageIdCounter.current}`;

    const send = useCallback(async (prompt: string) => {
        if (isStreaming || !prompt.trim()) return;

        // Add user message
        const userMessage: CopilotMessage = {
            id: nextId(),
            role: 'user',
            content: prompt,
            timestamp: new Date(),
        };
        setMessages(prev => [...prev, userMessage]);
        setIsStreaming(true);
        setStreamingContent('');

        const abortController = new AbortController();
        abortRef.current = abortController;

        try {
            const response = await fetch('/api/v1/copilot/chat', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                credentials: 'include',
                body: JSON.stringify({prompt}),
                signal: abortController.signal,
            });

            if (!response.ok || !response.body) {
                throw new Error(`HTTP ${response.status}`);
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';
            let fullContent = '';
            let outputTokens = 0;
            let currentEvent = '';
            const collectedToolCalls: ToolCallEvent[] = [];

            while (true) {
                const {done, value} = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, {stream: true});
                const lines = buffer.split('\n');
                buffer = lines.pop() ?? '';

                for (const line of lines) {
                    if (line.startsWith('event: ')) {
                        currentEvent = line.slice(7).trim();
                    } else if (line.startsWith('data: ')) {
                        try {
                            const data = JSON.parse(line.slice(6));
                            const kind = currentEvent || data.kind;

                            if (kind === 'tool_call' || data.kind === 'ToolCall') {
                                const tc: ToolCallEvent = {
                                    id: `tc-${++toolCallIdCounter.current}`,
                                    name: data.toolName ?? 'unknown',
                                    arguments: data.toolArguments,
                                    status: 'calling',
                                };
                                collectedToolCalls.push(tc);
                                setActiveToolCalls([...collectedToolCalls]);
                            } else if (kind === 'tool_result' || data.kind === 'ToolResult') {
                                const existing = collectedToolCalls.find(
                                    tc => tc.name === data.toolName && tc.status === 'calling'
                                );
                                if (existing) {
                                    existing.result = data.toolResult;
                                    existing.status = data.error ? 'error' : 'completed';
                                    setActiveToolCalls([...collectedToolCalls]);
                                }
                            } else if ((kind === 'content' || data.kind === 'Content') && data.content) {
                                fullContent += data.content;
                                setStreamingContent(fullContent);
                            } else if (kind === 'completed' || data.kind === 'Completed') {
                                outputTokens = data.outputTokens ?? 0;
                            } else if (kind === 'error' || data.kind === 'Error') {
                                fullContent += `\n\n**Error:** ${data.error}`;
                                setStreamingContent(fullContent);
                            }
                        } catch {
                            // Skip malformed SSE data
                        }
                        currentEvent = '';
                    }
                }
            }

            // Add assistant message with tool calls
            const assistantMessage: CopilotMessage = {
                id: nextId(),
                role: 'assistant',
                content: fullContent || 'No response received.',
                timestamp: new Date(),
                outputTokens,
                toolCalls: collectedToolCalls.length > 0 ? [...collectedToolCalls] : undefined,
            };
            setMessages(prev => [...prev, assistantMessage]);
        } catch (err) {
            if ((err as Error).name !== 'AbortError') {
                const errorMessage: CopilotMessage = {
                    id: nextId(),
                    role: 'assistant',
                    content: `**Error:** ${(err as Error).message}`,
                    timestamp: new Date(),
                };
                setMessages(prev => [...prev, errorMessage]);
            }
        } finally {
            setIsStreaming(false);
            setStreamingContent('');
            setActiveToolCalls([]);
            abortRef.current = null;
        }
    }, [isStreaming]);

    const stop = useCallback(() => {
        abortRef.current?.abort();
    }, []);

    const clear = useCallback(() => {
        stop();
        setMessages([]);
        setStreamingContent('');
    }, [stop]);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            abortRef.current?.abort();
        };
    }, []);

    return {
        messages,
        isStreaming,
        streamingContent,
        activeToolCalls,
        send,
        stop,
        clear,
    };
}
