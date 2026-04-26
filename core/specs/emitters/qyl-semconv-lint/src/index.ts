// Copyright (c) 2025-2026 ancplua
// SPDX-License-Identifier: MIT

import {
    createTypeSpecLibrary,
    paramMessage,
    setTypeSpecNamespace,
    type DecoratorContext,
    type Program,
    type Type,
} from "@typespec/compiler";
import { runAllRules } from "./rules.js";

export const $lib = createTypeSpecLibrary({
    name: "@qyl/typespec-qyl-semconv-lint",
    diagnostics: {
        "upstream-collision": {
            severity: "error",
            messages: {
                default: paramMessage`QYL-LINT-001: attribute '${"key"}' collides with upstream OTel namespace '${"prefix"}' — qyl attributes must live under 'qyl.'`,
            },
        },
    },
    state: {
        qylAttr: { description: "Collected qyl attribute declarations (populated by @qylAttr)" },
    },
} as const);

export const { reportDiagnostic, createDiagnostic, stateKeys } = $lib;

export type QylAttrPrimitive = "string" | "int" | "long" | "double" | "boolean" | "string[]";
export type QylAttrCardinality = "low" | "medium" | "high";
export type QylAttrStability = "experimental" | "stable" | "deprecated";

export interface QylAttrOptions {
    cardinality?: QylAttrCardinality;
    stability?: QylAttrStability;
    required?: boolean;
}

export interface QylAttrRecord {
    key: string;
    type: QylAttrPrimitive;
    cardinality?: QylAttrCardinality;
    stability?: QylAttrStability;
    required?: boolean;
    target: Type;
}

/**
 * Decorator implementation for `@qylAttr`. Records the annotation in a state
 * map keyed by target symbol; `$onValidate` enumerates the map and runs the
 * rule set once per compile.
 */
export function $qylAttr(
    context: DecoratorContext,
    target: Type,
    key: string,
    type: QylAttrPrimitive,
    options?: QylAttrOptions,
): void {
    const map = context.program.stateMap(stateKeys.qylAttr);
    const bucket = (map.get(target) as QylAttrRecord[] | undefined) ?? [];
    bucket.push({
        key,
        type,
        cardinality: options?.cardinality,
        stability: options?.stability,
        required: options?.required,
        target,
    });
    map.set(target, bucket);
}

// Bind the decorator implementation to the `Qyl.Semconv` namespace declared in lib/main.tsp.
// Without this, TypeSpec auto-registers `$qylAttr` at global scope and the fixture's
// `using Qyl.Semconv;` then finds the decorator in two scopes → `ambiguous-symbol`.
setTypeSpecNamespace("Qyl.Semconv", $qylAttr);

/**
 * Compiler-invoked validator. Runs AFTER all decorators have applied, so the
 * state map is complete. Emits diagnostics QYL-LINT-001..006.
 */
export function $onValidate(program: Program): void {
    runAllRules(program);
}
