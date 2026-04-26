// Copyright (c) 2025-2026 ancplua
// SPDX-License-Identifier: MIT

import { describe, it, expect } from "vitest";
import { Tester } from "./test-host.js";

const LIB = "@qyl/typespec-qyl-semconv-lint";

async function codes(body: string): Promise<string[]> {
    const ds = await Tester.diagnose(`using Qyl.Semconv; model X {\n${body}\n}`);
    return ds.map((d) => d.code);
}

describe("QYL-LINT-001 upstream-collision", () => {
    it("rejects gen_ai.*", async () => {
        expect(await codes(`@qylAttr("gen_ai.foo", "string") foo: string;`))
            .toContain(`${LIB}/upstream-collision`);
    });
    it("rejects http.*", async () => {
        expect(await codes(`@qylAttr("http.custom", "string") foo: string;`))
            .toContain(`${LIB}/upstream-collision`);
    });
    it("accepts qyl.* prefix", async () => {
        expect(await codes(`@qylAttr("qyl.clean.key", "string") foo: string;`))
            .not.toContain(`${LIB}/upstream-collision`);
    });
});
