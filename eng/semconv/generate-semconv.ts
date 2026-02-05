#!/usr/bin/env npx tsx
/**
 * OTel Semantic Conventions Generator
 *
 * Generates TypeScript, C#, TypeSpec, and DuckDB column definitions from
 * @opentelemetry/semantic-conventions NPM package.
 *
 * Usage:
 *   npm run generate           # Generate all outputs
 *   npm run generate:ts        # TypeScript only
 *   npm run generate:cs        # C# only
 *   npm run generate:tsp       # TypeSpec only
 *   npm run generate:sql       # DuckDB only
 */

import * as fs from "fs";
import * as path from "path";
import {fileURLToPath} from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// ============================================================================
// Types
// ============================================================================

interface Attribute {
    exportName: string; // ATTR_GEN_AI_SYSTEM
    value: string; // "gen_ai.system"
    deprecated?: string;
}

interface EnumValue {
    attributePrefix: string; // GEN_AI_SYSTEM
    exportName: string; // GEN_AI_SYSTEM_VALUE_OPENAI
    memberName: string; // OPENAI
    value: string; // "openai"
    deprecated?: string;
}

interface ParsedData {
    version: string;
    attributes: Attribute[];
    enums: Map<string, EnumValue[]>; // grouped by attributePrefix
}

// ============================================================================
// Configuration
// ============================================================================

const CONFIG = {
    // Filter to only include these prefixes (empty = all)
    includePrefixes: [
        // AI
        "gen_ai", "code",
        // Transport
        "http", "rpc", "messaging", "url", "user_agent", "signalr", "kestrel",
        // Data
        "db", "file", "vcs", "artifact", "elasticsearch",
        // Infra
        "cloud", "container", "k8s", "host", "os", "faas", "webengine",
        // Security
        "network", "tls", "dns",
        // Runtime
        "process", "thread", "system", "dotnet", "aspnetcore",
        // Identity
        "user", "enduser", "geo", "client", "server", "service", "telemetry",
        // Observe
        "browser", "session", "exception", "error", "log", "feature_flag", "otel", "test",
        // Ops
        "cicd", "deployment",
        // Vendor AI
        "openai", "azure",
    ],

    // Output paths (relative to this script) - Direct to final destinations
    outputs: {
        typescript: "../../src/qyl.dashboard/src/lib/semconv.ts",
        csharp: "../../src/qyl.servicedefaults/Instrumentation/SemanticConventions.g.cs",
        csharpUtf8: "../../src/qyl.servicedefaults/Instrumentation/SemanticConventions.Utf8.g.cs",
        typespec: "../../core/specs/generated/semconv.g.tsp",
        duckdb: "../../src/qyl.collector/Storage/promoted-columns.g.sql",
    },

    // TypeSpec reserved keywords that need escaping
    typespecReservedKeywords: new Set([
        "namespace", "model", "interface", "enum", "union", "alias",
        "scalar", "op", "using", "import", "is", "extends", "unknown",
        "void", "never", "null", "true", "false", "if", "else", "return",
    ]),

    // C# namespace
    csharpNamespace: "Qyl.ServiceDefaults.Instrumentation",

    // TypeSpec namespace
    typespecNamespace: "OTel.SemConv",

    // DuckDB type mappings based on attribute suffix
    duckDbTypes: new Map<string, string>([
        ["_tokens", "BIGINT"],
        ["_count", "BIGINT"],
        ["_size", "BIGINT"],
        ["_duration", "DOUBLE"],
        ["_temperature", "DOUBLE"],
        ["_top_p", "DOUBLE"],
        ["_top_k", "BIGINT"],
        ["_max_tokens", "BIGINT"],
        ["_seed", "BIGINT"],
        ["_frequency_penalty", "DOUBLE"],
        ["_presence_penalty", "DOUBLE"],
    ]),

    // TypeSpec type mappings based on attribute suffix
    typespecTypes: new Map<string, string>([
        ["_tokens", "int64"],
        ["_count", "int64"],
        ["_size", "int64"],
        ["_duration", "float64"],
        ["_temperature", "float64"],
        ["_top_p", "float64"],
        ["_top_k", "int64"],
        ["_max_tokens", "int64"],
        ["_seed", "int64"],
        ["_frequency_penalty", "float64"],
        ["_presence_penalty", "float64"],
        ["_port", "int32"],
        ["_pid", "int32"],
        ["_bytes", "int64"],
        ["_length", "int64"],
    ]),
};

