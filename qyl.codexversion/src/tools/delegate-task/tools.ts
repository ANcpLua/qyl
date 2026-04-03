import { z } from "zod";

import {
  getAgentSourceByCategory,
  getAgentSourceById,
  getAgentSourceByName,
  getPromptMetadataForAgent,
  getPromptMetadataForCategory,
} from "../../agents/registry";
import { getAgentModelFallbackChain, getCategoryModelFallbackChain } from "../../shared/model-requirements";
import { BackgroundAgentManager } from "../../features/background-agent/manager";
import { createRootLineage } from "../../features/background-agent/subagent-spawn-limits";

import type { BuiltInModelFallbackChain } from "../../shared/model-requirements";

const delegateModeSchema = z.enum(["sync", "background"]);
const routeSelectionSchema = z.object({
  category: z
    .string()
    .trim()
    .min(1)
    .transform((value) => value.toLowerCase())
    .optional(),
  subagent_type: z
    .string()
    .trim()
    .min(1)
    .transform((value) => value.toLowerCase())
    .optional(),
});

const requestSchema = z.object({
  subject: z.string().trim().min(1),
  rationale: z.string().trim().min(1),
  instructions: z.string().trim().min(1),
  context: z.record(z.unknown()).default({}),
  route: routeSelectionSchema,
  mode: delegateModeSchema.optional(),
  trace: z
    .object({
      requestId: z.string().trim().min(1),
      sessionId: z.string().trim().min(1),
      correlationId: z.string().trim().min(1).optional(),
    })
    .optional(),
  parentSessionId: z.string().trim().min(1).optional(),
  parentLineage: z.array(z.string().trim().min(1)).default([]),
  childSessionId: z.string().trim().min(1).optional(),
  timeoutMs: z.number().int().positive().optional(),
  requestIdHint: z.string().trim().min(1).optional(),
  sessionIdHint: z.string().trim().min(1).optional(),
});

const modelStepSchema = z.object({
  provider: z.string().min(1),
  model: z.string().min(1),
  variant: z.string().min(1).optional(),
});

const routeSchema = z.object({
  routingMode: z.enum(["category", "subagent"]),
  selectedAgentId: z.string().min(1),
  selectedAgentName: z.string().min(1),
  selectedCategory: z.string().min(1).optional(),
  candidateCount: z.number().int().nonnegative(),
  canRunInBackground: z.boolean(),
});

const delegateContractSchema = z.object({
  requestId: z.string().min(1),
  sessionId: z.string().min(1),
  fromAgentId: z.string().min(1),
  toAgentId: z.string().min(1),
  toAgentKind: z.enum(["orchestrator", "operator", "investigator", "delegate", "evaluator", "worker"]),
  channel: z.enum(["sync", "async", "batch"]),
  priority: z.enum(["low", "normal", "high", "urgent"]).default("normal"),
  subject: z.string().min(1),
  rationale: z.string().min(1),
  instructions: z.string().min(1),
  requirements: z.array(z.string()).default([]),
  payload: z.record(z.unknown()).default({}),
  metadata: z.record(z.unknown()).default({}),
  delegatedAt: z.string().datetime(),
  deadlineAt: z.string().datetime().optional(),
  responseDeadlineMs: z.number().int().positive(),
  trace: z.object({
    requestId: z.string().trim().min(1),
    sessionId: z.string().trim().min(1),
    correlationId: z.string().trim().min(1).optional(),
  }),
});

const modelSelectionSchema = z.object({
  modelChainId: z.string().min(1),
  selectedProvider: z.string().min(1),
  selectedModel: z.string().min(1),
  selectedVariant: z.string().min(1).optional(),
  requirementSteps: z.array(modelStepSchema).default([]),
});

const syncResultSchema = z.object({
  status: z.literal("sync_completed"),
  delegateResponse: z.record(z.unknown()),
});

