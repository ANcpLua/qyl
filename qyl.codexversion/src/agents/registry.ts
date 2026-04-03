import {
  AgentContract,
  AgentContractSchema,
  AgentKind,
} from "../core/contracts";
import {
  AgentPromptMetadata,
  BuiltInAgentPromptRegistry,
  builtInAgentPrompts,
  BuiltInPromptCategoryRegistry,
  builtInPromptCategories,
  PromptCategory,
} from "./prompt-metadata";
import {
  BuiltInModelFallbackChain,
  MODEL_REQUIREMENT_REGISTRY,
  getAgentModelFallbackChain,
  getCategoryModelFallbackChain,
} from "../shared/model-requirements";

type AgentSourceDefinition = {
  contract: AgentContract;
  promptMetadata: AgentPromptMetadata;
  modelFallbackChain?: BuiltInModelFallbackChain;
};

const nowIso = "2026-01-01T00:00:00.000Z";

const defaultPolicy = {
  maxDelegationsPerHour: 200,
  maxConcurrentTasks: 4,
  allowedRegions: ["global"],
  complianceTags: ["codex"],
};

const buildAgentContract = (
  agentMetadata: AgentPromptMetadata,
  kind: AgentKind,
  owner: string,
): AgentContract => {
  const contract: AgentContract = AgentContractSchema.parse({
    agentId: agentMetadata.id,
    kind,
    name: agentMetadata.displayName,
    version: agentMetadata.version,
    owner,
    capabilities: [
      {
        area: "coordination",
        level: 4,
      },
      {
        area: agentMetadata.category,
        level: 3,
      },
    ],
    policy: defaultPolicy,
    metadata: {
      promptId: agentMetadata.promptId,
      category: agentMetadata.category,
      displayName: agentMetadata.displayName,
      promptEnabled: String(agentMetadata.enabled),
    },
    enabled: agentMetadata.enabled,
    createdAt: nowIso,
    updatedAt: nowIso,
  });

  return contract;
};

const AGENT_KIND_BY_ID: Record<string, AgentKind> = {
  [builtInAgentPrompts.router.id]: "orchestrator",
  [builtInAgentPrompts.triage.id]: "operator",
  [builtInAgentPrompts.investigator.id]: "investigator",
  [builtInAgentPrompts.evaluator.id]: "evaluator",
  [builtInAgentPrompts.governor.id]: "worker",
};

const byAgentId = Object.fromEntries(
  Object.entries(builtInAgentPrompts).map(([key, promptMetadata]) => {
    const agentKind = AGENT_KIND_BY_ID[promptMetadata.id];
    if (!agentKind) {
      throw new Error(`Missing AGENT_KIND_BY_ID entry for '${promptMetadata.id}'.`);
    }

    const contract = buildAgentContract(
      promptMetadata,
      agentKind,
      `qyl-codexversion-${key}`,
    );

    return [
      promptMetadata.id,
      {
        contract,
        promptMetadata,
        modelFallbackChain: getAgentModelFallbackChain(promptMetadata.id),
      } satisfies AgentSourceDefinition,
    ];
  }),
) as Record<string, AgentSourceDefinition>;

const byAgentName = Object.fromEntries(
  Object.entries(byAgentId).map(([id, definition]) => {
    const normalizedName = definition.contract.name.toLowerCase();
    return [normalizedName, id] as const;
  }),
);

const resolveCategoryAgentIds = (registry: BuiltInPromptCategoryRegistry): string[] => {
  return Object.values(registry).flatMap((category) => {
    const uniqueIds = new Set([...category.defaultAgentIds, ...category.fallbackAgentIds]);
    return Array.from(uniqueIds);
  });
};

const categoryAgentIds = resolveCategoryAgentIds(builtInPromptCategories);
const byCategoryId = Object.fromEntries(
  Object.entries(builtInPromptCategories).map(([categoryId, category]) => {
    const normalizedCategoryId = categoryId.toLowerCase();
    const dedupedAgentIds = Array.from(
      new Set([...category.defaultAgentIds, ...category.fallbackAgentIds]),
    );
    return [
      normalizedCategoryId,
      dedupedAgentIds,
    ] as const;
  }),
);

const byPromptMetadataByCategoryId = Object.fromEntries(
  Object.entries(byCategoryId).map(([categoryId, agentIds]) => {
    const metadata = builtInPromptCategories[categoryId as keyof typeof builtInPromptCategories];
    const fallbackByCategory = getCategoryModelFallbackChain(categoryId);
    return [
      categoryId,
      {
        category: metadata,
        promptMetadataByAgent: agentIds
          .map((id) => byAgentId[id]?.promptMetadata)
          .filter((candidate): candidate is AgentPromptMetadata => Boolean(candidate)),
        modelFallbackChain: fallbackByCategory,
      } satisfies {
        category: PromptCategory;
        promptMetadataByAgent: AgentPromptMetadata[];
        modelFallbackChain?: BuiltInModelFallbackChain;
      },
    ] as const;
  }),
);

export const BUILT_IN_AGENT_SOURCE_REGISTRY: BuiltInAgentPromptRegistry = builtInAgentPrompts;

export const getAgentSourceById = (agentId: string): AgentSourceDefinition | undefined =>
  byAgentId[agentId];

export const getAgentSourceByName = (name: string): AgentSourceDefinition | undefined => {
  const id = byAgentName[name.trim().toLowerCase()];
  return id ? byAgentId[id] : undefined;
};

export const getAgentSourceByCategory = (categoryId: string): AgentSourceDefinition[] => {
  const ids = byCategoryId[categoryId.toLowerCase()] ?? [];
  return ids
    .map((id) => byAgentId[id])
    .filter((candidate): candidate is AgentSourceDefinition => Boolean(candidate));
};

export const getModelRequirementByAgentId = (
  agentId: string,
): BuiltInModelFallbackChain | undefined => {
  return byAgentId[agentId]?.modelFallbackChain;
};

export const getModelRequirementByCategoryId = (
  categoryId: string,
): BuiltInModelFallbackChain | undefined => {
  const normalizedCategoryId = categoryId.toLowerCase();
  return byPromptMetadataByCategoryId[normalizedCategoryId]?.modelFallbackChain;
};

export const getPromptMetadataForCategory = (
  categoryId: string,
): PromptCategory | undefined => {
  return builtInPromptCategories[categoryId.toLowerCase()];
};

export const getPromptMetadataForAgent = (agentId: string): AgentPromptMetadata | undefined => {
  return byAgentId[agentId]?.promptMetadata;
};

export const getAllAgentSources = (): AgentSourceDefinition[] => {
  return Object.values(byAgentId);
};

export const getAllCategories = (): PromptCategory[] => {
  return Object.values(builtInPromptCategories);
};

export const getAgentIdsByCategory = (categoryId: string): string[] => {
  return byCategoryId[categoryId.toLowerCase()] ?? [];
};

export const getGlobalModelFallbackChains = (): BuiltInModelFallbackChain[] => {
  return Object.values(MODEL_REQUIREMENT_REGISTRY.fallbackChains);
};

export const getModelChainIdForAgent = (agentId: string): string | undefined => {
  const chain = getModelRequirementByAgentId(agentId);
  return chain?.chainId;
};

export const getModelChainIdForCategory = (categoryId: string): string | undefined => {
  const chain = getModelRequirementByCategoryId(categoryId);
  return chain?.chainId;
};

export const listCategoryAgentNames = (categoryId: string): string[] => {
  return getAgentSourceByCategory(categoryId).map((agent) => agent.contract.name);
};

export const listCategoryIds = (): string[] => {
  return Object.keys(builtInPromptCategories);
};
