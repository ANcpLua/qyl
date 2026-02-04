import * as React from "react";
import {useState, useMemo, useCallback} from "react";
import {ChevronDown, ChevronRight, Copy, Check, Code2, FileJson, FileCode, Maximize2, Minimize2} from "lucide-react";
import {Button} from "./button";
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from "./collapsible";
import {Tooltip, TooltipContent, TooltipTrigger} from "./tooltip";
import {cn} from "@/lib/utils";
import {toast} from "sonner";

// Content type detection
type ContentType = "json" | "xml" | "text";

function detectContentType(content: string): ContentType {
    const trimmed = content.trim();
    if (trimmed.startsWith("{") || trimmed.startsWith("[")) {
        try {
            JSON.parse(trimmed);
            return "json";
        } catch {
            return "text";
        }
    }
    if (trimmed.startsWith("<") && trimmed.includes(">")) {
        return "xml";
    }
    return "text";
}

// Format content based on type
function formatContent(content: string, type: ContentType): string {
    if (type === "json") {
        try {
            return JSON.stringify(JSON.parse(content), null, 2);
        } catch {
            return content;
        }
    }
    if (type === "xml") {
        return formatXml(content);
    }
    return content;
}

// Simple XML formatter
function formatXml(xml: string): string {
    let formatted = "";
    let indent = 0;
    const tokens = xml.replace(/>\s*</g, ">\n<").split("\n");

    for (const token of tokens) {
        const trimmed = token.trim();
        if (!trimmed) continue;

        // Decrease indent for closing tags
        if (trimmed.startsWith("</")) {
            indent = Math.max(0, indent - 1);
        }

        formatted += "  ".repeat(indent) + trimmed + "\n";

        // Increase indent for opening tags (not self-closing or closing)
        if (
            trimmed.startsWith("<") &&
            !trimmed.startsWith("</") &&
            !trimmed.startsWith("<?") &&
            !trimmed.startsWith("<!") &&
            !trimmed.endsWith("/>") &&
            !trimmed.includes("</")
        ) {
            indent++;
        }
    }

    return formatted.trim();
}

// JSON Tree View Component
interface JsonTreeNodeProps {
    name: string;
    value: unknown;
    depth: number;
    isLast: boolean;
}

function JsonTreeNode({name, value, depth, isLast}: JsonTreeNodeProps) {
    const [isOpen, setIsOpen] = useState(depth < 2);

    const isObject = value !== null && typeof value === "object";
    const isArray = Array.isArray(value);
    const isEmpty = isObject && Object.keys(value as object).length === 0;

    const renderValue = () => {
        if (value === null) return <span className="text-signal-orange">null</span>;
        if (typeof value === "boolean") return <span className="text-signal-violet">{String(value)}</span>;
        if (typeof value === "number") return <span className="text-signal-cyan">{value}</span>;
        if (typeof value === "string") return <span className="text-signal-green">"{value}"</span>;
        return null;
    };

    if (!isObject) {
        return (
            <div className="flex items-baseline" style={{paddingLeft: depth * 16}}>
                <span className="text-brutal-slate">{name}:</span>
                <span className="ml-2">{renderValue()}</span>
                {!isLast && <span className="text-brutal-zinc">,</span>}
            </div>
        );
    }

    const entries = Object.entries(value as object);
    const bracket = isArray ? ["[", "]"] : ["{", "}"];

    return (
        <div style={{paddingLeft: depth * 16}}>
            <button
                className="flex items-center gap-1 hover:bg-brutal-zinc/30 -ml-4 pl-4 pr-2 py-0.5 w-full text-left"
                onClick={() => setIsOpen(!isOpen)}
            >
                {isEmpty ? (
                    <span className="w-4"/>
                ) : isOpen ? (
                    <ChevronDown className="w-3 h-3 text-brutal-slate"/>
                ) : (
                    <ChevronRight className="w-3 h-3 text-brutal-slate"/>
                )}
                <span className="text-brutal-slate">{name}:</span>
                <span className="text-brutal-zinc ml-1">
                    {bracket[0]}
                    {!isOpen && !isEmpty && (
                        <span className="text-brutal-slate mx-1">
                            {isArray ? `${entries.length} items` : `${entries.length} keys`}
                        </span>
                    )}
                    {(!isOpen || isEmpty) && bracket[1]}
                </span>
            </button>
            {isOpen && !isEmpty && (
                <>
                    {entries.map(([key, val], idx) => (
                        <JsonTreeNode
                            key={key}
                            name={isArray ? String(idx) : key}
                            value={val}
                            depth={depth + 1}
                            isLast={idx === entries.length - 1}
                        />
                    ))}
                    <div style={{paddingLeft: depth * 16}} className="text-brutal-zinc">
                        {bracket[1]}{!isLast && ","}
                    </div>
                </>
            )}
        </div>
    );
}