const backgroundResultSchema = z.object({
  status: z.literal("background_queued"),
  taskId: z.string().min(1),
  childSessionId: z.string().min(1),
  requestId: z.string().min(1),
  lineage: z.array(z.string().min(1)),
});

const responseSchema = z.object({
  route: routeSchema,
  mode: z.enum(["sync", "background"]),
  delegateRequest: delegateContractSchema,
  modelSelection: modelSelectionSchema,
  result: z.union([syncResultSchema, backgroundResultSchema]),
  trace: z.object({
    requestId: z.string().min(1),
    sessionId: z.string().min(1),
    correlationId: z.string().min(1).optional(),
  }),
});

export type DelegateTaskRequest = z.infer<typeof requestSchema>;
export type DelegateTaskResponse = z.infer<typeof responseSchema>;
export type ModelStep = z.infer<typeof modelStepSchema>;

type DelegateMode = "sync" | "background";

export type SyncExecutionInput = {
  delegateRequest: z.infer<typeof delegateContractSchema>;
  modelSelection: z.infer<typeof modelSelectionSchema>;
  trace: { requestId: string; sessionId: string; correlationId?: string };
};

export type SyncExecutionResult = z.infer<typeof syncResultSchema>;

export type BackgroundExecutionConfig = {
  backgroundManager?: BackgroundAgentManager;
};

export type DelegateTaskExecutor = {
  executeSync: (input: SyncExecutionInput) => Promise<SyncExecutionResult["delegateResponse"]>;
};

export type DelegateTaskOptions = {
  executor?: DelegateTaskExecutor;
  background?: BackgroundExecutionConfig;
  defaultMode?: DelegateMode;
  canSpawnWithoutManager?: boolean;
};

export class DelegateTaskError extends Error {
  public constructor(message: string) {
    super(message);
    this.name = "DelegateTaskError";
  }
}

const toSafeId = (candidate?: string): string | undefined =>
  candidate?.trim().toLowerCase();

const nowIso = (): string => new Date().toISOString();

const parseRequest = (raw: unknown): DelegateTaskRequest =>
  requestSchema.parse(raw);

const assertRouteExclusivity = (route: z.infer<typeof routeSelectionSchema>): {
  category?: string;
  subagentType?: string;
} => {
  const category = toSafeId(route.category);
  const subagentType = toSafeId(route.subagent_type);

  if ((category && subagentType) || (!category && !subagentType)) {
    throw new DelegateTaskError(
      "delegate-task requires exactly one of route.category or route.subagent_type",
    );
  }

  return { category, subagentType };
};

const pickModelFromChain = (chain: BuiltInModelFallbackChain): {
  chainId: string;
  provider: string;
  model: string;
  variant?: string;
  allSteps: ModelStep[];
} => {
  if (chain.steps.length === 0) {
    throw new DelegateTaskError(
      `Model fallback chain '${chain.chainId}' has no configured steps.`,
    );
  }

  const [first] = chain.steps;
  return {
    chainId: chain.chainId,
    provider: first.providers.at(0) ?? "",
    model: first.model,
    variant: first.variant,
    allSteps: chain.steps.map((step) => ({
      provider: step.providers.at(0) ?? "",
      model: step.model,
      variant: step.variant,
    })),
  };
};

const resolveRouteByAgentType = (subagentType: string) => {
  const exactId = getAgentSourceById(subagentType);
  const exactName = exactId ? undefined : getAgentSourceByName(subagentType);
  const resolved = exactId ?? exactName;

  if (!resolved) {
    throw new DelegateTaskError(
      `Unknown subagent_type '${subagentType}'. Register it in agent registry first.`,
    );
  }

  const metadata = getPromptMetadataForAgent(resolved.contract.agentId);
  if (!metadata) {
    throw new DelegateTaskError(`Agent metadata missing for ${resolved.contract.agentId}.`);
  }

  return {
    routingMode: "subagent" as const,
    selectedAgentId: resolved.contract.agentId,
    selectedAgentName: resolved.contract.name,
    selectedCategory: metadata.category,
    candidateCount: 1,
    canRunInBackground: metadata.orchestration.canRunInBackground,
    metadata,
    contract: resolved,
  };
};

