import { z } from "zod";

import {
  AgentOverridesSchema,
  type AgentOverrides,
} from "./agent-overrides";
import {
  BackgroundTaskLimitsSchema,
  type BackgroundTaskLimits,
} from "./background-task";

export const OhMyOpenAgentConfigSchema = z
  .object({
    agentOverrides: AgentOverridesSchema,
    backgroundTaskLimits: BackgroundTaskLimitsSchema,
    environment: z
      .string()
      .trim()
      .min(1)
      .default("default"),
    telemetryEnabled: z.boolean().default(false),
    features: z
      .object({
        allowRemoteAgents: z.boolean().default(false),
        allowAutoRepair: z.boolean().default(false),
      })
      .default({}),
  })
  .strict();

export type OhMyOpenAgentConfig = z.infer<
  typeof OhMyOpenAgentConfigSchema
>;

export type OhMyOpenAgentConfigParts = {
  agentOverrides: AgentOverrides;
  backgroundTaskLimits: BackgroundTaskLimits;
};
