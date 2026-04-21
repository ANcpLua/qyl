// Copyright (c) 2025-2026 ancplua
// SPDX-License-Identifier: MIT
//
// Rule bodies land in Commit 13. Commit 12 only needs a module that exports
// `runAllRules(program)` so the scaffold compiles and `$onValidate` can be
// wired from day one. Do not delete this file when Commit 13 lands — replace
// the stub body with the real implementation.

import type { Program } from "@typespec/compiler";

export function runAllRules(_program: Program): void {
    // Intentional no-op until Commit 13 lands the 6 rule implementations.
}