// Syntax highlighting for formatted content
function SyntaxHighlighter({content, type}: {content: string; type: ContentType}) {
    const highlighted = useMemo(() => {
        if (type === "json") {
            return highlightJson(content);
        }
        if (type === "xml") {
            return highlightXml(content);
        }
        return content;
    }, [content, type]);

    return (
        <pre className="text-sm font-mono whitespace-pre-wrap overflow-x-auto">
            {typeof highlighted === "string" ? (
                highlighted
            ) : (
                <code dangerouslySetInnerHTML={{__html: highlighted}}/>
            )}
        </pre>
    );
}

function highlightJson(json: string): string {
    return json
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        // Keys
        .replace(/"([^"]+)":/g, '<span class="text-brutal-white">"$1"</span>:')
        // String values
        .replace(/: "([^"]*)"([,\n\r\s}])/g, ': <span class="text-signal-green">"$1"</span>$2')
        // Numbers
        .replace(/: (-?\d+\.?\d*)([,\n\r\s}])/g, ': <span class="text-signal-cyan">$1</span>$2')
        // Booleans and null
        .replace(/: (true|false|null)([,\n\r\s}])/g, ': <span class="text-signal-violet">$1</span>$2')
        // Brackets
        .replace(/([{}[\]])/g, '<span class="text-brutal-zinc">$1</span>');
}

