import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { vi } from "vitest";

export class FakeEventSource {
  static instances: FakeEventSource[] = [];

  readonly url: string;
  readonly close = vi.fn();
  onopen: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;

  constructor(url: string | URL) {
    this.url = String(url);
    FakeEventSource.instances.push(this);
  }

  emitMessage(data: string): void {
    this.onmessage?.(new MessageEvent("message", { data }));
  }
}

export function enableReactActEnvironment(): void {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
}

export interface MountedHook<T> {
  current(): T;
  renders(): number;
  unmount(): void;
}

export function mountHook<T>(hook: () => T): MountedHook<T> {
  const container = document.createElement("div");
  const root: Root = createRoot(container);
  let value: T | undefined;
  let renderCount = 0;

  function Probe() {
    value = hook();
    renderCount++;
    return null;
  }

  act(() => root.render(<Probe />));

  return {
    current() {
      if (value === undefined) throw new Error("Hook did not render");
      return value;
    },
    renders: () => renderCount,
    unmount: () => act(() => root.unmount()),
  };
}

export async function flushAsyncWork(): Promise<void> {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
}

export async function waitFor(condition: () => boolean): Promise<void> {
  for (let attempt = 0; attempt < 100; attempt++) {
    if (condition()) return;
    await flushAsyncWork();
  }
  throw new Error("Condition did not become true");
}
