#!/usr/bin/env npx tsx
/**
 * OTel Semantic Conventions Generator
 *
 * Generates TypeScript, C#, and DuckDB column definitions from
 * @opentelemetry/semantic-conventions NPM package.
 *
 * Usage:
 *   npm run generate           # Generate all outputs
 *   npm run generate:ts        # TypeScript only
 *   npm run generate:cs        # C# only
 *   npm run generate:sql       # DuckDB only
 */

import * as fs from "fs";
import * as path from "path";
import { fileURLToPath } from "url";

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
    "user", "enduser", "geo", "client",
    // Observe
    "browser", "session", "exception", "error", "log", "feature_flag", "otel", "test",
    // Ops
    "cicd", "deployment",
  ],

  // Output paths (relative to this script)
  outputs: {
    typescript: "output/semconv.ts",
    csharp: "output/SemanticConventions.g.cs",
    csharpUtf8: "output/SemanticConventions.Utf8.g.cs",
    duckdb: "output/promoted-columns.sql",
    // NuGet Package
    packageDir: "output/OTelConventions",
  },

  // C# namespace
  csharpNamespace: "Qyl.ServiceDefaults.Instrumentation",

  // Package namespace (unused, kept for compile compatibility)
  packageNamespace: "Qyl.ServiceDefaults.Instrumentation",

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
  if (!fs.existsSync(filePath)) {
    return { attrs: [], enums: [] };
  }

  const content = fs.readFileSync(filePath, "utf-8");
  const attrs: Attribute[] = [];
  const enums: EnumValue[] = [];

  // Parse: export declare const ATTR_GEN_AI_SYSTEM: "gen_ai.system";
  const attrRegex =
    /export declare const (ATTR_[A-Z0-9_]+): "([^"]+)";/g;
  let match;
  while ((match = attrRegex.exec(content)) !== null) {
    const [, exportName, value] = match;
    attrs.push({ exportName, value });
  }

  // Parse: export declare const GEN_AI_OPERATION_NAME_VALUE_CHAT: "chat";
  // Pattern: PREFIX_VALUE_MEMBER where PREFIX doesn't contain VALUE
  const enumRegex =
    /export declare const ([A-Z0-9_]+?)_VALUE_([A-Z0-9_]+): "([^"]+)";/g;
  while ((match = enumRegex.exec(content)) !== null) {
    const [, attributePrefix, memberName, value] = match;
    // Skip if this looks like it's actually an attribute (contains ATTR)
    if (attributePrefix.includes("ATTR")) continue;
    enums.push({
      attributePrefix,
      exportName: `${attributePrefix}_VALUE_${memberName}`,
      memberName,
      value,
    });
  }

  return { attrs, enums };
}

