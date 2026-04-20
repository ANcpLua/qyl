import * as React from "react";
import {useCallback, useMemo, useState} from "react";
import {Check, ChevronDown, ChevronRight, Code2, Copy, FileCode, FileJson, Maximize2, Minimize2} from "lucide-react";
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

// Token types for safe syntax highlighting
type TokenType =
    "key"
    | "string"
    | "number"
    | "keyword"
    | "bracket"
    | "tag"
    | "attr-name"
    | "attr-value"
    | "text"
    | "plain";

interface Token {
    type: TokenType;
    value: string;
}

function tokenizeJson(json: string): Token[] {
    const tokens: Token[] = [];
    const re = /("(?:[^"\\]|\\.)*")(\s*:)?|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|(\btrue\b|\bfalse\b|\bnull\b)|([{}[\],:])|(\s+)|(.)/g;
    let match: RegExpExecArray | null;

    while ((match = re.exec(json)) !== null) {
        const [, quoted, colon, num, keyword, bracket, ws, other] = match;
        if (quoted) {
            tokens.push({type: colon ? "key" : "string", value: quoted});
            if (colon) tokens.push({type: "plain", value: colon});
        } else if (num) {
            tokens.push({type: "number", value: num});
        } else if (keyword) {
            tokens.push({type: "keyword", value: keyword});
        } else if (bracket) {
            tokens.push({type: "bracket", value: bracket});
        } else if (ws) {
            tokens.push({type: "plain", value: ws});
        } else if (other) {
            tokens.push({type: "plain", value: other});
        }
    }
    return tokens;
}

function tokenizeXml(xml: string): Token[] {
    const tokens: Token[] = [];
    const re = /(<\/?[a-zA-Z][a-zA-Z0-9-_:]*)|(\s[a-zA-Z-_:]+)(?==)|("[^"]*"|'[^']*')|(\/>|>)|([^<]+)|(.)/g;
    let match: RegExpExecArray | null;

    while ((match = re.exec(xml)) !== null) {
        const [full, tag, attrName, attrVal, close, text, other] = match;
        if (tag) {
            tokens.push({type: "tag", value: tag});
        } else if (attrName) {
            tokens.push({type: "attr-name", value: attrName});
        } else if (attrVal) {
            tokens.push({type: "attr-value", value: attrVal});
        } else if (close) {
            tokens.push({type: "tag", value: close});
        } else if (text) {
            tokens.push({type: "text", value: text});
        } else if (other) {
            tokens.push({type: "plain", value: full});
        }
    }
    return tokens;
}

const tokenClassMap: Record<TokenType, string | null> = {
    key: "text-brutal-white",
    string: "text-signal-green",
    number: "text-signal-cyan",
    keyword: "text-signal-violet",
    bracket: "text-brutal-zinc",
    tag: "text-signal-cyan",
    "attr-name": "text-signal-yellow",
    "attr-value": "text-signal-green",
    text: "text-brutal-white",
    plain: null,
};

// Syntax highlighting for formatted content — renders React elements, no innerHTML
function SyntaxHighlighter({content, type}: { content: string; type: ContentType }) {
    const tokens = useMemo(() => {
        if (type === "json") return tokenizeJson(content);
        if (type === "xml") return tokenizeXml(content);
        return null;
    }, [content, type]);

    return (
        <pre className="text-sm font-mono whitespace-pre-wrap overflow-x-auto">
            <code>
                {tokens
                    ? tokens.map((tok, i) => {
                        const cls = tokenClassMap[tok.type];
                        return cls
                            ? <span key={i} className={cls}>{tok.value}</span>
                            : tok.value;
                    })
                    : content}
            </code>
        </pre>
    );
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
                            <TooltipTrigger
                                render={<Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-6 w-6 min-h-11 min-w-11"
                                    aria-label={viewMode === "tree" ? "Show formatted" : "Show tree view"}
                                    onClick={() => setViewMode(viewMode === "tree" ? "formatted" : "tree")}
                                />}
                            >
                                {viewMode === "tree" ? (
                                    <Code2 className="h-3 w-3"/>
                                ) : (
                                    <FileJson className="h-3 w-3"/>
                                )}
                            </TooltipTrigger>
                            <TooltipContent side="top">
                                <p>{viewMode === "tree" ? "Show formatted" : "Show tree view"}</p>
                            </TooltipContent>
                        </Tooltip>
                    )}

                    {/* Expand/Collapse toggle */}
                    {needsCollapse && (
                        <Tooltip>
                            <TooltipTrigger
                                render={<Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-6 w-6 min-h-11 min-w-11"
                                    aria-label={isExpanded ? "Collapse content" : "Expand content"}
                                    onClick={() => setIsExpanded(!isExpanded)}
                                />}
                            >
                                {isExpanded ? (
                                    <Minimize2 className="h-3 w-3"/>
                                ) : (
                                    <Maximize2 className="h-3 w-3"/>
                                )}
                            </TooltipTrigger>
                            <TooltipContent side="top">
                                <p>{isExpanded ? "Collapse" : "Expand"}</p>
                            </TooltipContent>
                        </Tooltip>
                    )}

                    {/* Copy button */}
                    <Tooltip>
                        <TooltipTrigger
                            render={<Button
                                variant="ghost"
                                size="icon"
                                className="h-6 w-6 min-h-11 min-w-11"
                                aria-label={copied ? "Copied!" : "Copy raw content"}
                                onClick={handleCopy}
                            />}
                        >
                            {copied ? (
                                <Check className="h-3 w-3 text-signal-green"/>
                            ) : (
                                <Copy className="h-3 w-3"/>
                            )}
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
                    <CollapsibleContent keepMounted>
                        <div
                            className={cn(
                                "p-3 overflow-auto transition-[max-height] duration-200",
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
                        <div
                            className="absolute bottom-0 left-0 right-0 h-12 bg-gradient-to-t from-brutal-carbon to-transparent pointer-events-none"/>
                    )}
                </div>

                {/* Expand trigger */}
                {needsCollapse && (
                    <CollapsibleTrigger
                        render={<button
                            className="w-full py-2 text-xs text-brutal-slate hover:text-brutal-white hover:bg-brutal-zinc/30 transition-colors border-t border-brutal-zinc flex items-center justify-center gap-1"
                            onClick={() => setIsExpanded(!isExpanded)}
                        />}
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
