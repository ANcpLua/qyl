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
const TARGET = resolve(ROOT, "packages/Qyl.Client/Generated/src/Generated");

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

const patches = [
    patchNullableEnumCast,
    stripExperimentalAttribute,
    fixWrongQualification,
    addMissingUsings,
    patchRestClientNullableParams,
    addBclDisambiguationAliases,
];

function walk(dir, out = []) {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const full = join(dir, entry.name);
        if (entry.isDirectory()) walk(full, out);
        else if (entry.isFile() && entry.name.endsWith(".cs")) out.push(full);
    }
    return out;
}

if (!existsSync(TARGET)) {
    console.log(`patch-emitted-csharp: ${TARGET} not present, skipping`);
    process.exit(0);
}

const files = walk(TARGET);
let totalEdits = 0;

for (const file of files) {
    const original = readFileSync(file, "utf8");
    let updated = original;
    for (const p of patches) {
        updated = p(updated, file);
    }
    if (updated !== original) {
        writeFileSync(file, updated);
        totalEdits += 1;
    }
}

console.log(`patch-emitted-csharp: patched ${totalEdits} / ${files.length} file(s) under ${TARGET}`);