const resolveRouteByCategory = (category: string) => {
  const agents = getAgentSourceByCategory(category);
  if (agents.length === 0) {
    throw new DelegateTaskError(`Unknown or empty category '${category}'.`);
  }

  const selected = agents[0];
  const categoryMetadata = getPromptMetadataForCategory(category);
  if (!categoryMetadata) {
    throw new DelegateTaskError(`Category metadata missing for '${category}'.`);
  }

  const candidateIds = agents.map((agent) => agent.contract.agentId);
  return {
    routingMode: "category" as const,
    selectedAgentId: selected.contract.agentId,
    selectedAgentName: selected.contract.name,
    selectedCategory: categoryMetadata.id,
    candidateCount: candidatesCount(candidateIds),
    canRunInBackground: selected.promptMetadata.orchestration.canRunInBackground,
    metadata: selected.promptMetadata,
    contract: selected,
    candidates: candidateIds,
  };
};

const candidatesCount = (ids: string[]): number => ids.length;

const resolveRouting = (
  route: z.infer<typeof routeSelectionSchema>,
) => {
  const normalized = assertRouteExclusivity(route);
  if (normalized.subagentType) {
    return resolveRouteByAgentType(normalized.subagentType);
  }

  return resolveRouteByCategory(normalized.category!);
};

const selectMode = (
  requestedMode: DelegateMode | undefined,
  route: ReturnType<typeof resolveRouting>,
  orchestratedDefaultMode: DelegateMode = "sync",
): DelegateMode => {
  if (requestedMode) {
    return requestedMode;
  }

  if (!route.canRunInBackground) {
    return "sync";
  }

  return orchestratedDefaultMode;
};

const resolveModelRequirements = (params: {
  agentId?: string;
  category?: string;
}) => {
  let chain: BuiltInModelFallbackChain | undefined;

  if (params.agentId) {
    chain = getAgentModelFallbackChain(params.agentId) ?? getAgentSourceById(params.agentId)?.modelFallbackChain;
  }

  if (!chain && params.category) {
    chain = getCategoryModelFallbackChain(params.category);
  }

  if (!chain) {
    throw new DelegateTaskError(
      "No model requirement chain found in registry for selected route.",
    );
  }

  return pickModelFromChain(chain);
};

const resolveContextIds = (input: DelegateTaskRequest) => {
  const requestId = input.requestIdHint ?? input.trace?.requestId ?? crypto.randomUUID();
  const sessionId = input.sessionIdHint ?? input.trace?.sessionId ?? `delegate-session-${requestId}`;
  const normalizedParentSessionId = input.parentSessionId?.trim() || sessionId;
  const lineageFromInput = input.parentLineage.map((entry) => entry.trim()).filter(Boolean);
  const normalizedLineage = lineageFromInput.length > 0 ? lineageFromInput : createRootLineage(normalizedParentSessionId);

  if (normalizedLineage[normalizedLineage.length - 1] !== normalizedParentSessionId) {
    normalizedLineage.push(normalizedParentSessionId);
  }

  return { requestId, sessionId, parentSessionId: normalizedParentSessionId, parentLineage: normalizedLineage };
};

const buildChildSessionId = (
  parentSessionId: string,
  agentId: string,
  inputChildSessionId?: string,
) => {
  if (inputChildSessionId) {
    return inputChildSessionId;
  }

  const suffix = Math.random().toString(36).slice(2, 10);
  return `delegate-${parentSessionId}-${agentId}-${suffix}`;
};

