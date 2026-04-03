import { z } from "zod";

export const PromptConditionOperatorSchema = z.enum([
  "contains",
  "equals",
  "matches",
  "not",
  "gte",
  "lte",
  "within_budget",
]);

export type PromptConditionOperator = z.infer<typeof PromptConditionOperatorSchema>;

export const PromptConditionSchema = z.object({
  key: z.string().min(1),
  operator: PromptConditionOperatorSchema,
  value: z.unknown(),
  notes: z.string().max(240).optional(),
});

export type PromptCondition = z.infer<typeof PromptConditionSchema>;

export const CostHintSchema = z.object({
  unit: z.enum(["usd_per_1k_tokens", "usd_per_1k_items", "qualitative"]),
  input: z.number().nonnegative().optional(),
  output: z.number().nonnegative().optional(),
  latencyMs: z.number().int().nonnegative().optional(),
  confidence: z.number().min(0).max(1),
  tags: z.array(z.string().min(1)).default([]),
});

export type CostHint = z.infer<typeof CostHintSchema>;

export const OrchestrationHintSchema = z.object({
  mode: z.enum(["serial", "parallel", "bounded_parallel"]),
  maxParallelism: z.number().int().positive().default(1),
  requiresApproval: z.boolean().default(false),
  retryPolicy: z
    .enum(["none", "single", "bounded", "exponential_jitter"])
    .default("single"),
  retryBudget: z.number().int().nonnegative().default(1),
  timeoutMs: z.number().int().positive().default(30_000),
  canRunInBackground: z.boolean().default(false),
  fanOutAllowed: z.boolean().default(false),
  notes: z.string().max(200).optional(),
});

export type OrchestrationHint = z.infer<typeof OrchestrationHintSchema>;

export const AgentPromptMetadataSchema = z.object({
  id: z.string().min(1),
  displayName: z.string().min(1),
  promptId: z.string().min(1),
  description: z.string().max(300).default(""),
  category: z.string().min(1),
  triggers: z.array(PromptConditionSchema).default([]),
  useWhen: z.array(PromptConditionSchema).default([]),
  avoidWhen: z.array(PromptConditionSchema).default([]),
  cost: CostHintSchema,
  orchestration: OrchestrationHintSchema,
  enabled: z.boolean().default(true),
  version: z.string().default("1.0.0"),
  tags: z.array(z.string().min(1)).default([]),
});

export type AgentPromptMetadata = z.infer<typeof AgentPromptMetadataSchema>;

export const PromptCategorySchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1),
  description: z.string().max(240),
  promptIds: z.array(z.string().min(1)).default([]),
  defaultAgentIds: z.array(z.string().min(1)).default([]),
  fallbackAgentIds: z.array(z.string().min(1)).default([]),
  priority: z.number().int().nonnegative().default(0),
});

export type PromptCategory = z.infer<typeof PromptCategorySchema>;

export const TriggeredPromptContextSchema = z.object({
  goal: z.string().default("inspect"),
  text: z.string().default(""),
  confidence: z.number().min(0).max(1).default(1),
  risk: z.number().min(0).max(1).default(0),
  urgency: z.number().min(0).max(10).default(5),
  requiresApproval: z.boolean().default(false),
  budgetUsdPerInvocation: z.number().nonnegative().optional(),
  hasPrivateData: z.boolean().default(false),
  tags: z.array(z.string()).default([]),
});

export type TriggeredPromptContext = z.infer<typeof TriggeredPromptContextSchema>;

export const BuiltInAgentPromptRegistrySchema = z.record(AgentPromptMetadataSchema);
export type BuiltInAgentPromptRegistry = z.infer<
  typeof BuiltInAgentPromptRegistrySchema
>;

export const BuiltInPromptCategoryRegistrySchema = z.record(PromptCategorySchema);
export type BuiltInPromptCategoryRegistry = z.infer<
  typeof BuiltInPromptCategoryRegistrySchema
>;

