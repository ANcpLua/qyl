import { z } from "zod";

export const ModelRequirementDeclarationSchema = z
  .object({
    providers: z.array(z.string().min(1)).nonempty(),
    model: z.string().min(1),
    variant: z.string().min(1),
    requiresAnyModel: z.boolean().default(false),
  })
  .strict();

export type ModelRequirementDeclaration = z.infer<typeof ModelRequirementDeclarationSchema>;

export const ModelFallbackChainItemSchema = z
  .object({
    chainId: z.string().min(1),
    requirement: ModelRequirementDeclarationSchema,
  })
  .strict();

export type ModelFallbackChainItem = z.infer<typeof ModelFallbackChainItemSchema>;

export const BuiltInModelFallbackChainSchema = z
  .object({
    chainId: z.string().min(1),
    steps: z.array(ModelRequirementDeclarationSchema).min(1),
    category: z.string().optional(),
  })
  .strict();

export type BuiltInModelFallbackChain = z.infer<typeof BuiltInModelFallbackChainSchema>;

export const BuiltInAgentModelRequirementSchema = z
  .object({
    agentId: z.string().min(1),
    chainId: z.string().min(1),
  })
  .strict();

export type BuiltInAgentModelRequirement = z.infer<typeof BuiltInAgentModelRequirementSchema>;

export const BuiltInCategoryModelRequirementSchema = z
  .object({
    category: z.string().min(1),
    chainId: z.string().min(1),
  })
  .strict();

export type BuiltInCategoryModelRequirement = z.infer<typeof BuiltInCategoryModelRequirementSchema>;

export const modelFallbackChainRegistrySchema = z.record(z.string().min(1), BuiltInModelFallbackChainSchema);

export const builtInAgentModelRequirementsSchema = z.array(BuiltInAgentModelRequirementSchema);

export const builtInCategoryModelRequirementsSchema = z.array(BuiltInCategoryModelRequirementSchema);

export const ModelRequirementRegistrySchema = z
  .object({
    agentRequirements: builtInAgentModelRequirementsSchema,
    categoryRequirements: builtInCategoryModelRequirementsSchema,
    fallbackChains: modelFallbackChainRegistrySchema,
  })
  .strict();

export type ModelRequirementRegistry = z.infer<typeof ModelRequirementRegistrySchema>;

export const FALLBACK_CHAIN_ROUTER_ROUTING: BuiltInModelFallbackChain = {
  chainId: "fallback.router.routing",
  category: "routing",
  steps: [
    {
      providers: ["openai"],
      model: "gpt-4o-mini",
      variant: "low-latency",
      requiresAnyModel: true,
    },
    {
      providers: ["azure-openai", "openai"],
      model: "gpt-4o-mini",
      variant: "default",
      requiresAnyModel: true,
    },
  ],
};

export const FALLBACK_CHAIN_TRIAGE_INVESTIGATION: BuiltInModelFallbackChain = {
  chainId: "fallback.triage.investigation",
  category: "investigation",
  steps: [
    {
      providers: ["openai"],
      model: "gpt-4.1-mini",
      variant: "analysis",
      requiresAnyModel: false,
    },
    {
      providers: ["anthropic"],
      model: "claude-3.5-haiku",
      variant: "analysis",
      requiresAnyModel: false,
    },
  ],
};

export const FALLBACK_CHAIN_INVESTIGATOR_ANALYSIS: BuiltInModelFallbackChain = {
  chainId: "fallback.investigator.analysis",
  category: "investigation",
  steps: [
    {
      providers: ["anthropic"],
      model: "claude-3.5-sonnet",
      variant: "deep-analysis",
      requiresAnyModel: false,
    },
    {
      providers: ["openai", "azure-openai"],
      model: "gpt-4.1",
      variant: "reasoning",
      requiresAnyModel: false,
    },
  ],
};

export const FALLBACK_CHAIN_EVALUATOR_QUALITY: BuiltInModelFallbackChain = {
  chainId: "fallback.evaluator.quality",
  category: "quality",
  steps: [
    {
      providers: ["google"],
      model: "gemini-1.5-pro",
      variant: "critical-review",
      requiresAnyModel: false,
    },
    {
      providers: ["openai"],
      model: "gpt-4.1",
      variant: "quality-check",
      requiresAnyModel: false,
    },
  ],
};

