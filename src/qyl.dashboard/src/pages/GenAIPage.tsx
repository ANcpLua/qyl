import {useMemo, useState} from 'react';
import {
    Activity,
    Check,
    ChevronDown,
    ChevronRight,
    Copy,
    Cpu,
    DollarSign,
    MessageSquare,
    Sparkles,
    Wrench,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Separator} from '@/components/ui/separator';
import {formatDuration} from '@/hooks/use-telemetry';
// Using legacy types with message content - OpenAPI schema doesn't include messages yet
import type {GenAISpanData, Span} from '@/types/telemetry';

// Mock GenAI spans
const mockGenAISpans: (Span & { genai: GenAISpanData })[] = [
    {
        traceId: 'trace-1',
        spanId: 'span-1',
        name: 'chat openai.chat',
        kind: 'client',
        status: 'ok',
        startTime: new Date(Date.now() - 5000).toISOString(),
        endTime: new Date(Date.now() - 2000).toISOString(),
        durationMs: 3000,
        attributes: {},
        events: [],
        links: [],
        serviceName: 'chat-service',
        genai: {
            providerName: 'openai',
            requestModel: 'gpt-4o',
            responseModel: 'gpt-4o-2024-08-06',
            operationName: 'chat',
            inputTokens: 1250,
            outputTokens: 450,
            totalTokens: 1700,
            costUsd: 0.0425,
            finishReasons: ['stop'],
            inputMessages: [
                {role: 'system', content: 'You are a helpful assistant.'},
                {role: 'user', content: 'What is the capital of France?'},
            ],
            outputMessages: [
                {
                    role: 'assistant',
                    content: 'The capital of France is Paris. It is known as the "City of Light" and is famous for landmarks like the Eiffel Tower, the Louvre Museum, and Notre-Dame Cathedral.',
                },
            ],
        },
    },
    {
        traceId: 'trace-2',
        spanId: 'span-2',
        name: 'chat anthropic.messages',
        kind: 'client',
        status: 'ok',
        startTime: new Date(Date.now() - 10000).toISOString(),
        endTime: new Date(Date.now() - 6000).toISOString(),
        durationMs: 4000,
        attributes: {},
        events: [],
        links: [],
        serviceName: 'agent-service',
        genai: {
            providerName: 'anthropic',
            requestModel: 'claude-3-5-sonnet-20241022',
            responseModel: 'claude-3-5-sonnet-20241022',
            operationName: 'chat',
            inputTokens: 2100,
            outputTokens: 850,
            totalTokens: 2950,
            costUsd: 0.0885,
            finishReasons: ['end_turn'],
            inputMessages: [
                {role: 'user', content: 'Help me write a Python function to calculate fibonacci numbers.'},
            ],
            outputMessages: [
                {
                    role: 'assistant',
                    content: `Here's a Python function to calculate Fibonacci numbers:\n\n\`\`\`python\ndef fibonacci(n: int) -> int:\n    """Calculate the nth Fibonacci number."""\n    if n <= 1:\n        return n\n    a, b = 0, 1\n    for _ in range(2, n + 1):\n        a, b = b, a + b\n    return b\n\`\`\`\n\nThis uses an iterative approach which is O(n) time and O(1) space.`,
                },
            ],
            toolCalls: [
                {
                    id: 'call_1',
                    type: 'function',
                    function: {
                        name: 'code_interpreter',
                        arguments: '{"code": "def fibonacci(n):\\n    if n <= 1:\\n        return n\\n    return fibonacci(n-1) + fibonacci(n-2)"}',
                    },
                },
            ],
        },
    },
];

interface GenAISpanCardProps {
    span: Span & { genai: GenAISpanData };
    isExpanded: boolean;
    onToggle: () => void;
}