export const builtInAgentPrompts = {
  router: {
    id: "agent.router",
    displayName: "Router",
    promptId: "prompt.router.v1",
    description: "Routes incoming tasks to the best-fit agent based on explicit constraints.",
    category: "routing",
    triggers: [
      {
        key: "goal",
        operator: "contains",
        value: ["route", "delegate", "assign"],
        notes: "Task expresses dispatch intent",
      },
      {
        key: "risk",
        operator: "gte",
        value: 0.55,
        notes: "Non-trivial risk warrants structured routing",
      },
    ],
    useWhen: [
      {
        key: "confidence",
        operator: "gte",
        value: 0.6,
        notes: "Use when routing confidence is high",
      },
    ],
    avoidWhen: [
      {
        key: "hasPrivateData",
        operator: "equals",
        value: true,
        notes: "Avoid automated routing with sensitive context",
      },
    ],
    cost: {
      unit: "qualitative",
      confidence: 0.88,
      tags: ["cheap", "deterministic"],
    },
    orchestration: {
      mode: "parallel",
      maxParallelism: 2,
      requiresApproval: false,
      retryPolicy: "single",
      retryBudget: 1,
      timeoutMs: 10_000,
      canRunInBackground: true,
      fanOutAllowed: false,
    },
    enabled: true,
    version: "1.1.0",
    tags: ["routing", "control-plane", "cheap"],
  },
  triage: {
    id: "agent.triage",
    displayName: "Triage",
    promptId: "prompt.triage.v1",
    description: "Classifies incidents and prepares investigation context quickly.",
    category: "investigation",
    triggers: [
      {
        key: "goal",
        operator: "contains",
        value: ["bug", "incident", "anomaly", "alert"],
        notes: "Likely incident workflow",
      },
      {
        key: "urgency",
        operator: "gte",
        value: 6,
        notes: "Elevated urgency requires triage",
      },
    ],
    useWhen: [
      {
        key: "tags",
        operator: "contains",
        value: ["ops", "incident", "telemetry"],
        notes: "Operational context suggests triage first",
      },
    ],
    avoidWhen: [
      {
        key: "risk",
        operator: "gte",
        value: 0.9,
        notes: "High-risk changes should escalate immediately",
      },
      {
        key: "goal",
        operator: "contains",
        value: ["compliance", "policy"],
        notes: "Governance path uses dedicated agent",
      },
    ],
    cost: {
      unit: "qualitative",
      confidence: 0.83,
      tags: ["low-cost", "high-throughput"],
    },
    orchestration: {
      mode: "bounded_parallel",
      maxParallelism: 2,
      requiresApproval: false,
      retryPolicy: "bounded",
      retryBudget: 2,
      timeoutMs: 20_000,
      canRunInBackground: true,
      fanOutAllowed: true,
      notes: "Runs quick classification and evidence prefetch in parallel",
    },
    enabled: true,
    version: "1.0.1",
    tags: ["triage", "classification", "investigation"],
  },
  investigator: {
    id: "agent.investigator",
    displayName: "Investigator",
    promptId: "prompt.investigator.v2",
    description: "Collects evidence, links causality, and proposes the narrowest fix path.",
    category: "investigation",
    triggers: [
      {
        key: "goal",
        operator: "contains",
        value: ["root cause", "investigate", "debug", "analysis"],
        notes: "Explicit diagnostic intent",
      },
      {
        key: "confidence",
        operator: "lte",
        value: 0.8,
        notes: "Use when certainty is moderate",
      },
    ],
    useWhen: [
      {
        key: "tags",
        operator: "contains",
        value: ["telemetry", "logs", "trace"],
        notes: "Structured evidence available",
      },
      {
        key: "risk",
        operator: "lte",
        value: 0.9,
        notes: "Balanced-risk investigations are safe for this agent",
      },
    ],
    avoidWhen: [
      {
        key: "hasPrivateData",
        operator: "equals",
        value: true,
        notes: "Private data should pass policy gates first",
      },
      {
        key: "goal",
        operator: "contains",
        value: ["approve", "sign-off", "final decision"],
        notes: "Gating decisions are for governance",
      },
    ],
    cost: {
      unit: "usd_per_1k_tokens",
      input: 0.035,
      output: 0.12,
      confidence: 0.79,
      tags: ["medium-cost", "deep"],
    },
    orchestration: {
      mode: "serial",
      maxParallelism: 1,
      requiresApproval: false,
      retryPolicy: "single",
      retryBudget: 1,
      timeoutMs: 45_000,
      canRunInBackground: false,
      fanOutAllowed: false,
    },
    enabled: true,
    version: "2.0.0",
    tags: ["analysis", "evidence", "cause"],
  },
  evaluator: {
    id: "agent.evaluator",
    displayName: "Evaluator",
    promptId: "prompt.evaluator.v1",
    description:
      "Critically evaluates proposed solutions and checks risk, compliance, and regressions.",
    category: "quality",
    triggers: [
      {
        key: "goal",
        operator: "contains",
        value: ["evaluate", "review", "validate", "assess"],
        notes: "Review intent explicitly present",
      },
      {
        key: "risk",
        operator: "gte",
        value: 0.5,
        notes: "Higher-risk work needs evaluation gate",
      },
    ],
    useWhen: [
      {
        key: "tags",
        operator: "contains",
        value: ["quality", "release", "release-readiness"],
        notes: "Use for quality gate passes",
      },
    ],
    avoidWhen: [
      {
        key: "urgency",
        operator: "gte",
        value: 9,
        notes: "Critical emergencies can use temporary bypass flow",
      },
    ],
    cost: {
      unit: "usd_per_1k_tokens",
      input: 0.05,
      output: 0.1,
      confidence: 0.86,
      tags: ["quality", "safety"],
    },
    orchestration: {
      mode: "serial",
      maxParallelism: 1,
      requiresApproval: true,
      retryPolicy: "bounded",
      retryBudget: 2,
      timeoutMs: 30_000,
      canRunInBackground: false,
      fanOutAllowed: false,
      notes: "Requires explicit approval gating for risky changes",
    },
    enabled: true,
    version: "1.1.2",
    tags: ["governance", "quality", "safety"],
  },
  governor: {
    id: "agent.governor",
    displayName: "Governor",
    promptId: "prompt.governor.v1",
    description:
      "Applies policy gates, approvals, and escalation conditions before rollout.",
    category: "governance",
    triggers: [
      {
        key: "goal",
        operator: "contains",
        value: ["policy", "approve", "govern", "compliance"],
        notes: "Policy-oriented intent",
      },
      {
        key: "risk",
        operator: "gte",
        value: 0.7,
        notes: "Elevated risk needs governance",
      },
    ],
    useWhen: [
      {
        key: "requiresApproval",
        operator: "equals",
        value: true,
        notes: "Explicit approval required",
      },
    ],
    avoidWhen: [
      {
        key: "goal",
        operator: "contains",
        value: ["investigate", "triage", "diagnose"],
        notes: "Use workflow agents first",
      },
      {
        key: "urgency",
        operator: "gte",
        value: 9,
        notes: "Emergency paths can run with temporary manual override",
      },
    ],
    cost: {
      unit: "qualitative",
      confidence: 0.9,
      tags: ["high-value", "control-plane"],
    },
    orchestration: {
      mode: "serial",
      maxParallelism: 1,
      requiresApproval: true,
      retryPolicy: "exponential_jitter",
      retryBudget: 3,
      timeoutMs: 60_000,
      canRunInBackground: false,
      fanOutAllowed: false,
    },
    enabled: true,
    version: "1.0.0",
    tags: ["compliance", "governance", "policy"],
  },
} as const satisfies BuiltInAgentPromptRegistry;