// ============================================================================
// Parser
// ============================================================================

function getPackageVersion(): string {
    const pkgPath = path.join(
        __dirname,
        "node_modules/@opentelemetry/semantic-conventions/package.json"
    );
    const pkg = JSON.parse(fs.readFileSync(pkgPath, "utf-8"));
    return pkg.version;
}

function parseDeclarationFile(filePath: string): {
    attrs: Attribute[];
    enums: EnumValue[];
} {
    const content = fs.readFileSync(filePath, "utf-8");
    const attrs: Attribute[] = [];
    const enums: EnumValue[] = [];

    // Match: export declare const ATTR_GEN_AI_SYSTEM: "gen_ai.system";
    // Also: export const ATTR_GEN_AI_SYSTEM = 'gen_ai.system';
    const attrRegex =
        /export\s+(?:declare\s+)?const\s+(ATTR_[A-Z0-9_]+)\s*(?::\s*['"]([^'"]+)['"]|=\s*['"]([^'"]+)['"])/g;
    let match: RegExpExecArray | null;
    while ((match = attrRegex.exec(content)) !== null) {
        attrs.push({
            exportName: match[1],
            value: match[2] || match[3], // Type annotation or assignment
        });
    }

    // Match enum values: export declare const GEN_AI_SYSTEM_VALUE_OPENAI: "openai";
    // Also: export const GEN_AI_SYSTEM_VALUE_OPENAI = 'openai';
    const enumRegex =
        /export\s+(?:declare\s+)?const\s+([A-Z0-9_]+)_VALUE_([A-Z0-9_]+)\s*(?::\s*['"]([^'"]+)['"]|=\s*['"]([^'"]+)['"])/g;
    while ((match = enumRegex.exec(content)) !== null) {
        enums.push({
            attributePrefix: match[1],
            exportName: `${match[1]}_VALUE_${match[2]}`,
            memberName: match[2],
            value: match[3] || match[4], // Type annotation or assignment
        });
    }

    return {attrs, enums};
}

function parse(): ParsedData {
    const version = getPackageVersion();

    // Parse all .d.ts files in the package
    const semconvDir = path.join(
        __dirname,
        "node_modules/@opentelemetry/semantic-conventions/build/src"
    );

    const allAttrs: Attribute[] = [];
    const allEnums: EnumValue[] = [];

    function walkDir(dir: string) {
        const files = fs.readdirSync(dir);
        for (const file of files) {
            const fullPath = path.join(dir, file);
            const stat = fs.statSync(fullPath);
            if (stat.isDirectory()) {
                walkDir(fullPath);
            } else if (file.endsWith(".d.ts")) {
                const {attrs, enums} = parseDeclarationFile(fullPath);
                allAttrs.push(...attrs);
                allEnums.push(...enums);
            }
        }
    }

    walkDir(semconvDir);

    // Filter by prefix if configured
    const filteredAttrs =
        CONFIG.includePrefixes.length > 0
            ? allAttrs.filter((a) =>
                CONFIG.includePrefixes.some((p) => a.value.startsWith(p))
            )
            : allAttrs;

    const filteredEnums =
        CONFIG.includePrefixes.length > 0
            ? allEnums.filter((e) =>
                CONFIG.includePrefixes.some((p) => {
                    // Normalize both to use underscores for consistent comparison
                    const normalizedPrefix = p.replace(/\./g, "_");
                    const enumPrefixNormalized = e.attributePrefix.toLowerCase();
                    return e.value.startsWith(p) ||
                        e.value.startsWith(normalizedPrefix) ||
                        enumPrefixNormalized.startsWith(normalizedPrefix);
                })
            )
            : allEnums;

    // Group enums by attribute prefix
    const enumMap = new Map<string, EnumValue[]>();
    for (const e of filteredEnums) {
        const group = enumMap.get(e.attributePrefix) || [];
        group.push(e);
        enumMap.set(e.attributePrefix, group);
    }

    // Dedupe attributes by value
    const seenValues = new Set<string>();
    const dedupedAttrs = filteredAttrs.filter((a) => {
        if (seenValues.has(a.value)) return false;
        seenValues.add(a.value);
        return true;
    });

    // Sort for consistent output
    dedupedAttrs.sort((a, b) => a.value.localeCompare(b.value));

    console.log(
        `  Found ${dedupedAttrs.length} attributes, ${enumMap.size} enum groups`
    );

    return {
        version,
        attributes: dedupedAttrs,
        enums: enumMap,
    };
}