function GenAISpanCard({span, isExpanded, onToggle}: GenAISpanCardProps) {
    const [copiedMessage, setCopiedMessage] = useState<string | null>(null);

    const copyToClipboard = (text: string, id: string) => {
        navigator.clipboard.writeText(text);
        setCopiedMessage(id);
        setTimeout(() => setCopiedMessage(null), 2000);
    };

    const providerColor = span.genai.providerName === 'openai'
        ? 'text-green-500 border-green-500'
        : span.genai.providerName === 'anthropic'
            ? 'text-orange-500 border-orange-500'
            : 'text-violet-500 border-violet-500';

    return (
        <Card className={cn('transition-all', isExpanded && 'ring-1 ring-primary/50')}>
            <CardHeader
                className="cursor-pointer hover:bg-muted/50"
                onClick={onToggle}
            >
                <div className="flex items-start gap-4">
                    {/* Expand indicator */}
                    {isExpanded ? (
                        <ChevronDown className="w-5 h-5 mt-0.5 text-muted-foreground"/>
                    ) : (
                        <ChevronRight className="w-5 h-5 mt-0.5 text-muted-foreground"/>
                    )}

                    {/* Provider/Model info */}
                    <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                            <Badge variant="outline" className={providerColor}>
                                {span.genai.providerName}
                            </Badge>
                            <Badge variant="secondary">{span.genai.requestModel}</Badge>
                            <span className="text-sm text-muted-foreground">
                {span.serviceName}
              </span>
                        </div>
                        <p className="text-sm text-muted-foreground mt-1 truncate">
                            {span.genai.inputMessages?.[span.genai.inputMessages.length - 1]?.content?.slice(0, 100)}...
                        </p>
                    </div>

                    {/* Stats */}
                    <div className="flex items-center gap-6 text-sm">
                        <div className="text-right">
                            <div className="text-muted-foreground">Tokens</div>
                            <div className="font-mono">
                                {span.genai.inputTokens?.toLocaleString()} / {span.genai.outputTokens?.toLocaleString()}
                            </div>
                        </div>
                        <div className="text-right">
                            <div className="text-muted-foreground">Duration</div>
                            <div className="font-mono">{formatDuration(span.durationMs)}</div>
                        </div>
                        <div className="text-right">
                            <div className="text-muted-foreground">Cost</div>
                            <div className="font-mono text-green-500">
                                ${span.genai.costUsd?.toFixed(4)}
                            </div>
                        </div>
                    </div>
                </div>
            </CardHeader>

            {isExpanded && (
                <CardContent className="pt-0">
                    <Separator className="mb-4"/>

                    <Tabs defaultValue="messages" className="w-full">
                        <TabsList>
                            <TabsTrigger value="messages">
                                <MessageSquare className="w-4 h-4 mr-1"/>
                                Messages
                            </TabsTrigger>
                            {span.genai.toolCalls && span.genai.toolCalls.length > 0 && (
                                <TabsTrigger value="tools">
                                    <Wrench className="w-4 h-4 mr-1"/>
                                    Tools ({span.genai.toolCalls.length})
                                </TabsTrigger>
                            )}
                            <TabsTrigger value="details">
                                <Cpu className="w-4 h-4 mr-1"/>
                                Details
                            </TabsTrigger>
                        </TabsList>

                        <TabsContent value="messages" className="mt-4 space-y-4">
                            {/* Input messages */}
                            {span.genai.inputMessages?.map((msg, i) => (
                                <div key={`input-${i}`} className="relative group">
                                    <div className={cn(
                                        'p-3 rounded-lg',
                                        msg.role === 'system' && 'bg-yellow-500/10 border border-yellow-500/20',
                                        msg.role === 'user' && 'bg-blue-500/10 border border-blue-500/20',
                                        msg.role === 'assistant' && 'bg-violet-500/10 border border-violet-500/20'
                                    )}>
                                        <div className="flex items-center justify-between mb-2">
                                            <Badge variant="outline" className="text-xs">
                                                {msg.role}
                                            </Badge>
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                className="h-6 w-6 opacity-0 group-hover:opacity-100"
                                                onClick={() => copyToClipboard(msg.content || '', `input-${i}`)}
                                            >
                                                {copiedMessage === `input-${i}` ? (
                                                    <Check className="w-3 h-3 text-green-500"/>
                                                ) : (
                                                    <Copy className="w-3 h-3"/>
                                                )}
                                            </Button>
                                        </div>
                                        <pre className="text-sm whitespace-pre-wrap font-mono">
                      {msg.content}
                    </pre>
                                    </div>
                                </div>
                            ))}

                            {/* Divider */}
                            <div className="flex items-center gap-2 text-muted-foreground">
                                <Sparkles className="w-4 h-4"/>
                                <span className="text-xs">Response</span>
                                <div className="flex-1 border-t border-border"/>
                            </div>

                            {/* Output messages */}
                            {span.genai.outputMessages?.map((msg, i) => (
                                <div key={`output-${i}`} className="relative group">
                                    <div className="p-3 rounded-lg bg-violet-500/10 border border-violet-500/20">
                                        <div className="flex items-center justify-between mb-2">
                                            <Badge variant="outline" className="text-xs">
                                                {msg.role}
                                            </Badge>
                                            <Button
                                                variant="ghost"
                                                size="icon"
                                                className="h-6 w-6 opacity-0 group-hover:opacity-100"
                                                onClick={() => copyToClipboard(msg.content || '', `output-${i}`)}
                                            >
                                                {copiedMessage === `output-${i}` ? (
                                                    <Check className="w-3 h-3 text-green-500"/>
                                                ) : (
                                                    <Copy className="w-3 h-3"/>
                                                )}
                                            </Button>
                                        </div>
                                        <pre className="text-sm whitespace-pre-wrap font-mono">
                      {msg.content}
                    </pre>
                                    </div>
                                </div>
                            ))}
                        </TabsContent>

                        <TabsContent value="tools" className="mt-4 space-y-4">
                            {span.genai.toolCalls?.map((tool, i) => (
                                <div key={i} className="p-3 rounded-lg bg-muted">
                                    <div className="flex items-center gap-2 mb-2">
                                        <Wrench className="w-4 h-4 text-primary"/>
                                        <span className="font-medium">{tool.function.name}</span>
                                        <Badge variant="outline" className="text-xs">
                                            {tool.type}
                                        </Badge>
                                    </div>
                                    <pre className="text-sm whitespace-pre-wrap font-mono bg-background p-2 rounded">
                    {JSON.stringify(JSON.parse(tool.function.arguments), null, 2)}
                  </pre>
                                </div>
                            ))}
                        </TabsContent>

                        <TabsContent value="details" className="mt-4">
                            <div className="grid grid-cols-2 gap-4 text-sm">
                                <div>
                                    <span className="text-muted-foreground">Provider:</span>
                                    <span className="ml-2">{span.genai.providerName}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Request Model:</span>
                                    <span className="ml-2">{span.genai.requestModel}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Response Model:</span>
                                    <span className="ml-2">{span.genai.responseModel}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Operation:</span>
                                    <span className="ml-2">{span.genai.operationName}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Input Tokens:</span>
                                    <span className="ml-2 font-mono">{span.genai.inputTokens?.toLocaleString()}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Output Tokens:</span>
                                    <span className="ml-2 font-mono">{span.genai.outputTokens?.toLocaleString()}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Finish Reason:</span>
                                    <span className="ml-2">{span.genai.finishReasons?.join(', ')}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Duration:</span>
                                    <span className="ml-2 font-mono">{formatDuration(span.durationMs)}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Trace ID:</span>
                                    <span className="ml-2 font-mono text-primary">{span.traceId}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Span ID:</span>
                                    <span className="ml-2 font-mono">{span.spanId}</span>
                                </div>
                            </div>
                        </TabsContent>
                    </Tabs>
                </CardContent>
            )}
        </Card>
    );
}

