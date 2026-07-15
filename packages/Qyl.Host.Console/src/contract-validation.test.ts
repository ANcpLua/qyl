import { describe, expect, it } from "vitest";
import { parseLogLine, parseResourceState } from "./contract-validation";

describe("runner SSE contract validation", () => {
  it("accepts only generated resource-state fields and bounds", () => {
    expect(parseResourceState({
      name: "collector",
      lifecycle: "ready",
      timestamp: "2026-07-15T09:00:00Z",
      allocatedPort: 5100,
      restarts: 0,
    })).toMatchObject({ name: "collector", lifecycle: "ready" });

    expect(() => parseResourceState({ name: "collector", lifecycle: "ready" })).toThrow(
      /Runner contract mismatch/u,
    );
    expect(() => parseResourceState({
      name: "collector",
      lifecycle: "ready",
      timestamp: "2026-07-15T09:00:00Z",
      allocatedPort: 0,
    })).toThrow(/Runner contract mismatch/u);
    expect(() => parseResourceState({
      name: "collector",
      lifecycle: "ready",
      timestamp: "2026-07-15T09:00:00Z",
      handwrittenAlias: true,
    })).toThrow(/Runner contract mismatch/u);
  });

  it("accepts generated log lines and rejects alternate DTO shapes", () => {
    expect(parseLogLine({ resource: "collector", stream: "out", line: "ready" })).toEqual({
      resource: "collector",
      stream: "out",
      line: "ready",
    });

    expect(() => parseLogLine({ resource: "collector", message: "ready" })).toThrow(
      /Runner contract mismatch/u,
    );
    expect(() => parseLogLine({
      resource: "collector",
      stream: "out",
      line: "ready",
      timestamp: "2026-07-15T09:00:00Z",
    })).toThrow(/Runner contract mismatch/u);
    expect(() => parseLogLine({ resource: "collector", stream: "stdout", line: "ready" })).toThrow(
      /Runner contract mismatch/u,
    );
  });
});
