import { z } from "zod";

const permitCounterSchema = z.object({
  available: z.number().int().nonnegative(),
  total: z.number().int().positive(),
});

export const providerModelLimitsSchema = z.record(
  z.string(),
  z.object({
    maxConcurrent: z.number().int().positive(),
    maxQueued: z.number().int().nonnegative(),
  }),
);

export type ConcurrencyKey = `${string}/${string}`;

export type PermitCounter = z.infer<typeof permitCounterSchema>;
export type ProviderModelLimits = z.infer<typeof providerModelLimitsSchema>;

export type ConcurrencyLimitsInput = ProviderModelLimits;

export const boundedAutonomySchema = z.object({
  maxDepth: z.number().int().positive(),
  maxRootSessionSpawnBudget: z.number().int().nonnegative(),
  maxDescendantsPerSession: z.number().int().nonnegative(),
  rootSessionSpawnBudgets: z.record(
    z.string().min(1),
    z.number().int().nonnegative(),
  ).optional(),
  rootSessionId: z.string().min(1).optional(),
});

export type BoundedAutonomyConfig = z.infer<typeof boundedAutonomySchema>;

export type SessionLineage = ReadonlyArray<string>;
export type RootSessionSpawnUsage = ReadonlyMap<string, number>;
export type DescendantUsage = ReadonlyMap<string, number>;

export type SubagentSpawnState = {
  lineages: ReadonlyMap<string, SessionLineage>;
  rootSessionSpawnUsage: RootSessionSpawnUsage;
  descendantUsage: DescendantUsage;
};

export const emptySpawnState: SubagentSpawnState = {
  lineages: new Map(),
  rootSessionSpawnUsage: new Map(),
  descendantUsage: new Map(),
};

export type SpawnBlockReason =
  | "depth_limit_exceeded"
  | "root_budget_exceeded"
  | "descendant_limit_exceeded"
  | "lineage_cycle"
  | "duplicate_session_id";

export type SpawnDecision =
  | { ok: false; reason: SpawnBlockReason; reasonDetail: string }
  | { ok: true; nextState: SubagentSpawnState; nextLineage: SessionLineage };

export const createRootLineage = (rootSessionId: string): SessionLineage => [rootSessionId];

export const appendLineage = (parentLineage: SessionLineage, childSessionId: string): SessionLineage => [
  ...parentLineage,
  childSessionId,
];

export const lineageDepth = (lineage: SessionLineage): number => lineage.length;

export const getLineageRoot = (lineage: SessionLineage): string | undefined => lineage[0];

export const hasCycle = (lineage: SessionLineage, childSessionId: string): boolean => {
  return lineage.includes(childSessionId);
};

export const getSessionDepth = (lineage: SessionLineage): number => {
  return Math.max(0, lineageDepth(lineage) - 1);
};

const cloneMap = <T>(input: ReadonlyMap<string, T>): Map<string, T> => {
  return new Map(Array.from(input.entries()));
};

const getUsage = (usage: ReadonlyMap<string, number>, key: string): number => {
  return usage.get(key) ?? 0;
};

const withBudget = (config: BoundedAutonomyConfig, rootSessionId: string): number => {
  return config.rootSessionSpawnBudgets?.[rootSessionId] ?? config.maxRootSessionSpawnBudget;
};

const lineageDepthAllowed = (config: BoundedAutonomyConfig, parentLineage: SessionLineage): boolean => {
  return lineageDepth(parentLineage) < config.maxDepth;
};

const rootBudgetAllowed = (
  config: BoundedAutonomyConfig,
  usage: SubagentSpawnState,
  rootSessionId: string,
): boolean => {
  const used = getUsage(usage.rootSessionSpawnUsage, rootSessionId);
  const budget = withBudget(config, rootSessionId);
  return used < budget;
};

const descendantsForAncestors = (lineage: SessionLineage): ReadonlyArray<string> => lineage;

const descendantsAllowed = (config: BoundedAutonomyConfig, usage: SubagentSpawnState, parentLineage: SessionLineage): boolean => {
  if (config.maxDescendantsPerSession <= 0) {
    return parentLineage.length === 0;
  }

  for (const sessionId of descendantsForAncestors(parentLineage)) {
    if (getUsage(usage.descendantUsage, sessionId) >= config.maxDescendantsPerSession) {
      return false;
    }
  }

  return true;
};

