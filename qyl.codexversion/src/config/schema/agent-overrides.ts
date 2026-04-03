import { z } from "zod";

import { BackgroundTaskLimitsSchema } from "./background-task";

export const AgentOverrideSchema = z
  .object({
    enabled: z.boolean().optional(),
    label: z.string().trim().min(1).optional(),
    model: z.string().trim().min(1).optional(),
    backgroundTaskLimits: BackgroundTaskLimitsSchema.partial().optional(),
    tags: z.array(z.string().trim().min(1)).default([]),
    options: z.record(z.unknown()).default({}),
  })
  .strict();

export const AgentOverridesSchema = z.record(
  z.string().trim().min(1),
  AgentOverrideSchema,
).default({});

export type AgentOverride = z.infer<typeof AgentOverrideSchema>;
export type AgentOverrides = z.infer<typeof AgentOverridesSchema>;
