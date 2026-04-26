#!/usr/bin/env node
// Copyright (c) 2025-2026 ancplua
// SPDX-License-Identifier: MIT
//
// Post-emit patches for @typespec/http-client-csharp@1.0.0-alpha.*.
// Each entry targets a known alpha-emitter bug and documents why it exists.
//
// When upstream ships a fix, delete the corresponding patch rather than patching
// around it — drift in node_modules compounds quickly.

import { readdirSync, readFileSync, writeFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, "../../..");

const CLIENT_TARGET = resolve(ROOT, "packages/Qyl.Client/Generated/src/Generated");
const SERVER_TARGET = resolve(ROOT, "services/qyl.collector/Generated/generated");

/**
 * Each patch is a function that takes (content, filePath) and returns the updated
 * content. Return the original unchanged when the bug isn't present. Order matters
 * for the BCL-collision aliases: we add the type aliases after the last `using`
 * directive, so they must run after `addUsing`.
 */

// Patch 1 — `(int)<nullableEnum>?` emitted where `(int?)<nullableEnum>` is meant.
//   Matches `(int)IDENT?` only when followed by `,` or a closing `)` — that scopes
//   the rewrite to call-site arguments and avoids touching the (legal) null-propagating
//   member-access pattern `(int)foo?.Bar`.
function patchNullableEnumCast(content) {
    return content.replace(/\(int\)(\w+)\?(?=\s*[,)])/g, "(int?)$1");
}

// Patch 2 — strip `[Experimental("SCME0002")]` attribute usages.
//   The emitter resolves `ExperimentalAttribute` to the internal one in
//   System.ClientModel.Primitives; from outside that assembly it's inaccessible.
//   The attribute is consumer-advisory only; removing it has no runtime effect.
function stripExperimentalAttribute(content) {
    return content.replace(/^\s*\[Experimental\("SCME\d+"\)\]\r?\n/gm, "");
}

// Patch 3 — fix wrong namespace qualification `Domains.Observe.Log.{AggregationFunction,TimeBucket}`.
//   The convenience model-factory emits these types under the consumer's namespace
//   even though they live in Qyl.OTel.Metrics and Qyl.Common.Pagination respectively.
//   The file already `using`s both; strip the wrong qualifier.
function fixWrongQualification(content) {
    return content
        .replace(/\bDomains\.Observe\.Log\.AggregationFunction\b/g, "AggregationFunction")
        .replace(/\bDomains\.Observe\.Log\.TimeBucket\b/g, "TimeBucket");
}

// Patch 4 — add missing cross-namespace `using` directives.
//   The emitter doesn't add `using Qyl.OTel.Metrics;` / `using Qyl.Common.Pagination;`
//   to files outside those namespaces that reference AggregationFunction / TimeBucket.
function addMissingUsings(content) {
    const referencesAgg = /\bAggregationFunction\b/.test(content) && !/^namespace Qyl\.OTel\.Metrics\b/m.test(content);
    const referencesBucket = /\bTimeBucket\b/.test(content) && !/^namespace Qyl\.Common\.Pagination\b/m.test(content);
    if (!referencesAgg && !referencesBucket) return content;

    const needsAgg = referencesAgg && !/^using Qyl\.OTel\.Metrics;/m.test(content);
    const needsBucket = referencesBucket && !/^using Qyl\.Common\.Pagination;/m.test(content);
    if (!needsAgg && !needsBucket) return content;

    const extras = [];
    if (needsAgg) extras.push("using Qyl.OTel.Metrics;");
    if (needsBucket) extras.push("using Qyl.Common.Pagination;");

    // Insert after the last existing `using` line.
    const lines = content.split("\n");
    let lastUsing = -1;
    for (let i = 0; i < lines.length; i++) {
        if (/^using\s+[\w.]+;/.test(lines[i])) lastUsing = i;
    }
    if (lastUsing === -1) return content;
    lines.splice(lastUsing + 1, 0, ...extras);
    return lines.join("\n");
}