const incrementDescendants = (usage: DescendantUsage, parentLineage: SessionLineage): DescendantUsage => {
  const next = cloneMap(usage);
  for (const sessionId of descendantsForAncestors(parentLineage)) {
    const nextValue = getUsage(next, sessionId) + 1;
    next.set(sessionId, nextValue);
  }

  return next;
};

const registerLineage = (
  state: SubagentSpawnState,
  childSessionId: string,
  childLineage: SessionLineage,
): SubagentSpawnState => {
  const lineages = cloneMap(state.lineages);
  const root = getLineageRoot(childLineage);
  if (!root) {
    return {
      ...state,
      lineages,
    };
  }

  lineages.set(childSessionId, childLineage);

  const parentLineage = childLineage.slice(0, -1);
  const descendantUsage = incrementDescendants(state.descendantUsage, parentLineage);
  const rootSessionSpawnUsage = cloneMap(state.rootSessionSpawnUsage);
  rootSessionSpawnUsage.set(root, getUsage(rootSessionSpawnUsage, root) + 1);

  return {
    lineages,
    rootSessionSpawnUsage,
    descendantUsage,
  };
};

export const canSpawnSubagent = (
  config: BoundedAutonomyConfig,
  state: SubagentSpawnState,
  parentSessionId: string,
  parentLineage: SessionLineage,
  childSessionId: string,
): SpawnDecision => {
  if (state.lineages.has(childSessionId)) {
    return {
      ok: false,
      reason: "duplicate_session_id",
      reasonDetail: `Session ID '${childSessionId}' already exists in lineage tracking.`,
    };
  }

  if (parentLineage[parentLineage.length - 1] !== parentSessionId) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Parent lineage does not end at declared parent session.",
    };
  }

  if (hasCycle(parentLineage, childSessionId)) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Child session would create a lineage cycle.",
    };
  }

  if (!lineageDepthAllowed(config, parentLineage)) {
    return {
      ok: false,
      reason: "depth_limit_exceeded",
      reasonDetail: `Spawning would exceed maxDepth=${config.maxDepth}.`,
    };
  }

  const rootSessionId = getLineageRoot(parentLineage);
  if (!rootSessionId) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Parent lineage must be non-empty and identify a root session.",
    };
  }

  if (!rootBudgetAllowed(config, state, rootSessionId)) {
    return {
      ok: false,
      reason: "root_budget_exceeded",
      reasonDetail: `Root session '${rootSessionId}' exceeded budget of ${withBudget(config, rootSessionId)}.`,
    };
  }

  if (!descendantsAllowed(config, state, parentLineage)) {
    return {
      ok: false,
      reason: "descendant_limit_exceeded",
      reasonDetail: `One or more ancestors exceed maxDescendantsPerSession=${config.maxDescendantsPerSession}.`,
    };
  }

  const nextLineage = appendLineage(parentLineage, childSessionId);
  const nextState = registerLineage(state, childSessionId, nextLineage);

  return {
    ok: true,
    nextState,
    nextLineage,
  };
};

export const enforceSpawnBudgetsBeforeStart = (
  config: BoundedAutonomyConfig,
  state: SubagentSpawnState,
  parentSessionId: string,
  parentLineage: SessionLineage,
  childSessionId: string,
): SpawnDecision => {
  return canSpawnSubagent(config, state, parentSessionId, parentLineage, childSessionId);
};

export type ConcurrencyManagerConfig = {
  defaultMaxConcurrent: number;
  defaultMaxQueued: number;
  defaultsByProvider?: Readonly<Record<string, { maxConcurrent: number; maxQueued: number }> >;
  defaultsByModel?: Readonly<Record<string, { maxConcurrent: number; maxQueued: number }> >;
  defaultsByProviderModel?: Readonly<Record<ConcurrencyKey, { maxConcurrent: number; maxQueued: number }> >;
};

export type ConcurrencyStateSnapshot = {
  providers: ReadonlyMap<string, ReadonlyMap<string, SemaphoreSnapshot>>;
  queued: number;
  running: number;
};

type Resolver = (value: ReleaseHandle) => void;

export type SemaphoreSnapshot = {
  provider: string;
  model: string;
  config: {
    maxConcurrent: number;
    maxQueued: number;
  };
  available: number;
  running: number;
  queued: number;
  waiters: number;
};

export type ReleaseHandle = {
  release: () => void;
};

type QueueEntry = {
  id: number;
  resolve: Resolver;
  reject: (reason?: unknown) => void;
  timer?: ReturnType<typeof setTimeout>;
};