// ============================================================================
// TypeScript Generator
// ============================================================================

function generateTypeScript(data: ParsedData): string {
    const lines: string[] = [
        `// <auto-generated/>`,
        `// Generated from @opentelemetry/semantic-conventions v${data.version}`,
        `// Do not edit manually - run 'npm run generate' in SemconvGenerator`,
        ``,
        `// Attribute keys`,
    ];

    // Group attributes by prefix for readability
    const grouped = groupByPrefix(data.attributes.map((a) => a.value));

    for (const [prefix, attrs] of grouped) {
        lines.push(``);
        lines.push(`// ${prefix}`);
        for (const attr of attrs) {
            const found = data.attributes.find((a) => a.value === attr);
            if (found) {
                lines.push(`export const ${attrToConstName(found.value)} = "${found.value}";`);
            }
        }
    }

    // Enum values as objects
    if (data.enums.size > 0) {
        lines.push(``);
        lines.push(`// Enum values`);

        for (const [prefix, values] of data.enums) {
            const enumName = prefixToEnumName(prefix) + "Values";
            lines.push(`export const ${enumName} = {`);
            for (const v of values) {
                const memberName = snakeToPascal(v.memberName);
                lines.push(`  ${memberName}: "${v.value}",`);
            }
            lines.push(`} as const;`);
            lines.push(``);
        }
    }

    return lines.join("\n");
}

// ============================================================================
// C# Generator
// ============================================================================

function generateCSharp(data: ParsedData): string {
    const lines: string[] = [
        `// <auto-generated/>`,
        `// Generated from @opentelemetry/semantic-conventions v${data.version}`,
        `// Do not edit manually - run 'npm run generate' in SemconvGenerator`,
        ``,
        `namespace ${CONFIG.csharpNamespace};`,
        ``,
    ];

    // Group attributes by prefix
    const grouped = groupByPrefix(data.attributes.map((a) => a.value));

    for (const [prefix, attrs] of grouped) {
        const className = prefixToClassName(prefix) + "Attributes";
        lines.push(`/// <summary>`);
        lines.push(`/// Semantic convention attributes for ${prefix}.*`);
        lines.push(`/// </summary>`);
        lines.push(`public static class ${className}`);
        lines.push(`{`);

        for (const attr of attrs) {
            const found = data.attributes.find((a) => a.value === attr);
            if (found) {
                const propName = attrToCSharpPropName(found.value, prefix);
                lines.push(`    /// <summary>${found.value}</summary>`);
                lines.push(`    public const string ${propName} = "${found.value}";`);
                lines.push(``);
            }
        }

        lines.push(`}`);
        lines.push(``);
    }

    // Enum values
    for (const [prefix, values] of data.enums) {
        const className = prefixToClassName(prefix.toLowerCase().replace(/_/g, ".")) + "Values";
        lines.push(`/// <summary>`);
        lines.push(`/// Enum values for ${prefix.toLowerCase().replace(/_/g, ".")}`);
        lines.push(`/// </summary>`);
        lines.push(`public static class ${className}`);
        lines.push(`{`);

        for (const v of values) {
            const propName = snakeToPascal(v.memberName);
            lines.push(`    /// <summary>${v.value}</summary>`);
            lines.push(`    public const string ${propName} = "${v.value}";`);
            lines.push(``);
        }

        lines.push(`}`);
        lines.push(``);
    }

    return lines.join("\n");
}

// ============================================================================
// C# UTF-8 Generator (ReadOnlySpan<byte> for zero-allocation parsing)
// ============================================================================

