import {useState} from 'react';
import {Check, ChevronDown, ChevronRight, Copy, Code2, FileJson, Wrench} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible';
import {
    GEN_AI_TOOL_DEFINITIONS,
    GEN_AI_TOOL_NAME,
    GEN_AI_TOOL_CALL_ID,
    GEN_AI_TOOL_CALL_ARGUMENTS,
    GEN_AI_TOOL_CALL_RESULT,
    GEN_AI_TOOL_DESCRIPTION,
    GEN_AI_TOOL_TYPE,
} from '@/lib/semconv';

/**
 * Represents a single tool definition from gen_ai.tool.definitions
 */
interface ToolDefinition {
    type?: string;
    name?: string;
    function?: {
        name: string;
        description?: string;
        parameters?: Record<string, unknown>;
    };
    // Alternative format (some providers use this)
    description?: string;
    parameters?: Record<string, unknown>;
    input_schema?: Record<string, unknown>;
}

interface ToolDefinitionsViewerProps {
    /** Parsed span attributes */
    attributes: Record<string, unknown>;
    className?: string;
}

/**
 * Parse tool definitions from the gen_ai.tool.definitions attribute
 */
function parseToolDefinitions(definitions: unknown): ToolDefinition[] {
    if (!definitions) return [];

    // If already an array, return it
    if (Array.isArray(definitions)) {
        return definitions as ToolDefinition[];
    }

    // If it's a string, try to parse as JSON
    if (typeof definitions === 'string') {
        try {
            const parsed = JSON.parse(definitions);
            return Array.isArray(parsed) ? parsed : [parsed];
        } catch {
            return [];
        }
    }

    // If it's an object, wrap in array
    if (typeof definitions === 'object') {
        return [definitions as ToolDefinition];
    }

    return [];
}

/**
 * Parse JSON safely, returning the value if already parsed
 */
function parseJsonValue(value: unknown): unknown {
    if (typeof value === 'string') {
        try {
            return JSON.parse(value);
        } catch {
            return value;
        }
    }
    return value;
}

/**
 * Get the tool name from various formats
 */
function getToolName(tool: ToolDefinition): string {
    return tool.function?.name ?? tool.name ?? 'unnamed';
}

/**
 * Get the tool description from various formats
 */
function getToolDescription(tool: ToolDefinition): string | undefined {
    return tool.function?.description ?? tool.description;
}

/**
 * Get the tool schema (parameters/input_schema) from various formats
 */
function getToolSchema(tool: ToolDefinition): Record<string, unknown> | undefined {
    return tool.function?.parameters ?? tool.parameters ?? tool.input_schema;
}

interface JsonSchemaViewerProps {
    schema: Record<string, unknown>;
}

/**
 * Render a JSON schema in a readable format
 */
function JsonSchemaViewer({schema}: JsonSchemaViewerProps) {
    const [copiedSchema, setCopiedSchema] = useState(false);

    const copySchema = () => {
        navigator.clipboard.writeText(JSON.stringify(schema, null, 2));
        setCopiedSchema(true);
        setTimeout(() => setCopiedSchema(false), 2000);
    };

    return (
        <div className="relative group">
            <Button
                variant="ghost"
                size="icon"
                className="absolute top-2 right-2 h-6 w-6 opacity-0 group-hover:opacity-100 z-10"
                onClick={copySchema}
            >
                {copiedSchema ? (
                    <Check className="w-3 h-3 text-green-500" />
                ) : (
                    <Copy className="w-3 h-3" />
                )}
            </Button>
            <pre className="text-xs font-mono bg-muted/50 p-3 rounded-lg overflow-auto max-h-64 whitespace-pre-wrap">
                {JSON.stringify(schema, null, 2)}
            </pre>
        </div>
    );
}

interface SingleToolViewerProps {
    tool: ToolDefinition;
    defaultOpen?: boolean;
}

/**
 * Display a single tool definition with collapsible schema
 */