export function GenAIPage() {
    const [expandedSpans, setExpandedSpans] = useState<Set<string>>(new Set(['span-1']));

    // Stats
    const stats = useMemo(() => {
        return {
            totalCalls: mockGenAISpans.length,
            totalTokens: mockGenAISpans.reduce((a, s) => a + (s.genai.totalTokens || 0), 0),
            totalCost: mockGenAISpans.reduce((a, s) => a + (s.genai.costUsd || 0), 0),
            avgLatency:
                mockGenAISpans.reduce((a, s) => a + s.durationMs, 0) / mockGenAISpans.length,
        };
    }, []);

    const toggleSpan = (spanId: string) => {
        setExpandedSpans((prev) => {
            const next = new Set(prev);
            if (next.has(spanId)) {
                next.delete(spanId);
            } else {
                next.add(spanId);
            }
            return next;
        });
    };

    return (
        <div className="p-6 space-y-6">
            {/* Stats */}
            <div className="grid grid-cols-4 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Sparkles className="w-4 h-4 text-violet-500"/>
                            <span className="text-sm text-muted-foreground">GenAI Calls</span>
                        </div>
                        <div className="text-2xl font-bold mt-1">{stats.totalCalls}</div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Cpu className="w-4 h-4 text-cyan-500"/>
                            <span className="text-sm text-muted-foreground">Total Tokens</span>
                        </div>
                        <div className="text-2xl font-bold mt-1">
                            {stats.totalTokens.toLocaleString()}
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <DollarSign className="w-4 h-4 text-green-500"/>
                            <span className="text-sm text-muted-foreground">Total Cost</span>
                        </div>
                        <div className="text-2xl font-bold mt-1 text-green-500">
                            ${stats.totalCost.toFixed(4)}
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Activity className="w-4 h-4 text-yellow-500"/>
                            <span className="text-sm text-muted-foreground">Avg Latency</span>
                        </div>
                        <div className="text-2xl font-bold mt-1">
                            {formatDuration(stats.avgLatency)}
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* GenAI Spans */}
            <div className="space-y-4">
                <h2 className="text-lg font-semibold">Recent GenAI Calls</h2>

                {mockGenAISpans.length === 0 ? (
                    <Card>
                        <CardContent className="py-12 text-center text-muted-foreground">
                            <Sparkles className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                            <p>No GenAI telemetry found</p>
                            <p className="text-sm">GenAI spans will appear as your AI calls are traced</p>
                        </CardContent>
                    </Card>
                ) : (
                    <div className="space-y-4">
                        {mockGenAISpans.map((span) => (
                            <GenAISpanCard
                                key={span.spanId}
                                span={span}
                                isExpanded={expandedSpans.has(span.spanId)}
                                onToggle={() => toggleSpan(span.spanId)}
                            />
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