function generateCSharpUtf8(data: ParsedData): string {
    const lines: string[] = [
        `// <auto-generated/>`,
        `// Generated from @opentelemetry/semantic-conventions v${data.version}`,
        `// Do not edit manually - run 'npm run generate' in SemconvGenerator`,
        `//`,
        `// UTF-8 ReadOnlySpan<byte> for zero-allocation OTLP parsing hot paths`,
        ``,
        `namespace ${CONFIG.csharpNamespace};`,
        ``,
    ];

    // Group attributes by prefix
    const grouped = groupByPrefix(data.attributes.map((a) => a.value));

    for (const [prefix, attrs] of grouped) {
        const className = prefixToClassName(prefix) + "Utf8";
        lines.push(`/// <summary>`);
        lines.push(`/// UTF-8 attribute keys for ${prefix}.* (zero-allocation parsing)`);
        lines.push(`/// </summary>`);
        lines.push(`public static class ${className}`);
        lines.push(`{`);

        for (const attr of attrs) {
            const found = data.attributes.find((a) => a.value === attr);
            if (found) {
                const propName = attrToCSharpPropName(found.value, prefix);
                lines.push(`    /// <summary>${found.value}</summary>`);
                lines.push(`    public static ReadOnlySpan<byte> ${propName} => "${found.value}"u8;`);
                lines.push(``);
            }
        }

        lines.push(`}`);
        lines.push(``);
    }

    // Enum values as UTF-8
    for (const [prefix, values] of data.enums) {
        const className = prefixToClassName(prefix.toLowerCase().replace(/_/g, ".")) + "Utf8Values";
        lines.push(`/// <summary>`);
        lines.push(`/// UTF-8 enum values for ${prefix.toLowerCase().replace(/_/g, ".")}`);
        lines.push(`/// </summary>`);
        lines.push(`public static class ${className}`);
        lines.push(`{`);

        for (const v of values) {
            const propName = snakeToPascal(v.memberName);
            lines.push(`    /// <summary>${v.value}</summary>`);
            lines.push(`    public static ReadOnlySpan<byte> ${propName} => "${v.value}"u8;`);
            lines.push(``);
        }

        lines.push(`}`);
        lines.push(``);
    }

    return lines.join("\n");
}

// ============================================================================
// TypeSpec Generator (Enhanced for QYL integration)
// ============================================================================

