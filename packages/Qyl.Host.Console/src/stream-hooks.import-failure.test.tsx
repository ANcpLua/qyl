// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  enableReactActEnvironment,
  FakeEventSource,
  flushAsyncWork,
  mountHook,
} from "./stream-hooks.test-utils";
import { useLogs } from "./useLogs";
import { useResources } from "./useResources";

vi.mock("./contract-validation", () => {
  throw new Error("validator chunk unavailable");
});

describe("runner stream validation loading", () => {
  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
    enableReactActEnvironment();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("surfaces lazy validation import failures without opening streams", async () => {
    const resources = mountHook(useResources);
    const logs = mountHook(() => useLogs("collector"));

    await flushAsyncWork();

    expect(resources.current()).toMatchObject({
      connection: "failed",
    });
    expect(resources.current().error).toMatch(/^Unable to load resource stream validation:/u);
    expect(logs.current().error).toMatch(/^Unable to load log stream validation:/u);
    expect(FakeEventSource.instances).toHaveLength(0);

    resources.unmount();
    logs.unmount();
  });
});
