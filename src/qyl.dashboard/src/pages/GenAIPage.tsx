import {useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {
    Activity,
    AlertCircle,
    ChevronDown,
    ChevronRight,
    Cpu,
    DollarSign,
    FileJson,
    Loader2,
    MessageSquare,
    Sparkles,
    Wrench,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Separator} from '@/components/ui/separator';
import {CopyableText, DownloadButton, TextVisualizer} from '@/components/ui';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import {extractToolCallInfo, hasToolDefinitions, ToolCallViewer, ToolDefinitionsViewer,} from '@/components/genai';

// API response types
interface GenAiStats {
    requestCount: number;
    totalInputTokens: number;
    totalOutputTokens: number;
    totalCostUsd: number;
    averageEvalScore?: number;
}

interface GenAiSpan {
    spanId: string;
    traceId: string;
    name: string;
    kind: number;
    startTimeUnixNano: number;
    endTimeUnixNano: number;
    durationNs: number;
    statusCode: number;
    statusMessage?: string;
    serviceName?: string;
    genAiProviderName?: string;
    genAiRequestModel?: string;
    genAiResponseModel?: string;
    genAiInputTokens?: number;
    genAiOutputTokens?: number;
    genAiTemperature?: number;
    genAiStopReason?: string;
    genAiToolName?: string;
    genAiToolCallId?: string;
    genAiCostUsd?: number;
    attributesJson?: string;
}

interface GenAiSpansResponse {
    spans: GenAiSpan[];
    total: number;
}

// Fetch functions
async function fetchGenAiStats(): Promise<GenAiStats> {
    const res = await fetch('/api/v1/genai/stats');
    if (!res.ok) throw new Error('Failed to fetch GenAI stats');
    return res.json();
}

async function fetchGenAiSpans(limit = 50): Promise<GenAiSpansResponse> {
    const res = await fetch(`/api/v1/genai/spans?limit=${limit}`);
    if (!res.ok) throw new Error('Failed to fetch GenAI spans');
    return res.json();
}

function GenAISpanCard({
                           span,
                           isExpanded,
                           onToggle,
                       }: {
    span: GenAiSpan;
    isExpanded: boolean;
    onToggle: () => void;
}) {
    const totalTokens = (span.genAiInputTokens ?? 0) + (span.genAiOutputTokens ?? 0);
    const durationMs = nsToMs(span.durationNs);

    const providerColor =
        span.genAiProviderName === 'openai'
            ? 'text-green-500 border-green-500'
            : span.genAiProviderName === 'anthropic'
                ? 'text-orange-500 border-orange-500'
                : span.genAiProviderName === 'gcp.gemini'
                    ? 'text-blue-500 border-blue-500'
                    : 'text-violet-500 border-violet-500';

    // Parse attributes if available
    let parsedAttrs: Record<string, unknown> = {};
    if (span.attributesJson) {
        try {
            parsedAttrs = JSON.parse(span.attributesJson);
        } catch {
            // Ignore parse errors
        }
    }

    // Check for tool-related data from parsed attributes
    const toolCallInfo = extractToolCallInfo(parsedAttrs);
    const showToolDefinitions = hasToolDefinitions(parsedAttrs);
    const hasToolData = toolCallInfo.hasToolCall || showToolDefinitions;

    return (
        <Card className={cn('transition-all', isExpanded && 'ring-1 ring-primary/50')}>
            <CardHeader className="cursor-pointer hover:bg-muted/50" onClick={onToggle}>
                <div className="flex items-start gap-4">
                    {isExpanded ? (
                        <ChevronDown className="w-5 h-5 mt-0.5 text-muted-foreground"/>
                    ) : (
                        <ChevronRight className="w-5 h-5 mt-0.5 text-muted-foreground"/>
                    )}

                    <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                            <Badge variant="outline" className={providerColor}>
                                {span.genAiProviderName ?? 'unknown'}
                            </Badge>
                            <Badge variant="secondary">
                                {span.genAiResponseModel ?? span.genAiRequestModel ?? 'unknown'}
                            </Badge>
                            {hasToolData && (
                                <Badge variant="outline" className="text-cyan-500 border-cyan-500">
                                    <Wrench className="w-3 h-3 mr-1"/>
                                    Tools
                                </Badge>
                            )}
                            <span className="text-sm text-muted-foreground">
                                {span.serviceName ?? 'unknown'}
                            </span>
                        </div>
                        <p className="text-sm text-muted-foreground mt-1 truncate">{span.name}</p>
                    </div>

                    <div className="flex items-center gap-6 text-sm">
                        <div className="text-right">
                            <div className="text-muted-foreground">Tokens</div>
                            <div className="font-mono">
                                {span.genAiInputTokens?.toLocaleString() ?? '-'} /{' '}
                                {span.genAiOutputTokens?.toLocaleString() ?? '-'}
                            </div>
                        </div>
                        <div className="text-right">
                            <div className="text-muted-foreground">Duration</div>
                            <div className="font-mono">{formatDuration(durationMs)}</div>
                        </div>
                        <div className="text-right">
                            <div className="text-muted-foreground">Cost</div>
                            <div className="font-mono text-green-500">
                                ${span.genAiCostUsd?.toFixed(4) ?? '0.0000'}
                            </div>
                        </div>
                    </div>
                </div>
            </CardHeader>

            {isExpanded && (
                <CardContent className="pt-0">
                    <Separator className="mb-4"/>

                    <Tabs defaultValue="details" className="w-full">
                        <TabsList>
                            <TabsTrigger value="details">
                                <Cpu className="w-4 h-4 mr-1"/>
                                Details
                            </TabsTrigger>
                            {hasToolData && (
                                <TabsTrigger value="tools">
                                    <Wrench className="w-4 h-4 mr-1"/>
                                    Tools
                                </TabsTrigger>
                            )}
                            {Object.keys(parsedAttrs).length > 0 && (
                                <TabsTrigger value="attributes">
                                    <MessageSquare className="w-4 h-4 mr-1"/>
                                    Attributes
                                </TabsTrigger>
                            )}
                        </TabsList>

                        <TabsContent value="details" className="mt-4">
                            <div className="grid grid-cols-2 gap-4 text-sm">
                                <div>
                                    <span className="text-muted-foreground">Provider:</span>
                                    <span className="ml-2">{span.genAiProviderName ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Request Model:</span>
                                    <span className="ml-2">{span.genAiRequestModel ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Response Model:</span>
                                    <span className="ml-2">{span.genAiResponseModel ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Temperature:</span>
                                    <span className="ml-2">{span.genAiTemperature ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Input Tokens:</span>
                                    <span className="ml-2 font-mono">
                                        {span.genAiInputTokens?.toLocaleString() ?? '-'}
                                    </span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Output Tokens:</span>
                                    <span className="ml-2 font-mono">
                                        {span.genAiOutputTokens?.toLocaleString() ?? '-'}
                                    </span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Total Tokens:</span>
                                    <span className="ml-2 font-mono">{totalTokens.toLocaleString()}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Stop Reason:</span>
                                    <span className="ml-2">{span.genAiStopReason ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Duration:</span>
                                    <span className="ml-2 font-mono">{formatDuration(durationMs)}</span>
                                </div>
                                <div>
                                    <span className="text-muted-foreground">Cost:</span>
                                    <span className="ml-2 font-mono text-green-500">
                                        ${span.genAiCostUsd?.toFixed(6) ?? '0.000000'}
                                    </span>
                                </div>
                                <div className="flex items-center">
                                    <span className="text-muted-foreground">Trace ID:</span>
                                    <CopyableText
                                        value={span.traceId}
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
                                        value={span.spanId}
                                        label="Span ID"
                                        className="ml-2"
                                        truncate
                                        maxWidth="120px"
                                    />
                                </div>
                            </div>
                        </TabsContent>

                        {hasToolData && (
                            <TabsContent value="tools" className="mt-4 space-y-6">
                                {/* Tool Definitions Section */}
                                {showToolDefinitions && (
                                    <ToolDefinitionsViewer attributes={parsedAttrs}/>
                                )}

                                {/* Tool Call Section */}
                                {toolCallInfo.hasToolCall && (
                                    <div className="space-y-2">
                                        <div className="flex items-center gap-2 mb-3">
                                            <FileJson className="w-4 h-4 text-muted-foreground"/>
                                            <span
                                                className="text-sm font-medium text-muted-foreground uppercase tracking-wide">
                                                Tool Call
                                            </span>
                                        </div>
                                        <ToolCallViewer
                                            toolName={toolCallInfo.toolName!}
                                            toolCallId={toolCallInfo.toolCallId}
                                            toolType={toolCallInfo.toolType}
                                            description={toolCallInfo.description}
                                            arguments={toolCallInfo.arguments}
                                            result={toolCallInfo.result}
                                        />
                                    </div>
                                )}
                            </TabsContent>
                        )}

                        {Object.keys(parsedAttrs).length > 0 && (
                            <TabsContent value="attributes" className="mt-4">
                                <TextVisualizer
                                    content={JSON.stringify(parsedAttrs)}
                                    label="Attributes"
                                    defaultExpanded={false}
                                    maxCollapsedHeight={160}
                                    showTreeView={true}
                                />
                            </TabsContent>
                        )}
                    </Tabs>
                </CardContent>
            )}
        </Card>
    );
}

export function GenAIPage() {
    const [expandedSpans, setExpandedSpans] = useState<Set<string>>(new Set());

    const {
        data: stats,
        isLoading: statsLoading,
        error: statsError,
    } = useQuery({
        queryKey: ['genai-stats'],
        queryFn: fetchGenAiStats,
        staleTime: 30_000,
    });

    const {
        data: spansData,
        isLoading: spansLoading,
        error: spansError,
    } = useQuery({
        queryKey: ['genai-spans'],
        queryFn: () => fetchGenAiSpans(50),
        staleTime: 30_000,
    });

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

    const isLoading = statsLoading || spansLoading;
    const error = statsError || spansError;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-destructive"/>
                        <p className="text-destructive">Failed to load GenAI telemetry</p>
                        <p className="text-sm text-muted-foreground mt-2">
                            {error instanceof Error ? error.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

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
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1">
                                {stats?.requestCount.toLocaleString() ?? 0}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Cpu className="w-4 h-4 text-cyan-500"/>
                            <span className="text-sm text-muted-foreground">Total Tokens</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1">
                                {((stats?.totalInputTokens ?? 0) + (stats?.totalOutputTokens ?? 0)).toLocaleString()}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <DollarSign className="w-4 h-4 text-green-500"/>
                            <span className="text-sm text-muted-foreground">Total Cost</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-green-500">
                                ${stats?.totalCostUsd?.toFixed(4) ?? '0.0000'}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Activity className="w-4 h-4 text-yellow-500"/>
                            <span className="text-sm text-muted-foreground">Input/Output</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1">
                                {(stats?.totalInputTokens ?? 0).toLocaleString()} /{' '}
                                {(stats?.totalOutputTokens ?? 0).toLocaleString()}
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* GenAI Spans */}
            <div className="space-y-4">
                <div className="flex items-center justify-between">
                    <h2 className="text-lg font-semibold">Recent GenAI Calls</h2>
                    <DownloadButton
                        getData={() => (spansData?.spans ?? []).map(span => ({
                            spanId: span.spanId,
                            traceId: span.traceId,
                            name: span.name,
                            provider: span.genAiProviderName ?? '',
                            requestModel: span.genAiRequestModel ?? '',
                            responseModel: span.genAiResponseModel ?? '',
                            inputTokens: span.genAiInputTokens ?? 0,
                            outputTokens: span.genAiOutputTokens ?? 0,
                            totalTokens: (span.genAiInputTokens ?? 0) + (span.genAiOutputTokens ?? 0),
                            temperature: span.genAiTemperature ?? '',
                            stopReason: span.genAiStopReason ?? '',
                            toolName: span.genAiToolName ?? '',
                            toolCallId: span.genAiToolCallId ?? '',
                            costUsd: span.genAiCostUsd ?? 0,
                            durationMs: nsToMs(span.durationNs),
                            serviceName: span.serviceName ?? '',
                            statusCode: span.statusCode,
                            statusMessage: span.statusMessage ?? '',
                        }))}
                        filenamePrefix="genai-spans"
                        columns={[
                            'spanId', 'traceId', 'name', 'provider', 'requestModel', 'responseModel',
                            'inputTokens', 'outputTokens', 'totalTokens', 'temperature', 'stopReason',
                            'toolName', 'toolCallId', 'costUsd', 'durationMs', 'serviceName',
                            'statusCode', 'statusMessage'
                        ]}
                        disabled={!spansData?.spans?.length}
                    />
                </div>

                {isLoading ? (
                    <Card>
                        <CardContent className="py-12 text-center">
                            <Loader2 className="w-12 h-12 mx-auto mb-4 animate-spin text-muted-foreground"/>
                            <p className="text-muted-foreground">Loading GenAI telemetry...</p>
                        </CardContent>
                    </Card>
                ) : !spansData?.spans?.length ? (
                    <Card>
                        <CardContent className="py-12 text-center text-muted-foreground">
                            <Sparkles className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                            <p>No GenAI telemetry found</p>
                            <p className="text-sm">GenAI spans will appear as your AI calls are traced</p>
                        </CardContent>
                    </Card>
                ) : (
                    <div className="space-y-4">
                        {spansData.spans.map((span) => (
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
