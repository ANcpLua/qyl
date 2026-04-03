import { z } from "zod";

export const ISODateTime = z.string().datetime({ offset: true });

export const TraceContextSchema = z.object({
  requestId: z.string().min(1),
  sessionId: z.string().min(1),
  correlationId: z.string().optional(),
});

export type TraceContext = z.infer<typeof TraceContextSchema>;

export const AgentKindSchema = z.enum([
  "orchestrator",
  "operator",
  "investigator",
  "delegate",
  "evaluator",
  "worker",
]);

export type AgentKind = z.infer<typeof AgentKindSchema>;

export const AgentCapabilitySchema = z.object({
  area: z.string().min(1),
  level: z.number().int().min(1).max(5),
});

export type AgentCapability = z.infer<typeof AgentCapabilitySchema>;

export const AgentPolicySchema = z.object({
  maxDelegationsPerHour: z.number().int().min(0).default(50),
  maxConcurrentTasks: z.number().int().positive().default(3),
  allowedRegions: z.array(z.string()).default([]),
  complianceTags: z.array(z.string()).default([]),
});

export type AgentPolicy = z.infer<typeof AgentPolicySchema>;

export const AgentContractSchema = z.object({
  agentId: z.string().min(1),
  kind: AgentKindSchema,
  name: z.string().min(1),
  version: z.string().min(1),
  owner: z.string().optional(),
  capabilities: z.array(AgentCapabilitySchema).default([]),
  policy: AgentPolicySchema,
  metadata: z.record(z.string(), z.unknown()).default({}),
  enabled: z.boolean().default(true),
  createdAt: ISODateTime,
  updatedAt: ISODateTime,
  deprecatedAt: ISODateTime.optional(),
});

export type AgentContract = z.infer<typeof AgentContractSchema>;

export const SessionStateSchema = z.enum([
  "queued",
  "running",
  "awaiting_input",
  "paused",
  "completed",
  "failed",
  "cancelled",
]);

export type SessionState = z.infer<typeof SessionStateSchema>;

export const SessionPrioritySchema = z.enum(["low", "medium", "high", "critical"]);

export type SessionPriority = z.infer<typeof SessionPrioritySchema>;

export const AgentSessionContractSchema = z.object({
  sessionId: z.string().min(1),
  parentSessionId: z.string().optional(),
  rootAgentId: z.string().min(1),
  subjectAgentId: z.string().optional(),
  state: SessionStateSchema,
  priority: SessionPrioritySchema.default("medium"),
  labels: z.array(z.string()).default([]),
  context: z.record(z.string(), z.unknown()).default({}),
  trace: TraceContextSchema,
  startedAt: ISODateTime.optional(),
  updatedAt: ISODateTime,
  completedAt: ISODateTime.optional(),
  expiresAt: ISODateTime.optional(),
});

export type AgentSessionContract = z.infer<typeof AgentSessionContractSchema>;

export const DelegateRequestStatusSchema = z.enum([
  "created",
  "dispatched",
  "accepted",
  "in_progress",
  "returned",
  "rejected",
  "timed_out",
]);

export type DelegateRequestStatus = z.infer<typeof DelegateRequestStatusSchema>;

export const DelegatePrioritySchema = z.enum(["low", "normal", "high", "urgent"]);

export type DelegatePriority = z.infer<typeof DelegatePrioritySchema>;

export const DelegateChannelSchema = z.enum(["sync", "async", "batch"]);

export type DelegateChannel = z.infer<typeof DelegateChannelSchema>;

export const DelegateRequestContractSchema = z.object({
  requestId: z.string().min(1),
  sessionId: z.string().min(1),
  fromAgentId: z.string().min(1),
  toAgentId: z.string().optional(),
  toAgentKind: AgentKindSchema.optional(),
  channel: DelegateChannelSchema.default("async"),
  priority: DelegatePrioritySchema.default("normal"),
  subject: z.string().min(1),
  rationale: z.string().min(1),
  instructions: z.string().min(1),
  requirements: z.array(z.string()).default([]),
  payload: z.record(z.string(), z.unknown()).default({}),
  status: DelegateRequestStatusSchema.default("created"),
  delegatedAt: ISODateTime,
  deadlineAt: ISODateTime.optional(),
  responseDeadlineMs: z.number().int().positive().default(30_000),
  trace: TraceContextSchema,
  metadata: z.record(z.string(), z.unknown()).default({}),
});