export const FALLBACK_CHAIN_GOVERNOR_GOVERNANCE: BuiltInModelFallbackChain = {
  chainId: "fallback.governor.governance",
  category: "governance",
  steps: [
    {
      providers: ["openai"],
      model: "gpt-4.1",
      variant: "policy-review",
      requiresAnyModel: false,
    },
    {
      providers: ["anthropic", "google"],
      model: "claude-3.5-sonnet",
      variant: "policy-review",
      requiresAnyModel: false,
    },
  ],
};

export const MODEL_REQUIREMENT_FALLBACK_CHAINS: Record<string, BuiltInModelFallbackChain> = {
  [FALLBACK_CHAIN_ROUTER_ROUTING.chainId]: FALLBACK_CHAIN_ROUTER_ROUTING,
  [FALLBACK_CHAIN_TRIAGE_INVESTIGATION.chainId]: FALLBACK_CHAIN_TRIAGE_INVESTIGATION,
  [FALLBACK_CHAIN_INVESTIGATOR_ANALYSIS.chainId]: FALLBACK_CHAIN_INVESTIGATOR_ANALYSIS,
  [FALLBACK_CHAIN_EVALUATOR_QUALITY.chainId]: FALLBACK_CHAIN_EVALUATOR_QUALITY,
  [FALLBACK_CHAIN_GOVERNOR_GOVERNANCE.chainId]: FALLBACK_CHAIN_GOVERNOR_GOVERNANCE,
};

export const BUILT_IN_AGENT_MODEL_REQUIREMENTS: BuiltInAgentModelRequirement[] = [
  { agentId: "agent.router", chainId: FALLBACK_CHAIN_ROUTER_ROUTING.chainId },
  { agentId: "agent.triage", chainId: FALLBACK_CHAIN_TRIAGE_INVESTIGATION.chainId },
  { agentId: "agent.investigator", chainId: FALLBACK_CHAIN_INVESTIGATOR_ANALYSIS.chainId },
  { agentId: "agent.evaluator", chainId: FALLBACK_CHAIN_EVALUATOR_QUALITY.chainId },
  { agentId: "agent.governor", chainId: FALLBACK_CHAIN_GOVERNOR_GOVERNANCE.chainId },
];

export const BUILT_IN_CATEGORY_MODEL_REQUIREMENTS: BuiltInCategoryModelRequirement[] = [
  { category: "routing", chainId: FALLBACK_CHAIN_ROUTER_ROUTING.chainId },
  { category: "investigation", chainId: FALLBACK_CHAIN_INVESTIGATOR_ANALYSIS.chainId },
  { category: "quality", chainId: FALLBACK_CHAIN_EVALUATOR_QUALITY.chainId },
  { category: "governance", chainId: FALLBACK_CHAIN_GOVERNOR_GOVERNANCE.chainId },
];

export const MODEL_REQUIREMENT_REGISTRY: ModelRequirementRegistry = ModelRequirementRegistrySchema.parse({
  agentRequirements: BUILT_IN_AGENT_MODEL_REQUIREMENTS,
  categoryRequirements: BUILT_IN_CATEGORY_MODEL_REQUIREMENTS,
  fallbackChains: MODEL_REQUIREMENT_FALLBACK_CHAINS,
});

export const getAgentModelFallbackChain = (agentId: string): BuiltInModelFallbackChain | undefined => {
  const declaration = MODEL_REQUIREMENT_REGISTRY.agentRequirements.find((entry) => entry.agentId === agentId);
  if (!declaration) {
    return undefined;
  }
  return MODEL_REQUIREMENT_REGISTRY.fallbackChains[declaration.chainId];
};

export const getCategoryModelFallbackChain = (category: string): BuiltInModelFallbackChain | undefined => {
  const declaration = MODEL_REQUIREMENT_REGISTRY.categoryRequirements.find((entry) => entry.category === category);
  if (!declaration) {
    return undefined;
  }
  return MODEL_REQUIREMENT_REGISTRY.fallbackChains[declaration.chainId];
};