function SingleToolViewer({tool, defaultOpen = false}: SingleToolViewerProps) {
    const [isOpen, setIsOpen] = useState(defaultOpen);
    const name = getToolName(tool);
    const description = getToolDescription(tool);
    const schema = getToolSchema(tool);
    const toolType = tool.type ?? 'function';

    return (
        <Collapsible open={isOpen} onOpenChange={setIsOpen}>
            <CollapsibleTrigger className="flex items-center gap-2 w-full p-3 text-left hover:bg-muted/50 rounded-lg transition-colors">
                {isOpen ? (
                    <ChevronDown className="w-4 h-4 text-muted-foreground shrink-0" />
                ) : (
                    <ChevronRight className="w-4 h-4 text-muted-foreground shrink-0" />
                )}
                <Wrench className="w-4 h-4 text-primary shrink-0" />
                <span className="font-medium text-sm">{name}</span>
                <Badge variant="outline" className="text-xs ml-auto">
                    {toolType}
                </Badge>
            </CollapsibleTrigger>
            <CollapsibleContent>
                <div className="ml-10 pb-3 space-y-3">
                    {description && (
                        <p className="text-sm text-muted-foreground">{description}</p>
                    )}
                    {schema && (
                        <div className="space-y-2">
                            <div className="flex items-center gap-2">
                                <FileJson className="w-4 h-4 text-muted-foreground" />
                                <span className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                                    Parameters Schema
                                </span>
                            </div>
                            <JsonSchemaViewer schema={schema} />
                        </div>
                    )}
                </div>
            </CollapsibleContent>
        </Collapsible>
    );
}

interface ToolCallViewerProps {
    toolName: string;
    toolCallId?: string;
    toolType?: string;
    description?: string;
    arguments?: unknown;
    result?: unknown;
}

/**
 * Display a tool call with its arguments and result
 */