type SemaphoreState = {
  config: {
    maxConcurrent: number;
    maxQueued: number;
  };
  running: number;
  queue: Array<QueueEntry>;
  nextId: number;
  waiters: number;
};

export class ConcurrencyError extends Error {
  constructor(message: string, public readonly code: "cancelled" | "queue_full") {
    super(message);
    this.name = "ConcurrencyError";
  }
}

export class ConcurrencyManager {
  private readonly providerDefaults: Readonly<Record<string, { maxConcurrent: number; maxQueued: number }> >;
  private readonly modelDefaults: Readonly<Record<string, { maxConcurrent: number; maxQueued: number }> >;
  private readonly providerModelDefaults: Readonly<Record<ConcurrencyKey, { maxConcurrent: number; maxQueued: number }> >;
  private readonly baseDefault: { maxConcurrent: number; maxQueued: number };
  private readonly semaphores: Map<ConcurrencyKey, SemaphoreState>;

  constructor(config: ConcurrencyManagerConfig) {
    const validated = this.validateConfig(config);
    this.baseDefault = {
      maxConcurrent: validated.defaultMaxConcurrent,
      maxQueued: validated.defaultMaxQueued,
    };
    this.providerDefaults = validated.defaultsByProvider ?? {};
    this.modelDefaults = validated.defaultsByModel ?? {};
    this.providerModelDefaults = validated.defaultsByProviderModel ?? {};
    this.semaphores = new Map();
  }

  private validateConfig(config: ConcurrencyManagerConfig): ConcurrencyManagerConfig {
    const limitsSchema = z
      .object({
        defaultMaxConcurrent: z.number().int().positive(),
        defaultMaxQueued: z.number().int().nonnegative(),
        defaultsByProvider: z.record(
          z.string().min(1),
          z.object({
            maxConcurrent: z.number().int().positive(),
            maxQueued: z.number().nonnegative(),
          }),
        ).optional(),
        defaultsByModel: z.record(
          z.string().min(1),
          z.object({
            maxConcurrent: z.number().int().positive(),
            maxQueued: z.number().nonnegative(),
          }),
        ).optional(),
        defaultsByProviderModel: z.record(
          z.custom<ConcurrencyKey>((value) => {
            if (typeof value !== "string") {
              return false;
            }

            const [provider = "", model = ""] = value.split("/");
            return provider.length > 0 && model.length > 0;
          }, "Provider/model key must be in provider/model format"),
          z.object({
            maxConcurrent: z.number().int().positive(),
            maxQueued: z.number().nonnegative(),
          }),
        ).optional(),
      })
      .parse(config);

    return limitsSchema;
  }

  private buildKey(provider: string, model: string): ConcurrencyKey {
    return `${provider}/${model}` as ConcurrencyKey;
  }

  private loadState(provider: string, model: string): SemaphoreState {
    const key = this.buildKey(provider, model);
    const existing = this.semaphores.get(key);
    if (existing) {
      return existing;
    }

    const cfg = this.resolveLimits(provider, model);
    const created: SemaphoreState = {
      config: {
        maxConcurrent: cfg.maxConcurrent,
        maxQueued: cfg.maxQueued,
      },
      running: 0,
      queue: [],
      nextId: 0,
      waiters: 0,
    };
    this.semaphores.set(key, created);
    return created;
  }

  private resolveLimits(provider: string, model: string): { maxConcurrent: number; maxQueued: number } {
    const specific = this.providerModelDefaults[`${provider}/${model}` as ConcurrencyKey];
    if (specific) {
      return specific;
    }

    const providerDefault = this.providerDefaults[provider];
    if (providerDefault) {
      return providerDefault;
    }

    const modelDefault = this.modelDefaults[model];
    if (modelDefault) {
      return modelDefault;
    }

    return this.baseDefault;
  }