export const builtInPromptCategories: BuiltInPromptCategoryRegistry = {
  routing: {
    id: "routing",
    name: "Routing",
    description: "Decision routing and lightweight dispatching prompts.",
    promptIds: [builtInAgentPrompts.router.promptId],
    defaultAgentIds: [builtInAgentPrompts.router.id],
    fallbackAgentIds: [builtInAgentPrompts.triage.id],
    priority: 90,
  },
  investigation: {
    id: "investigation",
    name: "Investigation",
    description: "Prompt metadata for discovery, evidence gathering, and analysis.",
    promptIds: [
      builtInAgentPrompts.triage.promptId,
      builtInAgentPrompts.investigator.promptId,
    ],
    defaultAgentIds: [builtInAgentPrompts.investigator.id],
    fallbackAgentIds: [builtInAgentPrompts.triage.id],
    priority: 80,
  },
  quality: {
    id: "quality",
    name: "Quality",
    description: "Validation and verification prompts.",
    promptIds: [builtInAgentPrompts.evaluator.promptId],
    defaultAgentIds: [builtInAgentPrompts.evaluator.id],
    fallbackAgentIds: [builtInAgentPrompts.investigator.id],
    priority: 70,
  },
  governance: {
    id: "governance",
    name: "Governance",
    description: "Policy, approvals, and escalation-oriented prompts.",
    promptIds: [builtInAgentPrompts.governor.promptId],
    defaultAgentIds: [builtInAgentPrompts.governor.id],
    fallbackAgentIds: [builtInAgentPrompts.evaluator.id],
    priority: 100,
  },
} as const satisfies BuiltInPromptCategoryRegistry;

const compareValue = (
  operator: Exclude<
    PromptConditionOperator,
    "contains" | "matches" | "not" | "within_budget"
  >,
  left: unknown,
  right: unknown,
): boolean => {
  if (typeof left !== "number" || typeof right !== "number") {
    return false;
  }

  if (operator === "gte") {
    return left >= right;
  }

  if (operator === "lte") {
    return left <= right;
  }

  return left === right;
};