// Patch 5 — fix non-nullable parameter signatures on REST client helpers.
//   `CreateGetAllRequest(... int severityMin, int severityMax ...)` is wrong — the
//   body already does `if (severityMin != null)` null-checks. The convenience overload
//   calls it with `int?` arguments, triggering a CS1503 cast error.
function patchRestClientNullableParams(content, filePath) {
    if (filePath.endsWith("LogsApi.RestClient.cs")) {
        return content.replace(
            /CreateGetAllRequest\(string serviceName, int severityMin, int severityMax,/,
            "CreateGetAllRequest(string serviceName, int? severityMin, int? severityMax,"
        );
    }
    if (filePath.endsWith("TracesApi.RestClient.cs")) {
        return content.replace(
            /CreateGetAllRequest\(string serviceName, long\? minDurationMs, long\? maxDurationMs, int status,/,
            "CreateGetAllRequest(string serviceName, long? minDurationMs, long? maxDurationMs, int? status,"
        );
    }
    return content;
}

// Patch 6 — disambiguate BCL collisions (System.Attribute vs Qyl.Common.Attribute,
//   System.Diagnostics.Trace vs Qyl.OTel.Traces.Trace). We add `using` ALIASES right
//   after the last existing using block so the qyl types win locally.
function addBclDisambiguationAliases(content) {
    const needsTrace = /\bTrace\b/.test(content) && /^using Qyl\.OTel\.Traces;/m.test(content)
        && !/^using Trace\s*=/m.test(content);
    const needsAttribute = /typeof\(Attribute\)/.test(content) && /^using Qyl\.Common;/m.test(content)
        && !/^using Attribute\s*=/m.test(content);
    if (!needsTrace && !needsAttribute) return content;

    const aliases = [];
    if (needsTrace) aliases.push("using Trace = Qyl.OTel.Traces.Trace;");
    if (needsAttribute) aliases.push("using Attribute = Qyl.Common.Attribute;");

    const lines = content.split("\n");
    let lastUsing = -1;
    for (let i = 0; i < lines.length; i++) {
        if (/^using\s+[\w.]+;/.test(lines[i])) lastUsing = i;
    }
    if (lastUsing === -1) return content;
    lines.splice(lastUsing + 1, 0, ...aliases);
    return lines.join("\n");
}

// Patch 7 — http-server-csharp omits cross-namespace `using` directives in controller +
//   operation files. Add a blanket set of Qyl.* usings so any referenced type resolves.
//   Only applies to files under `operations/` or `controllers/` (the domain models already
//   know their own namespaces). Unused usings are harmless warnings.
const QYL_UMBRELLA_USINGS = [
    "using Qyl.Api;",
    "using Qyl.Common;",
    "using Qyl.Common.Errors;",
    "using Qyl.Common.Pagination;",
    "using Qyl.Domains.AI.GenAi;",
    "using Qyl.Domains.Agent.Checkpoint;",
    "using Qyl.Domains.Agent.Run;",
    "using Qyl.Domains.Agent.ToolCall;",
    "using Qyl.Domains.Agent.Workflow;",
    "using Qyl.Domains.Alerting;",
    "using Qyl.Domains.Configurator;",
    "using Qyl.Domains.Data.Db;",
    "using Qyl.Domains.Identity;",
    "using Qyl.Domains.Issues;",
    "using Qyl.Domains.Loom.Triage;",
    "using Qyl.Domains.Observe.Error;",
    "using Qyl.Domains.Observe.Log;",
    "using Qyl.Domains.Observe.Otel;",
    "using Qyl.Domains.Observe.Session;",
    "using Qyl.Domains.Observe.Test;",
    "using Qyl.Domains.Ops.Deployment;",
    "using Qyl.Domains.Ops.Retention;",
    "using Qyl.Domains.Runtime.System;",
    "using Qyl.Domains.Search;",
    "using Qyl.Domains.Transport.Http;",
    "using Qyl.Domains.Transport.Messaging;",
    "using Qyl.Domains.Transport.Rpc;",
    "using Qyl.Domains.Workflow;",
    "using Qyl.Domains.Workspace;",
    "using Qyl.Intelligence;",
    "using Qyl.OTel.Enums;",
    "using Qyl.OTel.Logs;",
    "using Qyl.OTel.Metrics;",
    "using Qyl.OTel.Profiles;",
    "using Qyl.OTel.Resource;",
    "using Qyl.OTel.Traces;",
    "using Qyl.Storage;",
];

function addServerSideUsings(content, filePath) {
    if (!/\/(operations|controllers)\//.test(filePath)) return content;
    const lines = content.split("\n");
    let lastUsing = -1;
    for (let i = 0; i < lines.length; i++) {
        if (/^using\s+[\w.=]+(\s*=\s*[\w.]+)?;/.test(lines[i])) lastUsing = i;
    }
    if (lastUsing === -1) return content;
    const existing = new Set(
        lines.filter((l) => /^using\s+Qyl\./.test(l)).map((l) => l.trim()),
    );
    const missing = QYL_UMBRELLA_USINGS.filter((u) => !existing.has(u));
    if (missing.length === 0) return content;
    lines.splice(lastUsing + 1, 0, ...missing);
    return lines.join("\n");
}

// Patch 8 — CS1009 on raw regex patterns. http-server-csharp interpolates JSON-Schema
//   `pattern` strings into regular C# string literals (`"..."`), producing invalid
//   escape sequences like `\d`, `\-`, `\_`. Convert to verbatim string literals (`@"..."`)
//   where the pattern contains a backslash. Any embedded double-quotes get doubled per
//   verbatim-literal rules — our semconv patterns don't contain `"`, so the simple
//   transform is safe.
function fixRegexPatterns(content) {
    return content.replace(
        /(\[StringConstraint\(Pattern = )"([^"]*\\[^"]*)"(\)\])/g,
        (_, prefix, body, suffix) => `${prefix}@"${body}"${suffix}`,
    );
}

// Patch 9 — server-side BCL disambiguation for ambiguous types across multiple domain
//   namespaces. `ErrorStats` exists in both `Qyl.Collector.Errors.ErrorStats` (hand-written)
//   and `Qyl.Domains.Observe.Error.ErrorStats` (emitted); `IssueStatus` exists in both
//   `Qyl.Domains.Issues.IssueStatus` and `Qyl.Contracts.Loom.IssueStatus`. In both cases
//   the emitted interface wants the TypeSpec-emitted one. Add a using alias.
function addServerSideDisambiguation(content, filePath) {
    if (!/\/(operations|controllers)\//.test(filePath)) return content;

    const aliases = [];
    // Trace: collides with System.Diagnostics.Trace on operations referencing OTel traces.
    if (/\bTrace\b/.test(content) && !/^using Trace\s*=/m.test(content)
        && /TracesApi|CursorPageTrace/.test(content)) {
        aliases.push("using Trace = Qyl.OTel.Traces.Trace;");
    }
    // IssueStatus: collides between Qyl.Domains.Issues (canonical) and Qyl.Contracts.Loom.
    if (/IssuesApi/.test(filePath) && !/^using IssueStatus\s*=/m.test(content)) {
        aliases.push("using IssueStatus = Qyl.Domains.Issues.IssueStatus;");
    }
    // ErrorStats: collides between hand-written Qyl.Collector.Errors and emitted Qyl.Domains.Observe.Error.
    if (/ErrorsApi/.test(filePath) && !/^using ErrorStats\s*=/m.test(content)) {
        aliases.push("using ErrorStats = Qyl.Domains.Observe.Error.ErrorStats;");
    }
    // LogStats: two TypeSpec models with the same name; the operation references the OTel one.
    if (/LogsApi/.test(filePath) && !/^using LogStats\s*=/m.test(content)
        && /\bLogStats\b/.test(content)) {
        aliases.push("using LogStats = Qyl.OTel.Logs.LogStats;");
    }
    if (!aliases.length) return content;

    const lines = content.split("\n");
    let lastUsing = -1;
    for (let i = 0; i < lines.length; i++) {
        if (/^using\s+[\w.=]+(\s*=\s*[\w.]+)?;/.test(lines[i])) lastUsing = i;
    }
    if (lastUsing === -1) return content;
    lines.splice(lastUsing + 1, 0, ...aliases);
    return lines.join("\n");
}

const clientPatches = [
    patchNullableEnumCast,
    stripExperimentalAttribute,
    fixWrongQualification,
    addMissingUsings,
    patchRestClientNullableParams,
    addBclDisambiguationAliases,
];

// Patch 8 — fully-qualify `Resource` property declarations in model files that
//   `using Qyl.OTel.Resource;`. The alpha emitter collides the class name `Resource`
//   with the namespace `Qyl.OTel.Resource`, and the namespace import wins when the
//   naked identifier is resolved — `error CS0118: 'Resource' is a namespace but is
//   used like a type`. A `using Resource = Qyl.OTel.Resource.Resource;` alias does
//   not override the namespace import, so we fully qualify the property type instead.
//   Known consumers: Metric.cs, Span.cs, LogRecord.cs.
function fullyQualifyResourceProperty(content, filePath) {
    if (!/\/models\//.test(filePath)) return content;
    if (!/^using Qyl\.OTel\.Resource;$/m.test(content)) return content;
    return content.replace(/\bpublic Resource Resource\b/g, "public Qyl.OTel.Resource.Resource Resource");
}

// Patch 9 — add a `using Trace = Qyl.OTel.Traces.Trace;` alias in model files that
//   reference the OTel `Trace` type. Collides with `System.Diagnostics.Trace` →
//   `error CS0104: 'Trace' is an ambiguous reference`. Unlike the Resource collision,
//   the alias resolves this one because there is no namespace-import precedence to beat.
//   Known consumers: TraceStreamEvent.cs, CursorPageTrace.cs.
function addTraceAliasInModels(content, filePath) {
    if (!/\/models\//.test(filePath)) return content;
    if (!/^using Qyl\.OTel\.Traces;$/m.test(content)) return content;
    if (/^using Trace\s*=/m.test(content)) return content;
    const declaresTrace = /\bpublic\s+Trace\s+\w+\b/.test(content);
    const isCursorPageTrace = /CursorPageTrace\.cs$/.test(filePath);
    if (!declaresTrace && !isCursorPageTrace) return content;
    return content.replace(
        /^using Qyl\.OTel\.Traces;$/m,
        "using Qyl.OTel.Traces;\nusing Trace = Qyl.OTel.Traces.Trace;",
    );
}

const serverPatches = [
    fixRegexPatterns,
    addServerSideUsings,
    addServerSideDisambiguation,
    fullyQualifyResourceProperty,
    addTraceAliasInModels,
];

function walk(dir, out = []) {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const full = join(dir, entry.name);
        if (entry.isDirectory()) walk(full, out);
        else if (entry.isFile() && entry.name.endsWith(".cs")) out.push(full);
    }
    return out;
}

function applyPatches(target, patches) {
    if (!existsSync(target)) {
        console.log(`patch-emitted-csharp: ${target} not present, skipping`);
        return;
    }
    const files = walk(target);
    let edits = 0;
    for (const file of files) {
        const original = readFileSync(file, "utf8");
        let updated = original;
        for (const p of patches) updated = p(updated, file);
        if (updated !== original) {
            writeFileSync(file, updated);
            edits += 1;
        }
    }
    console.log(`patch-emitted-csharp: patched ${edits} / ${files.length} file(s) under ${target}`);
}

applyPatches(CLIENT_TARGET, clientPatches);
applyPatches(SERVER_TARGET, serverPatches);