function parse(): ParsedData {
  const version = getPackageVersion();
  const basePath = path.join(
    __dirname,
    "node_modules/@opentelemetry/semantic-conventions/build/src"
  );

  // Parse both stable and experimental
  const files = [
    path.join(basePath, "stable_attributes.d.ts"),
    path.join(basePath, "experimental_attributes.d.ts"),
  ];

  let allAttrs: Attribute[] = [];
  let allEnums: EnumValue[] = [];

  for (const file of files) {
    const { attrs, enums } = parseDeclarationFile(file);
    allAttrs = allAttrs.concat(attrs);
    allEnums = allEnums.concat(enums);
  }

  // Filter by prefix if configured
  if (CONFIG.includePrefixes.length > 0) {
    allAttrs = allAttrs.filter((a) =>
      CONFIG.includePrefixes.some((p) => a.value.startsWith(p))
    );
    allEnums = allEnums.filter((e) => {
      // GEN_AI_OPERATION_NAME -> gen_ai_operation_name (lowercase)
      const prefixLower = e.attributePrefix.toLowerCase();
      // Check if prefix starts with any included prefix
      return CONFIG.includePrefixes.some((p) => {
        // gen_ai -> gen_ai (replace dots with underscores for matching)
        const normalizedP = p.replace(/\./g, "_");
        return prefixLower.startsWith(normalizedP);
      });
    });
  }

  // Group enums by attributePrefix
  const enumGroups = new Map<string, EnumValue[]>();
  for (const e of allEnums) {
    const group = enumGroups.get(e.attributePrefix) || [];
    group.push(e);
    enumGroups.set(e.attributePrefix, group);
  }

  console.log(
    `Parsed ${allAttrs.length} attributes and ${allEnums.length} enum values`
  );

  return { version, attributes: allAttrs, enums: enumGroups };
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
    `// =============================================================================`,
    `// Attribute Names`,
    `// =============================================================================`,
    ``,
  ];

  // Group attributes by prefix for organization
  const grouped = groupByPrefix(data.attributes.map((a) => a.value));

  for (const [prefix, attrs] of grouped) {
    lines.push(`// ${prefix}`);
    for (const attr of attrs) {
      const found = data.attributes.find((a) => a.value === attr);
      if (found) {
        const constName = attrToConstName(found.value);
        lines.push(`export const ${constName} = "${found.value}" as const;`);
      }
    }
    lines.push(``);
  }

  // Enum values
  if (data.enums.size > 0) {
    lines.push(`// =============================================================================`);
    lines.push(`// Enum Values`);
    lines.push(`// =============================================================================`);
    lines.push(``);

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
// Package Generator (split files per domain)
// ============================================================================

function generatePackage(data: ParsedData): void {
  const packageDir = path.join(__dirname, CONFIG.outputs.packageDir);

  // Ensure package directory exists
  if (!fs.existsSync(packageDir)) {
    fs.mkdirSync(packageDir, { recursive: true });
  }

  // Group attributes by top-level prefix
  const domainGroups = groupByTopLevelPrefix(data.attributes.map(a => a.value));

  // Generate one file per domain (attributes only, no enums)
  for (const [domain, attrs] of domainGroups) {
    const fileName = `${prefixToClassName(domain)}.g.cs`;
    const filePath = path.join(packageDir, fileName);

    const content = generateCSharpForDomain(data, domain, attrs, new Map());
    fs.writeFileSync(filePath, content);
    console.log(`  ✓ ${fileName}`);
  }

  // Generate single Enums file with ALL enum values (no duplicates)
  const enumsContent = generateCSharpEnumsFile(data);
  fs.writeFileSync(path.join(packageDir, "Enums.g.cs"), enumsContent);
  console.log(`  ✓ Enums.g.cs`);

  // Generate UTF-8 file (net8.0+ only, uses ReadOnlySpan<byte>)
  const utf8Content = generateCSharpUtf8ForPackage(data);
  fs.writeFileSync(path.join(packageDir, "Utf8.g.cs"), utf8Content);
  console.log(`  ✓ Utf8.g.cs`);

  console.log(`✓ Generated OTelConventions package (${domainGroups.size + 2} files)`);
}

function generateCSharpEnumsFile(data: ParsedData): string {
  const lines: string[] = [
    `// <auto-generated/>`,
    `// Generated from @opentelemetry/semantic-conventions v${data.version}`,
    `// Do not edit manually - run 'npm run generate:package' in SemconvGenerator`,
    ``,
    `namespace ${CONFIG.packageNamespace};`,
    ``,
  ];

  // Generate all enum value classes
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

function groupByTopLevelPrefix(values: string[]): Map<string, string[]> {
  const groups = new Map<string, string[]>();
  for (const v of values) {
    const parts = v.split(".");
    // Use first part as domain (gen_ai, http, db, etc.)
    const prefix = parts[0];
    const group = groups.get(prefix) || [];
    group.push(v);
    groups.set(prefix, group);
  }
  return groups;
}

function generateCSharpForDomain(
  data: ParsedData,
  domain: string,
  attrValues: string[],
  domainEnums: Map<string, EnumValue[]>
): string {
  const lines: string[] = [
    `// <auto-generated/>`,
    `// Generated from @opentelemetry/semantic-conventions v${data.version}`,
    `// Domain: ${domain}`,
    `// Do not edit manually - run 'npm run generate:package' in SemconvGenerator`,
    ``,
    `namespace ${CONFIG.packageNamespace};`,
    ``,
  ];

  // Group by full prefix (gen_ai.request, gen_ai.response, etc.)
  const grouped = groupByPrefix(attrValues);

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

  // Enum values for this domain
  for (const [prefix, values] of domainEnums) {
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

function generateCSharpUtf8ForPackage(data: ParsedData): string {
  const lines: string[] = [
    `// <auto-generated/>`,
    `// Generated from @opentelemetry/semantic-conventions v${data.version}`,
    `// Do not edit manually - run 'npm run generate:package' in SemconvGenerator`,
    `//`,
    `// UTF-8 ReadOnlySpan<byte> for zero-allocation OTLP parsing hot paths`,
    `// Requires .NET 8.0+ (uses u8 string literals)`,
    ``,
    `#if NET8_0_OR_GREATER`,
    ``,
    `using System;`,
    ``,
    `namespace ${CONFIG.packageNamespace};`,
    ``,
  ];

  // Group attributes by full prefix
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

  // Close #if NET8_0_OR_GREATER
  lines.push(`#endif`);

  return lines.join("\n");
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
  const sqlOnly = args.includes("--sql-only");
  const packageOnly = args.includes("--package");
  const generateAll = !tsOnly && !csOnly && !utf8Only && !sqlOnly && !packageOnly;

  // Optional overrides
  const namespaceOverride = parseArg(args, "namespace");
  const outputOverride = parseArg(args, "output");

  console.log("Parsing @opentelemetry/semantic-conventions...");
  const data = parse();

  // Ensure output directory exists
  const outputDir = path.join(__dirname, "output");
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }

  if (generateAll || tsOnly) {
    const ts = generateTypeScript(data);
    const tsPath = path.join(__dirname, CONFIG.outputs.typescript);
    fs.mkdirSync(path.dirname(tsPath), { recursive: true });
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
    fs.mkdirSync(path.dirname(csPath), { recursive: true });
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
    fs.mkdirSync(path.dirname(utf8Path), { recursive: true });
    fs.writeFileSync(utf8Path, utf8);
    console.log(`✓ Generated ${utf8Path.replace(__dirname + "/", "")}`);

    // Restore
    CONFIG.csharpNamespace = originalNamespace;
  }

  if (generateAll || sqlOnly) {
    const sql = generateDuckDb(data);
    const sqlPath = path.join(__dirname, CONFIG.outputs.duckdb);
    fs.mkdirSync(path.dirname(sqlPath), { recursive: true });
    fs.writeFileSync(sqlPath, sql);
    console.log(`✓ Generated ${CONFIG.outputs.duckdb}`);
  }

  if (packageOnly) {
    console.log("Generating OTelConventions package...");
    generatePackage(data);
  }

  console.log("Done!");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
