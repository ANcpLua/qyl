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
import {CopyableText} from '@/components/ui';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import type {SpanStatusCode} from '@/types';

// Alias for mock data - StatusCode uses the same values as SpanStatusCode
type StatusCode = SpanStatusCode;

// Helper to get total tokens from mock span (uses convenience accessors)
function getMockTotalTokens(span: MockGenAiSpan): number | null {
    const input = span.gen_ai_input_tokens;
    const output = span.gen_ai_output_tokens;
    if (input === undefined && output === undefined) return null;
    return (input ?? 0) + (output ?? 0);
}

// Helper to get duration from mock span
function getMockDurationNs(span: MockGenAiSpan): number {
    return span.end_time_unix_nano - span.start_time_unix_nano;
}

// Helper to get service name from resource
function getMockServiceName(span: MockGenAiSpan): string {
    const attr = span.resource.attributes.find(a => a.key === 'service.name');
    return (attr?.value as string) ?? 'unknown';
}

// Extended span data for GenAI display (messages are stored in attributes in real data)
interface GenAiExtendedData {
    inputMessages?: Array<{ role: string; content?: string }>;
    outputMessages?: Array<{ role: string; content?: string }>;
    toolCalls?: Array<{ id: string; type: string; function: { name: string; arguments: string } }>;
    finishReasons?: string[];
    operationName?: string;
}

// Mock GenAI span interface for display (flattened for convenience)
interface MockGenAiSpan {
    trace_id: string;
    span_id: string;
    name: string;
    kind: number;
    start_time_unix_nano: number;
    end_time_unix_nano: number;
    status: { code: StatusCode; message?: string };
    attributes: Array<{ key: string; value: unknown }>;
    resource: { attributes: Array<{ key: string; value: unknown }> };
    // Convenience accessors (would be derived from attributes in real code)
    gen_ai_system?: string;
    gen_ai_request_model?: string;
    gen_ai_response_model?: string;
    gen_ai_input_tokens?: number;
    gen_ai_output_tokens?: number;
    gen_ai_cost_usd?: number;
    gen_ai_extended?: GenAiExtendedData;
}

// Helper to create attribute array from object
function makeAttrs(obj: Record<string, unknown>): Array<{ key: string; value: unknown }> {
    return Object.entries(obj).map(([key, value]) => ({ key, value }));
}

// Mock GenAI spans - using snake_case properties matching OTel schema
const mockGenAISpans: MockGenAiSpan[] = [
    {
        trace_id: 'trace-1',
        span_id: 'span-1',
        name: 'chat openai.chat',
        kind: 3, // CLIENT
        status: { code: 1 },
        start_time_unix_nano: Date.now() * 1_000_000 - 5_000_000_000,
        end_time_unix_nano: Date.now() * 1_000_000 - 2_000_000_000,
        attributes: makeAttrs({
            'gen_ai.system': 'openai',
            'gen_ai.request.model': 'gpt-4o',
            'gen_ai.response.model': 'gpt-4o-2024-08-06',
            'gen_ai.usage.input_tokens': 1250,
            'gen_ai.usage.output_tokens': 450,
        }),
        resource: { attributes: makeAttrs({ 'service.name': 'chat-service' }) },
        // Convenience accessors
        gen_ai_system: 'openai',
        gen_ai_request_model: 'gpt-4o',
        gen_ai_response_model: 'gpt-4o-2024-08-06',
        gen_ai_input_tokens: 1250,
        gen_ai_output_tokens: 450,
        gen_ai_cost_usd: 0.0425,
        gen_ai_extended: {
            operationName: 'chat',
            finishReasons: ['stop'],
            inputMessages: [
                {role: 'system', content: 'You are a helpful assistant.'},
                {role: 'user', content: 'What is the capital of France?'},
            ],
            outputMessages: [
                {
                    role: 'assistant',
                    content:
                        'The capital of France is Paris. It is known as the "City of Light" and is famous for landmarks like the Eiffel Tower, the Louvre Museum, and Notre-Dame Cathedral.',
                },
            ],
        },
    },
    {
        trace_id: 'trace-2',
        span_id: 'span-2',
        name: 'chat anthropic.messages',
        kind: 3, // CLIENT
        status: { code: 1 },
        start_time_unix_nano: Date.now() * 1_000_000 - 10_000_000_000,
        end_time_unix_nano: Date.now() * 1_000_000 - 6_000_000_000,
        attributes: makeAttrs({
            'gen_ai.system': 'anthropic',
            'gen_ai.request.model': 'claude-3-5-sonnet-20241022',
            'gen_ai.response.model': 'claude-3-5-sonnet-20241022',
            'gen_ai.usage.input_tokens': 2100,
            'gen_ai.usage.output_tokens': 850,
        }),
        resource: { attributes: makeAttrs({ 'service.name': 'agent-service' }) },
        // Convenience accessors
        gen_ai_system: 'anthropic',
        gen_ai_request_model: 'claude-3-5-sonnet-20241022',
        gen_ai_response_model: 'claude-3-5-sonnet-20241022',
        gen_ai_input_tokens: 2100,
        gen_ai_output_tokens: 850,
        gen_ai_cost_usd: 0.0885,
        gen_ai_extended: {
            operationName: 'chat',
            finishReasons: ['end_turn'],
            inputMessages: [
                {
                    role: 'user',
                    content: 'Help me write a Python function to calculate fibonacci numbers.',
                },
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
                        arguments:
                            '{"code": "def fibonacci(n):\\n    if n <= 1:\\n        return n\\n    return fibonacci(n-1) + fibonacci(n-2)"}',
                    },
                },
            ],
        },
    },
];