function generateTypeSpec(data: ParsedData): string {
    const lines: string[] = [
        `// <auto-generated/>`,
        `// Generated from @opentelemetry/semantic-conventions v${data.version}`,
        `// Do not edit manually - run 'npm run generate:tsp' in SemconvGenerator`,
        `//`,
        `// Usage in your TypeSpec files:`,
        `//   import "./semconv.g.tsp";`,
        `//   using ${CONFIG.typespecNamespace};`,
        `//`,
        `//   model MySpan {`,
        `//     @encodedName("application/json", Keys.GenAi.providerName)`,
        `//     provider: GenAiProviderNameValue;`,
        `//   }`,
        ``,
        `import "@typespec/http";`,
        ``,
        `using TypeSpec.Http;`,
        ``,
        `namespace ${CONFIG.typespecNamespace};`,
        ``,
        `// ============================================================================`,
        `// Common OTel Scalars (for type-safe attribute values)`,
        `// ============================================================================`,
        ``,
        `/** 128-bit trace identifier (32 hex chars) */`,
        `@minLength(32) @maxLength(32)`,
        `@pattern("^[a-f0-9]{32}$")`,
        `scalar TraceId extends string;`,
        ``,
        `/** 64-bit span identifier (16 hex chars) */`,
        `@minLength(16) @maxLength(16)`,
        `@pattern("^[a-f0-9]{16}$")`,
        `scalar SpanId extends string;`,
        ``,
        `/** Token count (always int64 per semconv) */`,
        `scalar TokenCount extends int64;`,
        ``,
        `/** Duration in seconds (float64) */`,
        `scalar DurationSeconds extends float64;`,
        ``,
        `/** Duration in nanoseconds (int64) */`,
        `scalar DurationNanos extends int64;`,
        ``,
        `/** Port number */`,
        `@minValue(1) @maxValue(65535)`,
        `scalar Port extends int32;`,
        ``,
        `/** Byte count */`,
        `@minValue(0)`,
        `scalar ByteCount extends int64;`,
        ``,
    ];

    // Group attributes by top-level domain (gen_ai, http, db, etc.)
    const domainGroups = groupByTopLevelPrefix(data.attributes.map(a => a.value));

    // Build a map of attribute prefix -> enum values for quick lookup
    const enumLookup = buildEnumLookup(data.enums);

    // ========================================================================
    // Generate Keys namespace with string constants
    // ========================================================================
    lines.push(`// ============================================================================`);
    lines.push(`// Attribute Key Constants (use with @encodedName)`);
    lines.push(`// ============================================================================`);
    lines.push(`// Example: @encodedName("application/json", Keys.GenAi.providerName)`);
    lines.push(`// ============================================================================`);
    lines.push(``);
    lines.push(`namespace Keys {`);

    for (const [domain, attrs] of domainGroups) {
        const nsName = prefixToClassName(domain);
        lines.push(`  /** ${domain}.* attribute keys */`);
        lines.push(`  namespace ${nsName} {`);

        for (const attr of attrs) {
            const found = data.attributes.find((a) => a.value === attr);
            if (found) {
                const propName = attrToTypeSpecPropName(found.value, domain);
                lines.push(`    /** "${found.value}" */`);
                // propName is already escaped by attrToTypeSpecPropName
                lines.push(`    alias ${propName} = "${found.value}";`);
            }
        }

        lines.push(`  }`);
        lines.push(``);
    }

    lines.push(`}`);
    lines.push(``);

    // ========================================================================
    // Generate enum union types (deduplicated)
    // ========================================================================
    lines.push(`// ============================================================================`);
    lines.push(`// Enum Value Types (union types for known values)`);
    lines.push(`// ============================================================================`);
    lines.push(``);

    // Track emitted enums to avoid duplicates
    const emittedEnums = new Set<string>();

    for (const [enumPrefix, values] of data.enums) {
        const enumName = prefixToClassName(enumPrefix.toLowerCase().replace(/_/g, ".")) + "Value";

        // Skip if already emitted
        if (emittedEnums.has(enumName)) {
            continue;
        }
        emittedEnums.add(enumName);

        const attrKey = enumPrefix.toLowerCase().replace(/_/g, ".");

        lines.push(`/** Known values for ${attrKey} */`);
        lines.push(`union ${enumName} {`);

        for (const v of values) {
            const memberName = escapeTypeSpecKeyword(snakeToCamel(v.memberName));
            lines.push(`  /** "${v.value}" */`);
            lines.push(`  ${memberName}: "${v.value}",`);
        }

        lines.push(`  /** Allow unknown/custom values */`);
        lines.push(`  string,`);
        lines.push(`}`);
        lines.push(``);
    }

    // ========================================================================
    // Generate attribute models per domain
    // ========================================================================
    for (const [domain, attrs] of domainGroups) {
        const modelName = prefixToClassName(domain) + "Attributes";

        lines.push(`// ============================================================================`);
        lines.push(`// ${domain}.* Attributes Model`);
        lines.push(`// ============================================================================`);
        lines.push(``);

        lines.push(`/** Semantic convention attributes for ${domain}.* */`);
        lines.push(`model ${modelName} {`);

        for (const attr of attrs) {
            const found = data.attributes.find((a) => a.value === attr);
            if (found) {
                const propName = attrToTypeSpecPropName(found.value, domain);
                const propType = inferTypeSpecType(found.value, enumLookup);
                lines.push(`  /** ${found.value} */`);
                lines.push(`  @encodedName("application/json", "${found.value}")`);
                lines.push(`  ${propName}?: ${propType};`);
                lines.push(``);
            }
        }

        lines.push(`}`);
        lines.push(``);
    }

    // Note: Combined model (AllOTelAttributes) intentionally omitted due to
    // property name collisions across domains (name, id, version, etc.)
    // Use individual domain models instead: GenAiAttributes, DbAttributes, etc.

    return lines.join("\n");
}

function buildEnumLookup(enums: Map<string, EnumValue[]>): Map<string, string> {
    // Map from attribute key pattern to enum type name
    // e.g., "gen_ai.system" -> "GenAiSystemValue"
    const lookup = new Map<string, string>();

    for (const [prefix] of enums) {
        // Convert GEN_AI_SYSTEM -> gen_ai.system
        const attrKey = prefix.toLowerCase().replace(/_/g, ".");
        const enumName = prefixToClassName(attrKey) + "Value";
        lookup.set(attrKey, enumName);
    }

    return lookup;
}


function inferTypeSpecType(attrName: string, enumLookup: Map<string, string>): string {
    // Check if this attribute has known enum values
    const enumType = enumLookup.get(attrName);
    if (enumType) {
        return enumType;
    }

    // Check suffix mappings
    for (const [suffix, type] of CONFIG.typespecTypes) {
        if (attrName.endsWith(suffix)) {
            return type;
        }
    }

    // Default to string
    return "string";
}

