import { z } from "zod";

export const BackgroundTaskLimitsSchema = z
  .object({
    maxConcurrentTasks: z
      .number()
      .int("maxConcurrentTasks must be an integer")
      .nonnegative("maxConcurrentTasks must be 0 or greater")
      .default(4),
    maxRetriesPerTask: z
      .number()
      .int("maxRetriesPerTask must be an integer")
      .nonnegative("maxRetriesPerTask must be 0 or greater")
      .default(3),
    defaultTaskTimeoutMs: z
      .number()
      .int("defaultTaskTimeoutMs must be an integer")
      .positive("defaultTaskTimeoutMs must be greater than 0")
      .default(60_000),
    queueCapacity: z
      .number()
      .int("queueCapacity must be an integer")
      .nonnegative("queueCapacity must be 0 or greater")
      .default(1024),
  })
  .strict();

export type BackgroundTaskLimits = z.infer<typeof BackgroundTaskLimitsSchema>;
