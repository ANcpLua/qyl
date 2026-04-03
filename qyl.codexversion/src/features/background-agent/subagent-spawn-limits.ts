import { z } from "zod";

export const boundedAutonomySchema = z.object({
  maxDepth: z.number().int().positive(),
  maxRootSessionSpawnBudget: z.number().int().nonnegative(),
  maxDescendantsPerSession: z.number().int().nonnegative(),
  rootSessionSpawnBudgets: z.record(
    z.string().min(1),
    z.number().int().nonnegative(),
  ).optional(),
});

export type BoundedAutonomyConfig = z.infer<typeof boundedAutonomySchema>;

export type SessionLineage = ReadonlyArray<string>;

export type SubagentSpawnUsage = ReadonlyMap<string, number>;

export type SubagentSpawnState = {
  lineages: ReadonlyMap<string, SessionLineage>;
  rootSessionSpawnUsage: SubagentSpawnUsage;
  descendantUsage: SubagentSpawnUsage;
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
  | {
      ok: true;
      reason?: never;
      reasonDetail?: never;
      nextState: SubagentSpawnState;
      nextLineage: SessionLineage;
    }
  | {
      ok: false;
      reason: SpawnBlockReason;
      reasonDetail: string;
      nextState?: never;
      nextLineage?: never;
    };

const getUsage = (usage: SubagentSpawnUsage, key: string): number => usage.get(key) ?? 0;

const cloneMap = <T>(input: ReadonlyMap<string, T>): Map<string, T> => {
  return new Map(Array.from(input.entries()));
};

export const createRootLineage = (rootSessionId: string): SessionLineage => [rootSessionId];

export const appendLineage = (lineage: SessionLineage, childSessionId: string): SessionLineage => [
  ...lineage,
  childSessionId,
];

export const lineageDepth = (lineage: SessionLineage): number => lineage.length;

export const getLineageRoot = (lineage: SessionLineage): string | undefined => lineage[0];

export const hasCycle = (lineage: SessionLineage, childSessionId: string): boolean => lineage.includes(childSessionId);

export const isLineageValid = (
  state: SubagentSpawnState,
  parentSessionId: string,
  parentLineage: SessionLineage,
): { ok: true } | { ok: false; reason: SpawnBlockReason; reasonDetail: string } => {
  if (parentLineage.length === 0) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Parent lineage must contain at least one session id.",
    };
  }

  const lineageHead = parentLineage[parentLineage.length - 1];
  if (lineageHead !== parentSessionId) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Parent lineage must end with the declared parent session id.",
    };
  }

  if (!getLineageRoot(parentLineage)) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Parent lineage has no root session id.",
    };
  }

  if (state.lineages.has(parentSessionId) === false && parentSessionId !== parentLineage[0]) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Declared parent session is not tracked and is not a root session.",
    };
  }

  const trackedParentLineage = state.lineages.get(parentSessionId);
  if (trackedParentLineage && trackedParentLineage.length !== parentLineage.length) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Provided parent lineage does not match tracked lineage length.",
    };
  }

  if (trackedParentLineage && trackedParentLineage.some((id, i) => id !== parentLineage[i])) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Provided parent lineage does not match tracked lineage.",
    };
  }

  return { ok: true };
};

export const isWithinDepthLimit = (
  config: BoundedAutonomyConfig,
  parentLineage: SessionLineage,
): { ok: true } | { ok: false; reasonDetail: string } => {
  // We count nodes from root. A depth of 1 means only a root session exists and cannot spawn.
  if (lineageDepth(parentLineage) < config.maxDepth) {
    return { ok: true };
  }

  return {
    ok: false,
    reasonDetail: `Spawn would exceed maxDepth=${config.maxDepth}.`,
  };
};

const resolveRootBudget = (config: BoundedAutonomyConfig, rootSessionId: string): number => {
  return config.rootSessionSpawnBudgets?.[rootSessionId] ?? config.maxRootSessionSpawnBudget;
};

export const isWithinRootBudget = (
  config: BoundedAutonomyConfig,
  state: SubagentSpawnState,
  rootSessionId: string,
): { ok: true } | { ok: false; reasonDetail: string } => {
  const used = getUsage(state.rootSessionSpawnUsage, rootSessionId);
  const budget = resolveRootBudget(config, rootSessionId);
  if (used < budget) {
    return { ok: true };
  }

  return {
    ok: false,
    reasonDetail: `Root session '${rootSessionId}' exceeded spawn budget ${budget}.`,
  };
};

const allAncestorIds = (lineage: SessionLineage): ReadonlyArray<string> => lineage;

