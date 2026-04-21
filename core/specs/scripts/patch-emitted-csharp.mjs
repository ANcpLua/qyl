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

// Known alpha bug: `(int)<nullableEnum>?` is emitted where `(int?)<nullableEnum>`
// is meant. The convenience overload takes `SomeEnum?` and must forward an `int?`
// to the protocol overload. The alpha emitter places the `?` after the operand
// instead of inside the cast, producing invalid C#.
//
// Matches `(int)IDENT?` only when followed by `,` or a closing `)` — that scopes
// the rewrite to call-site arguments and avoids touching the (legal) null-propagating
// member-access pattern `(int)foo?.Bar`.
const patches = [
    {
        name: "cast-nullable-enum-to-int",
        pattern: /\(int\)(\w+)\?(?=\s*[,)])/g,
        replacement: "(int?)$1",
    },
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
        updated = updated.replace(p.pattern, p.replacement);
    }
    if (updated !== original) {
        writeFileSync(file, updated);
        totalEdits += 1;
    }
}

console.log(`patch-emitted-csharp: patched ${totalEdits} file(s) under ${TARGET}`);