const buildDelegateRequest = (params: {
  ids: { requestId: string; sessionId: string };
  route: ReturnType<typeof resolveRouting>;
  request: DelegateTaskRequest;
  chainSelection: ReturnType<typeof pickModelFromChain>;
  mode: DelegateMode;
}): z.infer<typeof delegateContractSchema> => {
  const { ids, route, request, chainSelection, mode } = params;

  return {
    requestId: ids.requestId,
    sessionId: ids.sessionId,
    fromAgentId: "orchestrator",
    toAgentId: route.selectedAgentId,
    toAgentKind: (route.metadata?.category === "routing"
      ? "orchestrator"
      : route.metadata?.category === "investigation"
        ? "investigator"
        : route.metadata?.category === "quality"
          ? "evaluator"
          : "worker") as
      | "orchestrator"
      | "operator"
      | "investigator"
      | "delegate"
      | "evaluator"
      | "worker",
    channel: mode === "sync" ? "sync" : "async",
    subject: request.subject,
    rationale: request.rationale,
    instructions: request.instructions,
    requirements: [chainSelection.modelChainId, chainSelection.selectedProvider, chainSelection.selectedModel],
    payload: {
      context: request.context,
      route,
      modelSelection: {
        chainId: chainSelection.chainId,
        provider: chainSelection.selectedProvider,
        model: chainSelection.selectedModel,
        variant: chainSelection.selectedVariant,
      },
    },
    metadata: {
      inputMode: mode,
      delegateMode: mode,
      candidateCount: route.candidateCount,
      routeKind: route.routingMode,
      category: route.selectedCategory ?? "unscoped",
    },
    delegatedAt: nowIso(),
    responseDeadlineMs: 30_000,
    deadlineAt: request.timeoutMs ? new Date(Date.now() + request.timeoutMs).toISOString() : undefined,
    trace: {
      requestId: ids.requestId,
      sessionId: ids.sessionId,
      correlationId: request.trace?.correlationId,
    },
  } satisfies z.infer<typeof delegateContractSchema>;
};

const runBackground = async (
  manager: BackgroundAgentManager | undefined,
  options: DelegateTaskRequest,
  ids: ReturnType<typeof resolveContextIds>,
  route: ReturnType<typeof resolveRouting>,
  modelSelection: ReturnType<typeof pickModelFromChain>,
): Promise<z.infer<typeof backgroundResultSchema>> => {
  if (!manager) {
    throw new DelegateTaskError(
      "Background mode requested but no BackgroundAgentManager was provided in tool options.",
    );
  }

  const childSessionId = buildChildSessionId(ids.parentSessionId, route.selectedAgentId, options.childSessionId);
  const spawnOutcome = await manager.requestSpawn({
    parentSessionId: ids.parentSessionId,
    parentLineage: ids.parentLineage,
    childSessionId,
    provider: modelSelection.selectedProvider,
    model: modelSelection.selectedModel,
    payload: {
      requestId: ids.requestId,
      sessionId: ids.sessionId,
      toAgentId: route.selectedAgentId,
      toAgentName: route.selectedAgentName,
      subject: options.subject,
      rationale: options.rationale,
      mode: "background",
    },
    timeoutMs: options.timeoutMs,
    requestId: ids.requestId,
    taskId: `bg-${ids.requestId}`,
  });

  if (!spawnOutcome.ok) {
    throw new DelegateTaskError(
      `Background spawn denied (${spawnOutcome.reason}): ${spawnOutcome.reasonDetail}`,
    );
  }

  return backgroundResultSchema.parse({
    status: "background_queued",
    taskId: spawnOutcome.taskId,
    childSessionId: spawnOutcome.childSessionId,
    requestId: spawnOutcome.requestId,
    lineage: spawnOutcome.lineage,
  });
};