export const isWithinDescendantCap = (
  config: BoundedAutonomyConfig,
  state: SubagentSpawnState,
  parentLineage: SessionLineage,
): { ok: true } | { ok: false; reasonDetail: string } => {
  if (config.maxDescendantsPerSession <= 0) {
    // Explicit zero means root/any parent cannot spawn children.
    return {
      ok: false,
      reasonDetail: `Descendant spawn cap is zero (maxDescendantsPerSession=${config.maxDescendantsPerSession}).`,
    };
  }

  for (const sessionId of allAncestorIds(parentLineage)) {
    if (getUsage(state.descendantUsage, sessionId) >= config.maxDescendantsPerSession) {
      return {
        ok: false,
        reasonDetail: `Session '${sessionId}' reached maxDescendantsPerSession=${config.maxDescendantsPerSession}.`,
      };
    }
  }

  return { ok: true };
};

const incrementDescendantUsage = (usage: SubagentSpawnUsage, parentLineage: SessionLineage): SubagentSpawnUsage => {
  const next = cloneMap(usage);
  for (const sessionId of allAncestorIds(parentLineage)) {
    next.set(sessionId, getUsage(next, sessionId) + 1);
  }

  return next;
};

const decrementDescendantUsage = (usage: SubagentSpawnUsage, parentLineage: SessionLineage): SubagentSpawnUsage => {
  const next = cloneMap(usage);
  for (const sessionId of allAncestorIds(parentLineage)) {
    const current = getUsage(next, sessionId);
    if (current > 0) {
      const nextCount = current - 1;
      if (nextCount === 0) {
        next.delete(sessionId);
      } else {
        next.set(sessionId, nextCount);
      }
    }
  }

  return next;
};

const registerSpawn = (
  state: SubagentSpawnState,
  childSessionId: string,
  nextLineage: SessionLineage,
  descendantChargeLineage: SessionLineage,
): SubagentSpawnState => {
  const rootSessionId = getLineageRoot(nextLineage);
  if (!rootSessionId) {
    return state;
  }

  const lineages = cloneMap(state.lineages);
  lineages.set(childSessionId, nextLineage);

  return {
    lineages,
    rootSessionSpawnUsage: (() => {
      const nextRootUsage = cloneMap(state.rootSessionSpawnUsage);
      nextRootUsage.set(rootSessionId, getUsage(state.rootSessionSpawnUsage, rootSessionId) + 1);
      return nextRootUsage;
    })(),
    descendantUsage: incrementDescendantUsage(state.descendantUsage, descendantChargeLineage),
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

  if (hasCycle(parentLineage, childSessionId)) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Child session would create a lineage cycle.",
    };
  }

  const lineageValidation = isLineageValid(state, parentSessionId, parentLineage);
  if (!lineageValidation.ok) {
    return {
      ok: false,
      reason: lineageValidation.reason,
      reasonDetail: lineageValidation.reasonDetail,
    };
  }

  const depthValidation = isWithinDepthLimit(config, parentLineage);
  if (!depthValidation.ok) {
    return {
      ok: false,
      reason: "depth_limit_exceeded",
      reasonDetail: depthValidation.reasonDetail,
    };
  }

  const rootSessionId = getLineageRoot(parentLineage);
  if (!rootSessionId) {
    return {
      ok: false,
      reason: "lineage_cycle",
      reasonDetail: "Parent lineage must identify a root session.",
    };
  }

  const rootBudgetValidation = isWithinRootBudget(config, state, rootSessionId);
  if (!rootBudgetValidation.ok) {
    return {
      ok: false,
      reason: "root_budget_exceeded",
      reasonDetail: rootBudgetValidation.reasonDetail,
    };
  }

  const descendantValidation = isWithinDescendantCap(config, state, parentLineage);
  if (!descendantValidation.ok) {
    return {
      ok: false,
      reason: "descendant_limit_exceeded",
      reasonDetail: descendantValidation.reasonDetail,
    };
  }

  const nextLineage = appendLineage(parentLineage, childSessionId);
  const nextState = registerSpawn(state, childSessionId, nextLineage, parentLineage);

  return {
    ok: true,
    nextLineage,
    nextState,
  };
};

export const releaseSubagentSpawn = (
  state: SubagentSpawnState,
  childLineage: SessionLineage,
): SubagentSpawnState => {
  const rootSessionId = getLineageRoot(childLineage);
  if (!rootSessionId || childLineage.length === 0) {
    return state;
  }

  const parentLineage = childLineage.slice(0, -1);

  const rootSessionSpawnUsage = (() => {
    const next = cloneMap(state.rootSessionSpawnUsage);
    const current = getUsage(next, rootSessionId);
    if (current <= 1) {
      next.delete(rootSessionId);
    } else {
      next.set(rootSessionId, current - 1);
    }

    return next;
  })();

  return {
    ...state,
    rootSessionSpawnUsage,
    descendantUsage: decrementDescendantUsage(state.descendantUsage, parentLineage),
  };
};

export const enforceSpawnBudgetsBeforeStart = canSpawnSubagent;