interface GenAISpanCardProps {
    span: MockGenAiSpan;
    isExpanded: boolean;
    onToggle: () => void;
}

function GenAISpanCard({span, isExpanded, onToggle}: GenAISpanCardProps) {
    const [copiedMessage, setCopiedMessage] = useState<string | null>(null);
    const totalTokens = getMockTotalTokens(span);
    const durationMs = nsToMs(getMockDurationNs(span));
    const ext = span.gen_ai_extended;

    const copyToClipboard = (text: string, id: string) => {
        navigator.clipboard.writeText(text);
        setCopiedMessage(id);
        setTimeout(() => setCopiedMessage(null), 2000);
    };

    const providerColor =
        span.gen_ai_system === 'openai'
            ? 'text-green-500 border-green-500'
            : span.gen_ai_system === 'anthropic'
                ? 'text-orange-500 border-orange-500'
                : 'text-violet-500 border-violet-500';

    return (
        <Card className={cn('transition-all', isExpanded && 'ring-1 ring-primary/50')}>
            <CardHeader className="cursor-pointer hover:bg-muted/50" onClick={onToggle}>
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
                                {span.gen_ai_system}
                            </Badge>
                            <Badge variant="secondary">{span.gen_ai_request_model}</Badge>
                            <span className="text-sm text-muted-foreground">{getMockServiceName(span)}</span>
                        </div>
                        <p className="text-sm text-muted-foreground mt-1 truncate">
                            {ext?.inputMessages?.[ext.inputMessages.length - 1]?.content?.slice(0, 100)}...
                        </p>
                    </div>

                    {/* Stats */}
                    <div className="flex items-center gap-6 text-sm">
                        <div className="text-right">
                            <div className="text-muted-foreground">Tokens</div>
                            <div className="font-mono">
                                {span.gen_ai_input_tokens?.toLocaleString()} / {span.gen_ai_output_tokens?.toLocaleString()}
                            </div>
                        </div>
                        <div className="text-right">
                            <div className="text-muted-foreground">Duration</div>
                            <div className="font-mono">{formatDuration(durationMs)}</div>
                        </div>
                        <div className="text-right">
                            <div className="text-muted-foreground">Cost</div>
                            <div className="font-mono text-green-500">${span.gen_ai_cost_usd?.toFixed(4)}</div>
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
                            {ext?.toolCalls && ext.toolCalls.length > 0 && (
                                <TabsTrigger value="tools">
                                    <Wrench className="w-4 h-4 mr-1"/>
                                    Tools ({ext.toolCalls.length})
                                </TabsTrigger>
                            )}
                            <TabsTrigger value="details">
                                <Cpu className="w-4 h-4 mr-1"/>
                                Details
                            </TabsTrigger>
                        </TabsList>

                        <TabsContent value="messages" className="mt-4 space-y-4">
                            {/* Input messages */}
                            {ext?.inputMessages?.map((msg, i) => (
                                <div key={`input-${i}`} className="relative group">
                                    <div
                                        className={cn(
                                            'p-3 rounded-lg',
                                            msg.role === 'system' && 'bg-yellow-500/10 border border-yellow-500/20',
                                            msg.role === 'user' && 'bg-blue-500/10 border border-blue-500/20',
                                            msg.role === 'assistant' && 'bg-violet-500/10 border border-violet-500/20'
                                        )}
                                    >
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
                                        <pre className="text-sm whitespace-pre-wrap font-mono">{msg.content}</pre>
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
                            {ext?.outputMessages?.map((msg, i) => (
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
                                        <pre className="text-sm whitespace-pre-wrap font-mono">{msg.content}</pre>
                                    </div>
                                </div>
                            ))}
                        </TabsContent>

                        <TabsContent value="tools" className="mt-4 space-y-4">
                            {ext?.toolCalls?.map((tool, i) => (
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
                                    <span className="ml-2">{span.gen_ai_system}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Request Model:</span>
                                    <span className="ml-2">{span.gen_ai_request_model}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Response Model:</span>
                                    <span className="ml-2">{span.gen_ai_response_model}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Operation:</span>
                                    <span className="ml-2">{ext?.operationName}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Input Tokens:</span>
                                    <span className="ml-2 font-mono">{span.gen_ai_input_tokens?.toLocaleString()}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Output Tokens:</span>
                                    <span className="ml-2 font-mono">{span.gen_ai_output_tokens?.toLocaleString()}</span>
                                </div>
                                {totalTokens && (
                                    <div>
                                        <span className="text-muted-foreground">Total Tokens:</span>
                                        <span className="ml-2 font-mono">{totalTokens.toLocaleString()}</span>
                                    </div>
                                )}
                                <div>
                                    <span className="text-muted-foreground">Finish Reason:</span>
                                    <span className="ml-2">{ext?.finishReasons?.join(', ')}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Duration:</span>
                                    <span className="ml-2 font-mono">{formatDuration(durationMs)}</span>
                                </div>
                                <div className="flex items-center">
                                    <span className="text-muted-foreground">Trace ID:</span>
                                    <CopyableText
                                        value={span.trace_id}
                                        label="Trace ID"
                                        className="ml-2"
                                        textClassName="text-primary"
                                        truncate
                                        maxWidth="120px"
                                    />
                                </div>
                                <div className="flex items-center">
                                    <span className="text-muted-foreground">Span ID:</span>
                                    <CopyableText
                                        value={span.span_id}
                                        label="Span ID"
                                        className="ml-2"
                                        truncate
                                        maxWidth="120px"
                                    />
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
            totalTokens: mockGenAISpans.reduce((a, s) => a + (getMockTotalTokens(s) || 0), 0),
            totalCost: mockGenAISpans.reduce((a, s) => a + (s.gen_ai_cost_usd || 0), 0),
            avgLatency:
                mockGenAISpans.reduce((a, s) => a + nsToMs(getMockDurationNs(s)), 0) / mockGenAISpans.length,
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
                        <div className="text-2xl font-bold mt-1">{stats.totalTokens.toLocaleString()}</div>
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
                        <div className="text-2xl font-bold mt-1">{formatDuration(stats.avgLatency)}</div>
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
                                key={span.span_id}
                                span={span}
                                isExpanded={expandedSpans.has(span.span_id)}
                                onToggle={() => toggleSpan(span.span_id)}
                            />
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