export const delegateTask = async (
  rawInput: unknown,
  options: DelegateTaskOptions = {},
): Promise<DelegateTaskResponse> => {
  const input = parseRequest(rawInput);
  const ids = resolveContextIds(input);
  const route = resolveRouting(input.route);

  const modelSelection = resolveModelRequirements({
    agentId: route.selectedAgentId,
    category: route.selectedCategory,
  });

  const selectedModel = modelSelectionSchema.parse({
    modelChainId: modelSelection.chainId,
    selectedProvider: modelSelection.selectedProvider,
    selectedModel: modelSelection.selectedModel,
    selectedVariant: modelSelection.selectedVariant,
    requirementSteps: modelSelection.allSteps,
  });

  let mode = selectMode(input.mode, route, options.defaultMode ?? "sync");

  const delegateRequest = delegateContractSchema.parse(
    buildDelegateRequest({
      ids,
      route,
      request: input,
      chainSelection: {
        chainId: selectedModel.modelChainId,
        selectedProvider: selectedModel.selectedProvider,
        selectedModel: selectedModel.selectedModel,
        selectedVariant: selectedModel.selectedVariant,
        allSteps: selectedModel.requirementSteps,
      },
      mode,
    }),
  );

  if (mode === "background") {
    if (!route.canRunInBackground) {
      throw new DelegateTaskError(
        `Requested background mode for '${route.selectedCategory ?? route.selectedAgentId}' but target cannot run in background.`,
      );
    }

    if (!options.background?.backgroundManager) {
      if (options.canSpawnWithoutManager) {
        mode = "sync";
      } else {
        throw new DelegateTaskError(
          "Background mode requested but no BackgroundAgentManager was provided in tool options.",
        );
      }
    }
  }

  if (mode === "background") {
    const result = await runBackground(
      options.background?.backgroundManager,
      input,
      ids,
      route,
      modelSelection,
    );

    return responseSchema.parse({
      route: {
        routingMode: route.routingMode,
        selectedAgentId: route.selectedAgentId,
        selectedAgentName: route.selectedAgentName,
        selectedCategory: route.selectedCategory,
        candidateCount: route.candidateCount,
        canRunInBackground: route.canRunInBackground,
      },
      mode,
      delegateRequest,
      modelSelection: {
        ...selectedModel,
      },
      result,
      trace: {
        requestId: ids.requestId,
        sessionId: ids.sessionId,
        correlationId: input.trace?.correlationId,
      },
    });
  }

  if (mode === "background" && !options.background?.backgroundManager && options.canSpawnWithoutManager) {
    mode = "sync";
  }

  if (!options.executor) {
    throw new DelegateTaskError(
      mode === "background"
        ? "Background mode requires a BackgroundAgentManager to queue execution."
        : "Sync mode requires an executor to run the delegated task immediately.",
    );
  }

  const syncResponse = await options.executor.executeSync({
    delegateRequest,
    modelSelection: {
      modelChainId: selectedModel.modelChainId,
      selectedProvider: selectedModel.selectedProvider,
      selectedModel: selectedModel.selectedModel,
      selectedVariant: selectedModel.selectedVariant,
      requirementSteps: selectedModel.requirementSteps,
    },
    trace: {
      requestId: ids.requestId,
      sessionId: ids.sessionId,
      correlationId: input.trace?.correlationId,
    },
  });

  return responseSchema.parse({
    route: {
      routingMode: route.routingMode,
      selectedAgentId: route.selectedAgentId,
      selectedAgentName: route.selectedAgentName,
      selectedCategory: route.selectedCategory,
      candidateCount: route.candidateCount,
      canRunInBackground: route.canRunInBackground,
    },
    mode,
    delegateRequest,
    modelSelection: {
      ...selectedModel,
    },
    result: {
      status: "sync_completed",
      delegateResponse: syncResponse,
    },
    trace: {
      requestId: ids.requestId,
      sessionId: ids.sessionId,
      correlationId: input.trace?.correlationId,
    },
  });
};

export const delegateTaskInputSchema = requestSchema;
export const delegateTaskResponseSchema = responseSchema;