export function ToolCallViewer({
    toolName,
    toolCallId,
    toolType,
    description,
    arguments: toolArgs,
    result,
}: ToolCallViewerProps) {
    const [showArgs, setShowArgs] = useState(true);
    const [showResult, setShowResult] = useState(true);
    const [copiedArgs, setCopiedArgs] = useState(false);
    const [copiedResult, setCopiedResult] = useState(false);

    const parsedArgs = parseJsonValue(toolArgs);
    const parsedResult = parseJsonValue(result);

    const copyToClipboard = (value: unknown, setCopied: (v: boolean) => void) => {
        const text = typeof value === 'string' ? value : JSON.stringify(value, null, 2);
        navigator.clipboard.writeText(text);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    return (
        <div className="space-y-4">
            {/* Tool Call Header */}
            <div className="flex items-center gap-3 p-3 bg-muted rounded-lg">
                <Wrench className="w-5 h-5 text-primary" />
                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-medium">{toolName}</span>
                        {toolType && (
                            <Badge variant="outline" className="text-xs">
                                {toolType}
                            </Badge>
                        )}
                    </div>
                    {toolCallId && (
                        <div className="text-xs text-muted-foreground font-mono truncate">
                            ID: {toolCallId}
                        </div>
                    )}
                </div>
            </div>

            {description && (
                <p className="text-sm text-muted-foreground">{String(description)}</p>
            )}

            {/* Arguments */}
            {parsedArgs != null && (
                <Collapsible open={showArgs} onOpenChange={setShowArgs}>
                    <CollapsibleTrigger className="flex items-center gap-2 w-full text-left">
                        {showArgs ? (
                            <ChevronDown className="w-4 h-4 text-muted-foreground" />
                        ) : (
                            <ChevronRight className="w-4 h-4 text-muted-foreground" />
                        )}
                        <Code2 className="w-4 h-4 text-cyan-500" />
                        <span className="text-sm font-medium">Arguments</span>
                    </CollapsibleTrigger>
                    <CollapsibleContent>
                        <div className="relative group mt-2">
                            <Button
                                variant="ghost"
                                size="icon"
                                className="absolute top-2 right-2 h-6 w-6 opacity-0 group-hover:opacity-100 z-10"
                                onClick={() => copyToClipboard(parsedArgs, setCopiedArgs)}
                            >
                                {copiedArgs ? (
                                    <Check className="w-3 h-3 text-green-500" />
                                ) : (
                                    <Copy className="w-3 h-3" />
                                )}
                            </Button>
                            <pre className="text-xs font-mono bg-muted/50 p-3 rounded-lg overflow-auto max-h-48 whitespace-pre-wrap">
                                {typeof parsedArgs === 'string'
                                    ? parsedArgs
                                    : JSON.stringify(parsedArgs, null, 2)}
                            </pre>
                        </div>
                    </CollapsibleContent>
                </Collapsible>
            )}

            {/* Result */}
            {parsedResult != null && (
                <Collapsible open={showResult} onOpenChange={setShowResult}>
                    <CollapsibleTrigger className="flex items-center gap-2 w-full text-left">
                        {showResult ? (
                            <ChevronDown className="w-4 h-4 text-muted-foreground" />
                        ) : (
                            <ChevronRight className="w-4 h-4 text-muted-foreground" />
                        )}
                        <Check className="w-4 h-4 text-green-500" />
                        <span className="text-sm font-medium">Result</span>
                    </CollapsibleTrigger>
                    <CollapsibleContent>
                        <div className="relative group mt-2">
                            <Button
                                variant="ghost"
                                size="icon"
                                className="absolute top-2 right-2 h-6 w-6 opacity-0 group-hover:opacity-100 z-10"
                                onClick={() => copyToClipboard(parsedResult, setCopiedResult)}
                            >
                                {copiedResult ? (
                                    <Check className="w-3 h-3 text-green-500" />
                                ) : (
                                    <Copy className="w-3 h-3" />
                                )}
                            </Button>
                            <pre className="text-xs font-mono bg-muted/50 p-3 rounded-lg overflow-auto max-h-48 whitespace-pre-wrap">
                                {typeof parsedResult === 'string'
                                    ? parsedResult
                                    : JSON.stringify(parsedResult, null, 2)}
                            </pre>
                        </div>
                    </CollapsibleContent>
                </Collapsible>
            )}
        </div>
    );
}

/**
 * Display tool definitions from a GenAI span
 *
 * This component renders the gen_ai.tool.definitions attribute which contains
 * the list of tools available to the AI model. Each tool is displayed with
 * its name, description, and JSON schema in collapsible sections.
 */
export function ToolDefinitionsViewer({attributes, className}: ToolDefinitionsViewerProps) {
    const toolDefinitions = parseToolDefinitions(attributes[GEN_AI_TOOL_DEFINITIONS]);

    if (toolDefinitions.length === 0) {
        return null;
    }

    return (
        <div className={cn('space-y-2', className)}>
            <div className="flex items-center gap-2 mb-3">
                <Wrench className="w-4 h-4 text-muted-foreground" />
                <span className="text-sm font-medium text-muted-foreground uppercase tracking-wide">
                    Available Tools ({toolDefinitions.length})
                </span>
            </div>
            <div className="space-y-1 border rounded-lg p-2">
                {toolDefinitions.map((tool, index) => (
                    <SingleToolViewer
                        key={`${getToolName(tool)}-${index}`}
                        tool={tool}
                        defaultOpen={toolDefinitions.length === 1}
                    />
                ))}
            </div>
        </div>
    );
}

/**
 * Extract tool call information from span attributes
 */
export function extractToolCallInfo(attributes: Record<string, unknown>): {
    hasToolCall: boolean;
    toolName?: string;
    toolCallId?: string;
    toolType?: string;
    description?: string;
    arguments?: unknown;
    result?: unknown;
} {
    const toolName = attributes[GEN_AI_TOOL_NAME] as string | undefined;

    if (!toolName) {
        return {hasToolCall: false};
    }

    return {
        hasToolCall: true,
        toolName,
        toolCallId: attributes[GEN_AI_TOOL_CALL_ID] as string | undefined,
        toolType: attributes[GEN_AI_TOOL_TYPE] as string | undefined,
        description: attributes[GEN_AI_TOOL_DESCRIPTION] as string | undefined,
        arguments: attributes[GEN_AI_TOOL_CALL_ARGUMENTS],
        result: attributes[GEN_AI_TOOL_CALL_RESULT],
    };
}

/**
 * Check if span has tool definitions
 */
export function hasToolDefinitions(attributes: Record<string, unknown>): boolean {
    const definitions = attributes[GEN_AI_TOOL_DEFINITIONS];
    if (!definitions) return false;
    const parsed = parseToolDefinitions(definitions);
    return parsed.length > 0;
}

export default ToolDefinitionsViewer;