function highlightXml(xml: string): string {
    return xml
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        // Tags
        .replace(/&lt;(\/?[a-zA-Z][a-zA-Z0-9-_:]*)/g, '&lt;<span class="text-signal-cyan">$1</span>')
        // Attributes
        .replace(/([a-zA-Z-_:]+)=(&quot;|")/g, '<span class="text-signal-yellow">$1</span>=<span class="text-signal-green">$2')
        // Closing quote
        .replace(/(&quot;|")(\s*\/?&gt;)/g, '$1</span>$2')
        // Text content between tags
        .replace(/&gt;([^<&]+)&lt;/g, '&gt;<span class="text-brutal-white">$1</span>&lt;');
}

// Main TextVisualizer component
interface TextVisualizerProps {
    content: string;
    label?: string;
    defaultExpanded?: boolean;
    maxCollapsedHeight?: number;
    showTreeView?: boolean;
    className?: string;
}

export function TextVisualizer({
    content,
    label,
    defaultExpanded = false,
    maxCollapsedHeight = 120,
    showTreeView = true,
    className,
}: TextVisualizerProps) {
    const [isExpanded, setIsExpanded] = useState(defaultExpanded);
    const [viewMode, setViewMode] = useState<"formatted" | "tree">("formatted");
    const [copied, setCopied] = useState(false);

    const contentType = useMemo(() => detectContentType(content), [content]);
    const formattedContent = useMemo(() => formatContent(content, contentType), [content, contentType]);
    const parsedJson = useMemo(() => {
        if (contentType === "json") {
            try {
                return JSON.parse(content);
            } catch {
                return null;
            }
        }
        return null;
    }, [content, contentType]);

    const lineCount = formattedContent.split("\n").length;
    const needsCollapse = lineCount > 6;

    const handleCopy = useCallback(async (e: React.MouseEvent) => {
        e.stopPropagation();
        try {
            await navigator.clipboard.writeText(content);
            setCopied(true);
            toast.success(`${label ?? "Content"} copied to clipboard`);
            setTimeout(() => setCopied(false), 1500);
        } catch {
            toast.error("Failed to copy to clipboard");
        }
    }, [content, label]);

    const TypeIcon = contentType === "json" ? FileJson : contentType === "xml" ? FileCode : Code2;
    const typeLabel = contentType === "json" ? "JSON" : contentType === "xml" ? "XML" : "Text";

    return (
        <div className={cn("group relative bg-brutal-carbon border border-brutal-zinc", className)}>
            {/* Header */}
            <div className="flex items-center justify-between px-3 py-2 border-b border-brutal-zinc bg-brutal-dark">
                <div className="flex items-center gap-2">
                    <TypeIcon className="w-4 h-4 text-brutal-slate"/>
                    <span className="text-xs font-medium text-brutal-slate uppercase tracking-wider">
                        {label ?? typeLabel}
                    </span>
                    <span className="text-xs text-brutal-zinc">
                        ({lineCount} lines)
                    </span>
                </div>
                <div className="flex items-center gap-1">
                    {/* Tree view toggle (JSON only) */}
                    {showTreeView && contentType === "json" && parsedJson && (
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-6 w-6"
                                    onClick={() => setViewMode(viewMode === "tree" ? "formatted" : "tree")}
                                >
                                    {viewMode === "tree" ? (
                                        <Code2 className="h-3 w-3"/>
                                    ) : (
                                        <FileJson className="h-3 w-3"/>
                                    )}
                                </Button>
                            </TooltipTrigger>
                            <TooltipContent side="top">
                                <p>{viewMode === "tree" ? "Show formatted" : "Show tree view"}</p>
                            </TooltipContent>
                        </Tooltip>
                    )}

                    {/* Expand/Collapse toggle */}
                    {needsCollapse && (
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-6 w-6"
                                    onClick={() => setIsExpanded(!isExpanded)}
                                >
                                    {isExpanded ? (
                                        <Minimize2 className="h-3 w-3"/>
                                    ) : (
                                        <Maximize2 className="h-3 w-3"/>
                                    )}
                                </Button>
                            </TooltipTrigger>
                            <TooltipContent side="top">
                                <p>{isExpanded ? "Collapse" : "Expand"}</p>
                            </TooltipContent>
                        </Tooltip>
                    )}

                    {/* Copy button */}
                    <Tooltip>
                        <TooltipTrigger asChild>
                            <Button
                                variant="ghost"
                                size="icon"
                                className="h-6 w-6"
                                onClick={handleCopy}
                            >
                                {copied ? (
                                    <Check className="h-3 w-3 text-signal-green"/>
                                ) : (
                                    <Copy className="h-3 w-3"/>
                                )}
                            </Button>
                        </TooltipTrigger>
                        <TooltipContent side="top">
                            <p>{copied ? "Copied!" : "Copy raw content"}</p>
                        </TooltipContent>
                    </Tooltip>
                </div>
            </div>

            {/* Content */}
            <Collapsible open={isExpanded || !needsCollapse}>
                <div className="relative">
                    <CollapsibleContent forceMount>
                        <div
                            className={cn(
                                "p-3 overflow-auto transition-all duration-200",
                                !isExpanded && needsCollapse && "max-h-[var(--collapsed-height)]"
                            )}
                            style={{
                                "--collapsed-height": `${maxCollapsedHeight}px`,
                            } as React.CSSProperties}
                        >
                            {viewMode === "tree" && parsedJson ? (
                                <div className="text-sm font-mono">
                                    <JsonTreeNode
                                        name="root"
                                        value={parsedJson}
                                        depth={0}
                                        isLast={true}
                                    />
                                </div>
                            ) : (
                                <SyntaxHighlighter content={formattedContent} type={contentType}/>
                            )}
                        </div>
                    </CollapsibleContent>

                    {/* Fade overlay when collapsed */}
                    {!isExpanded && needsCollapse && (
                        <div className="absolute bottom-0 left-0 right-0 h-12 bg-gradient-to-t from-brutal-carbon to-transparent pointer-events-none"/>
                    )}
                </div>

                {/* Expand trigger */}
                {needsCollapse && (
                    <CollapsibleTrigger asChild>
                        <button
                            className="w-full py-2 text-xs text-brutal-slate hover:text-brutal-white hover:bg-brutal-zinc/30 transition-colors border-t border-brutal-zinc flex items-center justify-center gap-1"
                            onClick={() => setIsExpanded(!isExpanded)}
                        >
                            {isExpanded ? (
                                <>
                                    <ChevronDown className="w-3 h-3"/>
                                    Collapse
                                </>
                            ) : (
                                <>
                                    <ChevronRight className="w-3 h-3"/>
                                    Expand ({lineCount} lines)
                                </>
                            )}
                        </button>
                    </CollapsibleTrigger>
                )}
            </Collapsible>
        </div>
    );
}

// Utility function to check if content should use TextVisualizer
export function isStructuredContent(content: string): boolean {
    const type = detectContentType(content);
    return type === "json" || type === "xml";
}

// Export content type detection for external use
export {detectContentType, type ContentType};