function attrToTypeSpecPropName(attr: string, domain: string): string {
    // gen_ai.request.model with domain gen_ai -> requestModel
    // Remove the domain prefix and convert to camelCase
    const withoutDomain = attr.startsWith(domain + ".")
        ? attr.slice(domain.length + 1)
        : attr;

    // Convert dots and underscores to camelCase
    const parts = withoutDomain.split(/[._]/);
    const identifier = parts
        .map((p, i) => i === 0 ? p.toLowerCase() : p.charAt(0).toUpperCase() + p.slice(1).toLowerCase())
        .join("");

    // Escape TypeSpec reserved keywords with backticks
    return escapeTypeSpecKeyword(identifier);
}

function snakeToCamel(snake: string): string {
    const parts = snake.toLowerCase().split("_");
    return parts
        .map((p, i) => i === 0 ? p : p.charAt(0).toUpperCase() + p.slice(1))
        .join("");
}

/**
 * Escapes TypeSpec reserved keywords with backticks.
 * e.g., "namespace" -> "`namespace`", "unknown" -> "`unknown`"
 */
function escapeTypeSpecKeyword(identifier: string): string {
    if (CONFIG.typespecReservedKeywords.has(identifier)) {
        return `\`${identifier}\``;
    }
    return identifier;
}

// ============================================================================
// DuckDB Generator
// ============================================================================

function generateDuckDb(data: ParsedData): string {
    const lines: string[] = [
        `-- <auto-generated/>`,
        `-- Generated from @opentelemetry/semantic-conventions v${data.version}`,
        `-- Do not edit manually - run 'npm run generate' in SemconvGenerator`,
        `--`,
        `-- Promoted columns for fast queries (extracted from attributes_json)`,
        `-- Include in CREATE TABLE statements as needed`,
        ``,
    ];

    // Group attributes by prefix
    const grouped = groupByPrefix(data.attributes.map((a) => a.value));

    for (const [prefix, attrs] of grouped) {
        lines.push(`-- ${prefix} attributes`);

        for (const attr of attrs) {
            const columnName = attr.replace(/\./g, "_");
            const duckDbType = inferDuckDbType(attr);
            lines.push(`${columnName} ${duckDbType},`);
        }

        lines.push(``);
    }

    return lines.join("\n");
}

function inferDuckDbType(attrName: string): string {
    // Check suffix mappings
    for (const [suffix, type] of CONFIG.duckDbTypes) {
        if (attrName.endsWith(suffix)) {
            return type;
        }
    }
    // Default to VARCHAR
    return "VARCHAR";
}

// ============================================================================
// Helpers
// ============================================================================

function groupByPrefix(values: string[]): Map<string, string[]> {
    const groups = new Map<string, string[]>();
    for (const v of values) {
        const parts = v.split(".");
        const prefix = parts.slice(0, 2).join("."); // e.g., "gen_ai"
        const group = groups.get(prefix) || [];
        group.push(v);
        groups.set(prefix, group);
    }
    return groups;
}

function groupByTopLevelPrefix(values: string[]): Map<string, string[]> {
    const groups = new Map<string, string[]>();
    for (const v of values) {
        const parts = v.split(".");
        // Use first part only: gen_ai.request.model -> gen_ai
        const prefix = parts[0].replace(/_/g, "_");
        const group = groups.get(prefix) || [];
        group.push(v);
        groups.set(prefix, group);
    }
    return groups;
}

function attrToConstName(attr: string): string {
    // gen_ai.system -> GEN_AI_SYSTEM
    return attr.toUpperCase().replace(/\./g, "_");
}

function prefixToEnumName(prefix: string): string {
    // GEN_AI_SYSTEM -> GenAiSystem
    return snakeToPascal(prefix);
}

function prefixToClassName(prefix: string): string {
    // gen_ai -> GenAi
    return prefix
        .split(/[._]/)
        .map((p) => p.charAt(0).toUpperCase() + p.slice(1).toLowerCase())
        .join("");
}

function attrToCSharpPropName(attr: string, prefix: string): string {
    // gen_ai.request.model with prefix gen_ai -> RequestModel
    // gen_ai.system with prefix gen_ai.system -> Value (fallback for exact match)
    if (attr === prefix || attr.length <= prefix.length) {
        // Extract last segment as the property name
        const parts = attr.split(".");
        const lastPart = parts[parts.length - 1];
        return lastPart.charAt(0).toUpperCase() + lastPart.slice(1).toLowerCase();
    }
    const withoutPrefix = attr.slice(prefix.length + 1); // remove "gen_ai."
    return withoutPrefix
        .split(/[._]/)
        .map((p) => p.charAt(0).toUpperCase() + p.slice(1).toLowerCase())
        .join("");
}