export type DelegateRequestContract = z.infer<typeof DelegateRequestContractSchema>;

export const DelegateResponseContractSchema = z.object({
  requestId: z.string().min(1),
  fromAgentId: z.string().min(1),
  toAgentId: z.string().min(1),
  status: z.enum(["accepted", "rejected", "returned"]),
  notes: z.string().optional(),
  confidence: z.number().min(0).max(1).optional(),
  resultSummary: z.record(z.string(), z.unknown()).default({}),
  createdAt: ISODateTime,
  trace: TraceContextSchema,
});

export type DelegateResponseContract = z.infer<typeof DelegateResponseContractSchema>;

export const ConcurrencyShapeSchema = z.object({
  maxConcurrent: z.number().int().positive(),
  maxQueued: z.number().int().nonnegative().default(1000),
  burstAllowance: z.number().int().nonnegative().default(0),
  queueTimeoutMs: z.number().int().positive().default(60_000),
  retryLimit: z.number().int().nonnegative().default(3),
  retryBackoffMs: z.number().int().nonnegative().default(1_000),
  cancellationGraceMs: z.number().int().nonnegative().default(5_000),
});

export type ConcurrencyShape = z.infer<typeof ConcurrencyShapeSchema>;

export const BackoffPolicySchema = z.enum(["fixed", "linear", "exponential"]);

export type BackoffPolicy = z.infer<typeof BackoffPolicySchema>;

export const BackgroundTaskStateSchema = z.enum([
  "scheduled",
  "queued",
  "running",
  "retrying",
  "succeeded",
  "failed",
  "cancelled",
  "dead_lettered",
]);

export type BackgroundTaskState = z.infer<typeof BackgroundTaskStateSchema>;

export const BackgroundTaskContractSchema = z.object({
  taskId: z.string().min(1),
  sessionId: z.string().min(1),
  ownerAgentId: z.string().min(1),
  kind: z.string().min(1),
  target: z.string().min(1),
  input: z.record(z.string(), z.unknown()).default({}),
  state: BackgroundTaskStateSchema,
  lockToken: z.string().optional(),
  attempt: z.number().int().nonnegative().default(0),
  limits: ConcurrencyShapeSchema,
  backoff: BackoffPolicySchema.default("exponential"),
  startedAt: ISODateTime.optional(),
  updatedAt: ISODateTime,
  finishedAt: ISODateTime.optional(),
  progress: z.number().min(0).max(1).default(0),
  progressNotes: z.string().max(2048).optional(),
  error: z
    .object({
      code: z.string(),
      message: z.string(),
      retriable: z.boolean(),
      details: z.record(z.string(), z.unknown()).default({}),
    })
    .optional(),
  trace: TraceContextSchema,
});

export type BackgroundTaskContract = z.infer<typeof BackgroundTaskContractSchema>;

export const ConcurrencyLimitsContractSchema = z.object({
  global: ConcurrencyShapeSchema,
  byAgentKind: z.record(AgentKindSchema, ConcurrencyShapeSchema).default({}),
  bySessionPriority: z.record(SessionPrioritySchema, ConcurrencyShapeSchema).default({}),
  byTaskKind: z.record(z.string(), ConcurrencyShapeSchema).default({}),
  updatedAt: ISODateTime,
});

export type ConcurrencyLimitsContract = z.infer<typeof ConcurrencyLimitsContractSchema>;

export const ModelReferenceSchema = z.object({
  provider: z.string().min(1),
  model: z.string().min(1),
  family: z.string().optional(),
  capabilities: z.array(z.string()).default([]),
  rateLimitKey: z.string().optional(),
  region: z.string().optional(),
  tags: z.array(z.string()).default([]),
});

export type ModelReference = z.infer<typeof ModelReferenceSchema>;

export const FallbackSelectorSchema = z.object({
  mode: z.enum(["round_robin", "least_latency", "highest_quality", "cost_priority"]),
  maxOutputTokens: z.number().int().positive(),
  budgetUsdPerInvocation: z.number().positive().optional(),
  timeoutMs: z.number().int().positive(),
});

export type FallbackSelector = z.infer<typeof FallbackSelectorSchema>;

