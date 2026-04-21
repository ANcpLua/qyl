// Copyright (c) 2025-2026 ancplua
// SPDX-License-Identifier: MIT

import type { Program } from "@typespec/compiler";
import {
    reportDiagnostic,
    stateKeys,
    type QylAttrCardinality,
    type QylAttrRecord,
} from "./index.js";
import { RESERVED_PREFIXES, lookupUpstream } from "./registry.js";

/**
 * Flatten the per-target state map buckets into a single array for cross-site
 * rules (type drift, cardinality drift, stability regression).
 */
function collectAll(program: Program): QylAttrRecord[] {
    const map = program.stateMap(stateKeys.qylAttr);
    const out: QylAttrRecord[] = [];
    for (const bucket of map.values()) {
        for (const rec of bucket as QylAttrRecord[]) out.push(rec);
    }
    return out;
}

// Rule 001 / 002 — namespace ownership.
//   001: must NOT start with a reserved OTel prefix (gen_ai., http., …)
//   002: must start with `qyl.`
function checkNamespace(program: Program, records: readonly QylAttrRecord[]): void {
    for (const r of records) {
        const collision = RESERVED_PREFIXES.find((p) => r.key.startsWith(p));
        if (collision) {
            reportDiagnostic(program, {
                code: "upstream-collision",
                target: r.target,
                format: { key: r.key, prefix: collision.slice(0, -1) },
            });
            continue;
        }
        if (!r.key.startsWith("qyl.")) {
            reportDiagnostic(program, {
                code: "bad-namespace",
                target: r.target,
                format: { key: r.key },
            });
        }
    }
}

// Rule 003 — OTel naming convention.
//   lowercase letters / digits / underscores, dot-separated segments, no
//   leading or trailing dots, no doubled dots.
const NAMING_RE = /^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$/;

function checkNaming(program: Program, records: readonly QylAttrRecord[]): void {
    for (const r of records) {
        if (!NAMING_RE.test(r.key)) {
            reportDiagnostic(program, {
                code: "bad-naming",
                target: r.target,
                format: { key: r.key },
            });
        }
    }
}

// Rule 004 — type consistency across sites, and against upstream registry.
function checkTypeConsistency(program: Program, records: readonly QylAttrRecord[]): void {
    const byKey = new Map<string, QylAttrRecord[]>();
    for (const r of records) {
        const bucket = byKey.get(r.key) ?? [];
        bucket.push(r);
        byKey.set(r.key, bucket);
    }

    for (const [key, sites] of byKey) {
        const types = new Set(sites.map((s) => s.type));

        // Cross-site drift: same key declared with two different primitives.
        if (types.size > 1) {
            const [first, ...rest] = sites;
            for (const other of rest) {
                if (other.type !== first.type) {
                    reportDiagnostic(program, {
                        code: "type-drift",
                        target: other.target,
                        format: {
                            key,
                            typeA: other.type,
                            typeB: first.type,
                            otherSite: "another @qylAttr site",
                        },
                    });
                }
            }
        }

        // Defense-in-depth: if the key still somehow matches upstream (e.g.
        // a future reserved prefix that slips past the hard list), flag the
        // type mismatch against upstream.
        const upstream = lookupUpstream(key);
        if (upstream) {
            for (const s of sites) {
                if (s.type !== upstream.type) {
                    reportDiagnostic(program, {
                        code: "type-drift",
                        target: s.target,
                        format: {
                            key,
                            typeA: s.type,
                            typeB: upstream.type,
                            otherSite: `upstream OTel registry (group ${upstream.group})`,
                        },
                    });
                }
            }
        }
    }
}

// Rule 005 — stability one-way ratchet.
//   Once any site declares `stable`, no later site may regress to `experimental`.
//   `deprecated` is allowed after `stable` (that's how deprecation works).
function checkStabilityConsistency(program: Program, records: readonly QylAttrRecord[]): void {
    const byKey = new Map<string, QylAttrRecord[]>();
    for (const r of records) {
        if (!r.stability) continue;
        const bucket = byKey.get(r.key) ?? [];
        bucket.push(r);
        byKey.set(r.key, bucket);
    }

    for (const sites of byKey.values()) {
        if (!sites.some((s) => s.stability === "stable")) continue;
        for (const s of sites) {
            if (s.stability === "experimental") {
                reportDiagnostic(program, {
                    code: "stability-regression",
                    target: s.target,
                    format: { key: s.key, prior: "stable", current: s.stability },
                });
            }
        }
    }
}

// Rule 006 — cardinality consistency (warning).
function checkCardinalityConsistency(program: Program, records: readonly QylAttrRecord[]): void {
    const byKey = new Map<string, QylAttrRecord[]>();
    for (const r of records) {
        if (!r.cardinality) continue;
        const bucket = byKey.get(r.key) ?? [];
        bucket.push(r);
        byKey.set(r.key, bucket);
    }

    for (const sites of byKey.values()) {
        const unique = Array.from(new Set<QylAttrCardinality>(sites.map((s) => s.cardinality!)));
        if (unique.length > 1) {
            const site = sites[0]!;
            reportDiagnostic(program, {
                code: "cardinality-drift",
                target: site.target,
                format: { key: site.key, a: unique[0]!, b: unique[1]! },
            });
        }
    }
}

export function runAllRules(program: Program): void {
    const records = collectAll(program);
    if (records.length === 0) return;

    checkNamespace(program, records);
    checkNaming(program, records);
    checkTypeConsistency(program, records);
    checkStabilityConsistency(program, records);
    checkCardinalityConsistency(program, records);
}