function snakeToPascal(snake: string): string {
    return snake
        .toLowerCase()
        .split("_")
        .map((p) => p.charAt(0).toUpperCase() + p.slice(1))
        .join("");
}


// ============================================================================
// Main
// ============================================================================

function parseArg(args: string[], name: string): string | undefined {
    const prefix = `--${name}=`;
    const arg = args.find(a => a.startsWith(prefix));
    return arg ? arg.slice(prefix.length) : undefined;
}

async function main() {
    const args = process.argv.slice(2);
    const tsOnly = args.includes("--ts-only");
    const csOnly = args.includes("--cs-only");
    const utf8Only = args.includes("--utf8-only");
    const tspOnly = args.includes("--tsp-only");
    const sqlOnly = args.includes("--sql-only");
    const generateAll = !tsOnly && !csOnly && !utf8Only && !tspOnly && !sqlOnly;

    // Optional overrides
    const namespaceOverride = parseArg(args, "namespace");
    const outputOverride = parseArg(args, "output");

    console.log("Parsing @opentelemetry/semantic-conventions...");
    const data = parse();

    if (generateAll || tsOnly) {
        const ts = generateTypeScript(data);
        const tsPath = path.join(__dirname, CONFIG.outputs.typescript);
        fs.mkdirSync(path.dirname(tsPath), {recursive: true});
        fs.writeFileSync(tsPath, ts);
        console.log(`✓ Generated ${CONFIG.outputs.typescript}`);
    }

    if (generateAll || csOnly) {
        // Use override namespace if provided
        const originalNamespace = CONFIG.csharpNamespace;
        if (namespaceOverride) {
            CONFIG.csharpNamespace = namespaceOverride;
        }

        const cs = generateCSharp(data);
        const csPath = outputOverride
            ? path.join(__dirname, outputOverride)
            : path.join(__dirname, CONFIG.outputs.csharp);
        fs.mkdirSync(path.dirname(csPath), {recursive: true});
        fs.writeFileSync(csPath, cs);
        console.log(`✓ Generated ${outputOverride || CONFIG.outputs.csharp}`);

        // Restore
        CONFIG.csharpNamespace = originalNamespace;
    }

    if (generateAll || utf8Only) {
        // Use override namespace if provided
        const originalNamespace = CONFIG.csharpNamespace;
        if (namespaceOverride) {
            CONFIG.csharpNamespace = namespaceOverride;
        }

        const utf8 = generateCSharpUtf8(data);
        const utf8Path = outputOverride
            ? path.join(__dirname, outputOverride)
            : path.join(__dirname, CONFIG.outputs.csharpUtf8);
        fs.mkdirSync(path.dirname(utf8Path), {recursive: true});
        fs.writeFileSync(utf8Path, utf8);
        console.log(`✓ Generated ${utf8Path.replace(__dirname + "/", "")}`);

        // Restore
        CONFIG.csharpNamespace = originalNamespace;
    }

    if (generateAll || tspOnly) {
        // Use override namespace if provided
        const originalNamespace = CONFIG.typespecNamespace;
        if (namespaceOverride) {
            CONFIG.typespecNamespace = namespaceOverride;
        }

        const tsp = generateTypeSpec(data);
        const tspPath = outputOverride
            ? path.join(__dirname, outputOverride)
            : path.join(__dirname, CONFIG.outputs.typespec);
        fs.mkdirSync(path.dirname(tspPath), {recursive: true});
        fs.writeFileSync(tspPath, tsp);
        console.log(`✓ Generated ${tspPath.replace(__dirname + "/", "")}`);

        // Restore
        CONFIG.typespecNamespace = originalNamespace;
    }

    if (generateAll || sqlOnly) {
        const sql = generateDuckDb(data);
        const sqlPath = path.join(__dirname, CONFIG.outputs.duckdb);
        fs.mkdirSync(path.dirname(sqlPath), {recursive: true});
        fs.writeFileSync(sqlPath, sql);
        console.log(`✓ Generated ${CONFIG.outputs.duckdb}`);
    }

    console.log("Done!");
}

main().catch((err) => {
    console.error(err);
    process.exit(1);
});