  async acquire(provider: string, model: string, options?: { signal?: AbortSignal; timeoutMs?: number }): Promise<ReleaseHandle> {
    const state = this.loadState(provider, model);
    const key = this.buildKey(provider, model);
    const abortSignal = options?.signal;

    if (abortSignal?.aborted) {
      throw new ConcurrencyError("Acquisition cancelled", "cancelled");
    }

    const shouldRunNow = state.running < state.config.maxConcurrent;
    if (shouldRunNow) {
      state.running += 1;
      return { release: () => this.release(key) };
    }

    if (abortSignal?.aborted) {
      throw new ConcurrencyError("Acquisition cancelled", "cancelled");
    }

    if (state.queue.length >= state.config.maxQueued) {
      throw new ConcurrencyError("Concurrency queue is full", "queue_full");
    }

    return new Promise<ReleaseHandle>((resolve, reject) => {
      const id = state.nextId += 1;
      const entry: QueueEntry = {
        id,
        resolve,
        reject,
      };

      const onAbort = () => {
        this.removeQueueEntry(state, id, true);
        reject(new ConcurrencyError("Acquisition cancelled", "cancelled"));
      };

      if (abortSignal?.aborted) {
        throw new ConcurrencyError("Acquisition cancelled", "cancelled");
      }

      if (abortSignal) {
        abortSignal.addEventListener("abort", onAbort, { once: true });
      }

      if (typeof options?.timeoutMs === "number" && options.timeoutMs >= 0) {
        entry.timer = setTimeout(() => {
          this.removeQueueEntry(state, id, true);
          reject(new ConcurrencyError("Acquisition timed out", "cancelled"));
          if (abortSignal) {
            abortSignal.removeEventListener("abort", onAbort);
          }
        }, options.timeoutMs);
      }

      state.queue.push(entry);
      state.waiters += 1;
      entry.resolve = (handle) => {
        if (abortSignal) {
          abortSignal.removeEventListener("abort", onAbort);
        }
        if (entry.timer) {
          clearTimeout(entry.timer);
          entry.timer = undefined;
        }
        state.waiters -= 1;
        resolve(handle);
      };
      entry.reject = (reason) => {
        if (abortSignal) {
          abortSignal.removeEventListener("abort", onAbort);
        }
        if (entry.timer) {
          clearTimeout(entry.timer);
          entry.timer = undefined;
        }
        state.waiters -= 1;
        reject(reason);
      };
    });
  }

  private removeQueueEntry(state: SemaphoreState, id: number, shouldReject: boolean): void {
    const index = state.queue.findIndex((entry) => entry.id === id);
    if (index < 0) {
      return;
    }

    const [entry] = state.queue.splice(index, 1);
    state.waiters = Math.max(0, state.waiters - 1);

    if (entry.timer) {
      clearTimeout(entry.timer);
      entry.timer = undefined;
    }

    if (shouldReject) {
      entry.reject(new ConcurrencyError("Acquisition cancelled", "cancelled"));
    }
  }

  release(key: ConcurrencyKey): void {
    const state = this.semaphores.get(key);
    if (!state) {
      return;
    }

    state.running = Math.max(0, state.running - 1);

    if (state.queue.length <= 0) {
      return;
    }

    if (state.running >= state.config.maxConcurrent) {
      return;
    }

    const entry = state.queue.shift();
    if (!entry) {
      return;
    }

    state.running += 1;
    entry.resolve({ release: () => this.release(key) });
  }

  getSnapshot(): ConcurrencyStateSnapshot {
    const providers = new Map<string, Map<string, SemaphoreSnapshot>>();
    let queued = 0;
    let running = 0;

    for (const [key, state] of this.semaphores.entries()) {
      const [provider, model] = key.split("/", 2);
      const providerMap = providers.get(provider) ?? new Map<string, SemaphoreSnapshot>();
      const snapshot: SemaphoreSnapshot = {
        provider,
        model,
        config: {
          maxConcurrent: state.config.maxConcurrent,
          maxQueued: state.config.maxQueued,
        },
        available: Math.max(0, state.config.maxConcurrent - state.running),
        running: state.running,
        queued: state.queue.length,
        waiters: state.waiters,
      };

      providerMap.set(model, snapshot);
      providers.set(provider, providerMap);
      queued += state.queue.length;
      running += state.running;
    }

    return {
      providers,
      queued,
      running,
    };
  }

  getPermitCounter(): PermitCounter {
    const snapshot = this.getSnapshot();
    let busyPermits = 0;
    let maxPermits = 0;

    for (const providerMap of snapshot.providers.values()) {
      for (const semaphore of providerMap.values()) {
        maxPermits += semaphore.config.maxConcurrent;
        busyPermits += semaphore.running;
      }
    }

    return {
      available: Math.max(0, maxPermits - busyPermits),
      total: maxPermits,
    };
  }

  getLimits() : ConcurrencyLimitsInput {
    const result: ConcurrencyLimitsInput = {};
    for (const [key, state] of this.semaphores.entries()) {
      result[key] = {
        maxConcurrent: state.config.maxConcurrent,
        maxQueued: state.config.maxQueued,
      };
    }

    return result;
  }
}