const withinBudget = (budget: unknown, threshold: unknown): boolean => {
  if (typeof budget !== "number" || typeof threshold !== "number") {
    return false;
  }

  return budget <= threshold;
};

const evaluateCondition = (
  condition: PromptCondition,
  context: TriggeredPromptContext,
): boolean => {
  const contextValue = (context as Record<string, unknown>)[condition.key];

  if (condition.operator === "contains") {
    if (typeof contextValue === "string") {
      const contextText = String(contextValue).toLowerCase();
      const needles = Array.isArray(condition.value)
        ? condition.value
        : [condition.value];

      return needles.every((needle) => {
        if (needle == null) {
          return false;
        }

        return contextText.includes(String(needle).toLowerCase());
      });
    }

    if (!Array.isArray(contextValue)) {
      return false;
    }

    const needles = Array.isArray(condition.value) ? condition.value : [condition.value];
    return needles.every((needle) => {
      return contextValue.some((candidate) => {
        if (needle == null) {
          return false;
        }

        return String(candidate)
          .toLowerCase()
          .includes(String(needle).toLowerCase());
      });
    });
  }

  if (condition.operator === "matches") {
    if (typeof contextValue !== "string" || typeof condition.value !== "string") {
      return false;
    }

    try {
      return new RegExp(condition.value, "i").test(contextValue);
    } catch {
      return false;
    }
  }

  if (condition.operator === "not") {
    return contextValue !== condition.value;
  }

  if (condition.operator === "within_budget") {
    return withinBudget(context.budgetUsdPerInvocation, condition.value);
  }

  return compareValue(
    condition.operator,
    contextValue,
    condition.value,
  );
};

const evaluateAll = (
  conditions: PromptCondition[],
  context: TriggeredPromptContext,
): boolean => {
  return conditions.every((condition) => evaluateCondition(condition, context));
};

export const validateAgentPromptMetadata = (
  metadata: unknown,
): AgentPromptMetadata => {
  return AgentPromptMetadataSchema.parse(metadata);
};

export const validatePromptCategory = (metadata: unknown): PromptCategory => {
  return PromptCategorySchema.parse(metadata);
};

export const listBuiltInAgents = (): AgentPromptMetadata[] => {
  return Object.values(builtInAgentPrompts);
};

export const listBuiltInCategories = (): PromptCategory[] => {
  return Object.values(builtInPromptCategories).sort(
    (a, b) => b.priority - a.priority,
  );
};

export const getBuiltInAgent = (
  agentId: string,
): AgentPromptMetadata | undefined => {
  const prompt =
    builtInAgentPrompts[agentId as keyof typeof builtInAgentPrompts];

  if (prompt) {
    return prompt;
  }

  return listBuiltInAgents().find((metadata) => metadata.id === agentId);
};

export const getBuiltInCategory = (
  categoryId: string,
): PromptCategory | undefined => {
  return builtInPromptCategories[categoryId as keyof typeof builtInPromptCategories];
};

export const agentsForUse = (
  context: TriggeredPromptContext,
): AgentPromptMetadata[] => {
  const normalizedContext = TriggeredPromptContextSchema.parse(context);
  const candidates = listBuiltInAgents();

  return candidates.filter((agent) => {
    if (!agent.enabled) {
      return false;
    }

    const triggerMatch =
      agent.triggers.length === 0 || evaluateAll(agent.triggers, normalizedContext);

    const useMatch =
      agent.useWhen.length === 0 || evaluateAll(agent.useWhen, normalizedContext);

    const avoidMatch = evaluateAll(agent.avoidWhen, normalizedContext);

    return triggerMatch && useMatch && !avoidMatch;
  });
};

export const preferredAgentForCategory = (
  categoryId: string,
  context: TriggeredPromptContext,
): AgentPromptMetadata | undefined => {
  const candidates = getBuiltInCategory(categoryId);
  if (!candidates) {
    return undefined;
  }

  const usableAgents = agentsForUse(context);

  for (const id of candidates.defaultAgentIds) {
    const agent = getBuiltInAgent(id);
    if (agent && usableAgents.includes(agent)) {
      return agent;
    }
  }

  for (const id of candidates.fallbackAgentIds) {
    const agent = getBuiltInAgent(id);
    if (agent && usableAgents.includes(agent)) {
      return agent;
    }
  }

  return undefined;
};

export const orchestrationByAgentId = (
  agentId: string,
): OrchestrationHint | undefined => {
  return getBuiltInAgent(agentId)?.orchestration;
};

export const costByAgentId = (agentId: string): CostHint | undefined => {
  return getBuiltInAgent(agentId)?.cost;
};