export const ModelFallbackStepSchema = z.object({
  model: ModelReferenceSchema,
  role: z.enum(["primary", "fallback", "backup"]),
  selector: FallbackSelectorSchema,
  requiredForTaskTypes: z.array(z.string()).default([]),
  reasons: z.array(z.string()).default([]),
  cooldownMs: z.number().int().nonnegative().default(0),
  maxRetries: z.number().int().nonnegative().default(2),
});

export type ModelFallbackStep = z.infer<typeof ModelFallbackStepSchema>;

export const ModelFallbackChainContractSchema = z.object({
  chainId: z.string().min(1),
  tenantScope: z.string().optional(),
  taskType: z.string().min(1),
  steps: z.array(ModelFallbackStepSchema).min(1),
  effectiveFrom: ISODateTime.optional(),
  effectiveTo: ISODateTime.optional(),
  disabled: z.boolean().default(false),
  createdBy: z.string().min(1),
  trace: TraceContextSchema,
});

export type ModelFallbackChainContract = z.infer<typeof ModelFallbackChainContractSchema>;

export const PromptVariableSchema = z.object({
  name: z.string().min(1),
  required: z.boolean().default(false),
  type: z.enum(["string", "number", "boolean", "json"]),
  defaultValue: z.unknown().optional(),
});

export type PromptVariable = z.infer<typeof PromptVariableSchema>;

export const PromptMetadataSchema = z.object({
  promptId: z.string().min(1),
  agentId: z.string().min(1),
  title: z.string().min(1),
  templateVersion: z.string().min(1),
  templateRef: z.string().url(),
  checksum: z.string().min(1),
  locale: z.string().default("en-US"),
  maxTokens: z.number().int().positive(),
  temperature: z.number().min(0).max(2),
  topP: z.number().min(0).max(1),
  toolsAllowed: z.array(z.string()).default([]),
  variables: z.array(PromptVariableSchema).default([]),
  safetyFlags: z.array(z.string()).default([]),
  createdAt: ISODateTime,
  updatedAt: ISODateTime,
  notes: z.string().max(2048).optional(),
  trace: TraceContextSchema,
});

export type PromptMetadata = z.infer<typeof PromptMetadataSchema>;

export const ExecutionOutcomeSchema = z.enum(["success", "partial", "failure", "skipped", "deferred"]);

export type ExecutionOutcome = z.infer<typeof ExecutionOutcomeSchema>;

export const ExecutionArtifactSchema = z.object({
  artifactId: z.string().min(1),
  kind: z.string().min(1),
  uri: z.string().url(),
  metadata: z.record(z.string(), z.unknown()).default({}),
});

export type ExecutionArtifact = z.infer<typeof ExecutionArtifactSchema>;

export const ExecutionResultContractSchema = z.object({
  executionId: z.string().min(1),
  sessionId: z.string().min(1),
  agentId: z.string().min(1),
  requestId: z.string().optional(),
  taskId: z.string().optional(),
  sessionState: SessionStateSchema,
  outcome: ExecutionOutcomeSchema,
  message: z.string().max(2048).optional(),
  startedAt: ISODateTime.optional(),
  completedAt: ISODateTime.optional(),
  latencyMs: z.number().int().nonnegative().optional(),
  modelChainId: z.string().optional(),
  usedModels: z.array(ModelReferenceSchema).default([]),
  promptId: z.string().optional(),
  tokens: z
    .object({
      input: z.number().int().nonnegative().optional(),
      output: z.number().int().nonnegative().optional(),
      total: z.number().int().nonnegative().optional(),
      costUsd: z.number().nonnegative().optional(),
    })
    .default({}),
  artifacts: z.array(ExecutionArtifactSchema).default([]),
  checks: z
    .array(
      z.object({
        name: z.string().min(1),
        outcome: z.boolean(),
        reason: z.string().optional(),
      }),
    )
    .default([]),
  trace: TraceContextSchema,
});

export type ExecutionResultContract = z.infer<typeof ExecutionResultContractSchema>;

export const contractsSchemaMap = {
  agent: AgentContractSchema,
  session: AgentSessionContractSchema,
  delegateRequest: DelegateRequestContractSchema,
  delegateResponse: DelegateResponseContractSchema,
  backgroundTask: BackgroundTaskContractSchema,
  concurrencyLimits: ConcurrencyLimitsContractSchema,
  modelFallbackChain: ModelFallbackChainContractSchema,
  promptMetadata: PromptMetadataSchema,
  executionResult: ExecutionResultContractSchema,
} as const;

export type ContractMap = typeof contractsSchemaMap;
