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

const validationModule = vi.hoisted(() => {
  let release = () => {};
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { pending, release: () => release() };
});

vi.mock("./contract-validation", async () => {
  await validationModule.pending;
  return {
    parseLogLine: vi.fn(),
    parseResourceState: vi.fn(),
  };
});

describe("runner stream cancellation", () => {
  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
    enableReactActEnvironment();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("does not create streams or mutate hook state when validation resolves after unmount", async () => {
    const resources = mountHook(useResources);
    const logs = mountHook(() => useLogs("collector"));
    const resourcesBeforeUnmount = resources.current();
    const logsBeforeUnmount = logs.current();
    const resourceRenders = resources.renders();
    const logRenders = logs.renders();

    resources.unmount();
    logs.unmount();
    validationModule.release();
    await flushAsyncWork();

    expect(FakeEventSource.instances).toHaveLength(0);
    expect(resources.current()).toBe(resourcesBeforeUnmount);
    expect(logs.current()).toBe(logsBeforeUnmount);
    expect(resources.renders()).toBe(resourceRenders);
    expect(logs.renders()).toBe(logRenders);
  });
});
