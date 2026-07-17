// @vitest-environment jsdom

import { act } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  enableReactActEnvironment,
  FakeEventSource,
  mountHook,
  waitFor,
} from "./stream-hooks.test-utils";
import { useLogs } from "./useLogs";
import { useResources } from "./useResources";

describe("runner stream contract failures", () => {
  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
    enableReactActEnvironment();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("closes malformed streams and exposes their generated-contract failures", async () => {
    const resources = mountHook(useResources);
    const logs = mountHook(() => useLogs("collector"));
    await waitFor(() => FakeEventSource.instances.length === 2);

    const resourceSource = FakeEventSource.instances.find(
      (source) => source.url === "/runner/resources/stream",
    );
    const logSource = FakeEventSource.instances.find(
      (source) => source.url === "/runner/resources/collector/logs/stream",
    );
    expect(resourceSource).toBeDefined();
    expect(logSource).toBeDefined();

    act(() => {
      resourceSource?.emitMessage(JSON.stringify({ name: "collector", lifecycle: "ready" }));
      logSource?.emitMessage(JSON.stringify({ resource: "collector", message: "invalid" }));
    });

    expect(resourceSource?.close).toHaveBeenCalledOnce();
    expect(logSource?.close).toHaveBeenCalledOnce();
    expect(resources.current().connection).toBe("failed");
    expect(resources.current().error).toMatch(/Resource stream contract failure: Runner contract mismatch/u);
    expect(logs.current().error).toMatch(/Log stream contract failure: Runner contract mismatch/u);

    resources.unmount();
    logs.unmount();
  });
});
