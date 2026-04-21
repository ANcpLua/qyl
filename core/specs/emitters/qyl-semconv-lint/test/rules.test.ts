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
});

describe("QYL-LINT-002 bad-namespace", () => {
    it("rejects non-qyl. prefix", async () => {
        expect(await codes(`@qylAttr("acme.thing", "string") foo: string;`))
            .toContain(`${LIB}/bad-namespace`);
    });
    it("accepts qyl.* prefix", async () => {
        const cs = await codes(`@qylAttr("qyl.clean.key", "string") foo: string;`);
        expect(cs).not.toContain(`${LIB}/bad-namespace`);
        expect(cs).not.toContain(`${LIB}/upstream-collision`);
    });
});

describe("QYL-LINT-003 bad-naming", () => {
    it("rejects doubled dots", async () => {
        expect(await codes(`@qylAttr("qyl.foo..bar", "string") foo: string;`))
            .toContain(`${LIB}/bad-naming`);
    });
    it("rejects uppercase", async () => {
        expect(await codes(`@qylAttr("qyl.Foo", "string") foo: string;`))
            .toContain(`${LIB}/bad-naming`);
    });
    it("accepts underscores and digits", async () => {
        expect(await codes(`@qylAttr("qyl.storage.size_v2", "long") foo: int64;`))
            .not.toContain(`${LIB}/bad-naming`);
    });
});

describe("QYL-LINT-004 type-drift", () => {
    it("flags same key declared with different types", async () => {
        expect(await codes(`
            @qylAttr("qyl.drift.me", "string") a: string;
            @qylAttr("qyl.drift.me", "long") b: int64;
        `)).toContain(`${LIB}/type-drift`);
    });
    it("does not flag a single site", async () => {
        expect(await codes(`@qylAttr("qyl.single.site", "string") a: string;`))
            .not.toContain(`${LIB}/type-drift`);
    });
});

describe("QYL-LINT-005 stability-regression", () => {
    it("flags experimental after stable", async () => {
        expect(await codes(`
            @qylAttr("qyl.ratchet", "string", #{ stability: "stable" }) a: string;
            @qylAttr("qyl.ratchet", "string", #{ stability: "experimental" }) b: string;
        `)).toContain(`${LIB}/stability-regression`);
    });
    it("allows deprecated after stable", async () => {
        expect(await codes(`
            @qylAttr("qyl.retired", "string", #{ stability: "stable" }) a: string;
            @qylAttr("qyl.retired", "string", #{ stability: "deprecated" }) b: string;
        `)).not.toContain(`${LIB}/stability-regression`);
    });
});

describe("QYL-LINT-006 cardinality-drift", () => {
    it("warns when cardinality differs", async () => {
        expect(await codes(`
            @qylAttr("qyl.card.test", "string", #{ cardinality: "low" }) a: string;
            @qylAttr("qyl.card.test", "string", #{ cardinality: "high" }) b: string;
        `)).toContain(`${LIB}/cardinality-drift`);
    });
    it("silent when cardinality matches", async () => {
        expect(await codes(`
            @qylAttr("qyl.card.match", "string", #{ cardinality: "low" }) a: string;
            @qylAttr("qyl.card.match", "string", #{ cardinality: "low" }) b: string;
        `)).not.toContain(`${LIB}/cardinality-drift`);
    });
});
