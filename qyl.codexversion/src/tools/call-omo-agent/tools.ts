import { z } from "zod";

import {
  BackgroundExecutor,
  type BackgroundExecutorManager,
  type BackgroundTaskStatus,
  type ContinuityState,
} from "./background-executor";

const ROUTING_MODES = ["auto", "sync", "background"] as const;
type RoutingMode = (typeof ROUTING_MODES)[number];

const ContinuityCheckpointSchema = z.unknown().optional();

const RoutingPolicySchema = z
  .object({
    mode: z.enum(ROUTING_MODES).default("auto"),
    max_sync_wait_ms: z.number().int().positive().default(1500),
    estimated_duration_ms: z.number().int().nonnegative().optional(),
    request_background: z.boolean().default(false),
    request_id: z.string().trim().min(1).optional(),
    task_id: z.string().trim().min(1).optional(),
  })
  .strict();

const PayloadSchema = z.record(z.string(), z.unknown()).default({});
const MetadataSchema = z.record(z.string(), z.unknown()).default({});
const MessageInputSchema = z
  .object({
    instructions: z.string().trim().min(1).optional(),
    prompt: z.string().trim().min(1).optional(),
    task: z.string().trim().min(1).optional(),
  })
  .refine(
    (value) =>
      value.instructions !== undefined || value.prompt !== undefined || value.task !== undefined,
    {
      path: ["instructions"],
      message: "One of `instructions`, `prompt`, or `task` must be provided.",
    }
  );

export const CallOmoAgentRequestSchema = MessageInputSchema.extend({
  session_id: z.string().trim().min(1),
  agent_id: z.string().trim().min(1).optional(),
  continuity_token: z.string().trim().min(1).optional(),
  continuation_checkpoint: ContinuityCheckpointSchema,
  routing: RoutingPolicySchema.default({}),
  payload: PayloadSchema,
  metadata: MetadataSchema,
}).strict().transform((value) => {
  const instructions = value.instructions ?? value.prompt ?? value.task;
  if (instructions === undefined) {
    throw new Error("Unable to normalize call instruction text.");
  }

  const { prompt, task, ...rest } = value;
  return {
    ...rest,
    instructions,
  };
});

export type CallOmoAgentRequest = z.infer<typeof CallOmoAgentRequestSchema>;

export type OmoAgentExecutionMode = Exclude<RoutingMode, "auto">;

export const CallOmoAgentResponseContinuitySchema = z.object({
  session_id: z.string().min(1),
  continuity_token: z.string().min(1),
  continuation_checkpoint: ContinuityCheckpointSchema,
});

export type CallOmoAgentResponseContinuity = z.infer<
  typeof CallOmoAgentResponseContinuitySchema
>;

export interface CallOmoAgentExecutionHooks<TProgress, TOutput> {
  onBeforeSync?: (request: CallOmoAgentRequest) => Promise<void> | void;
  onAfterSync?: (
    request: CallOmoAgentRequest,
    output: TOutput,
  ) => Promise<void> | void;
  onBackgroundQueued?: (taskId: string, request: CallOmoAgentRequest) => Promise<void> | void;
  onBackgroundStarted?: (taskId: string, request: CallOmoAgentRequest) => Promise<void> | void;
  onBackgroundProgress?: (
    taskId: string,
    request: CallOmoAgentRequest,
    progress: TProgress,
  ) => Promise<void> | void;
  onBackgroundCompleted?: (
    taskId: string,
    request: CallOmoAgentRequest,
    output: TOutput,
  ) => Promise<void> | void;
  onBackgroundFailed?: (taskId: string, request: CallOmoAgentRequest, error: Error) => Promise<void> | void;
  onBackgroundCanceled?: (taskId: string, request: CallOmoAgentRequest) => Promise<void> | void;
}

export interface CallOmoAgentManager<TCheckpoint = unknown, TOutput = unknown, TProgress = unknown, TInput = CallOmoAgentRequest> {
  execute(
    request: TInput,
    context: CallOmoAgentExecutionContext<TCheckpoint, TProgress>,
  ): Promise<TOutput>;
  normalizeInput?: (request: CallOmoAgentRequest) => Promise<TInput> | TInput;
  canStartBackgroundTask?: (sessionId: string) => Promise<boolean> | boolean;
  getSessionContinuity?: (sessionId: string) => Promise<ContinuityState<TCheckpoint> | undefined> | ContinuityState<TCheckpoint> | undefined;
  saveSessionContinuity?: (sessionId: string, continuity: ContinuityState<TCheckpoint>) => Promise<void> | void;
  hooks?: CallOmoAgentExecutionHooks<TProgress, TOutput>;
}

export interface CallOmoAgentExecutionContext<TCheckpoint = unknown, TProgress = unknown> {
  sessionId: string;
  taskId?: string;
  continuity: ContinuityState<TCheckpoint>;
  signal: AbortSignal;
  setProgress: (progress: TProgress) => void;
  updateContinuity: (continuity: TCheckpoint) => void;
}

export interface CallOmoAgentToolOptions<TCheckpoint = unknown, TOutput = unknown, TProgress = unknown, TInput = CallOmoAgentRequest> {
  manager: CallOmoAgentManager<TCheckpoint, TOutput, TProgress, TInput>;
  backgroundExecutor?: BackgroundExecutor;
  toolName?: string;
}

export type CallOmoAgentSyncResponse<TOutput, TCheckpoint = unknown> =
  {
    kind: "sync";
    task_id?: string;
    output: TOutput;
    mode: "sync";
    session: CallOmoAgentResponseContinuity & { checkpoint: TCheckpoint };
  } & (
    {
      request_id: string;
    } | {
      request_id?: undefined;
    }
  );

export type CallOmoAgentBackgroundResponse = {
  kind: "background";
  task_id: string;
  status: BackgroundTaskStatus;
  mode: "background";
  session: CallOmoAgentResponseContinuity & { checkpoint: unknown };
};

export type CallOmoAgentResponse<TOutput, TCheckpoint = unknown> =
  | CallOmoAgentSyncResponse<TOutput, TCheckpoint>
  | CallOmoAgentBackgroundResponse;

const createContinuityToken = (sessionId: string) => {
  return `${sessionId}.continuity.${Date.now().toString(36)}.${Math.random().toString(16).slice(2, 10)}`;
};

const routeExecution = (request: CallOmoAgentRequest): OmoAgentExecutionMode | "sync" => {
  if (request.routing.mode === "background") {
    return "background";
  }
  if (request.routing.mode === "sync") {
    return "sync";
  }

  if (request.routing.request_background) {
    return "background";
  }

  if (
    request.routing.estimated_duration_ms !== undefined &&
    request.routing.estimated_duration_ms > request.routing.max_sync_wait_ms
  ) {
    return "background";
  }

  if (request.routing.request_id !== undefined || request.routing.task_id !== undefined) {
    return "background";
  }

  return "sync";
};

export class CallOmoAgentTool<
  TCheckpoint = unknown,
  TOutput = unknown,
  TProgress = unknown,
  TInput = CallOmoAgentRequest,
> {
  private readonly continuityStore = new Map<string, ContinuityState<TCheckpoint>>();
  private readonly backgroundExecutor: BackgroundExecutor;
  private readonly toolName: string;

  public constructor(private readonly options: CallOmoAgentToolOptions<TCheckpoint, TOutput, TProgress, TInput>) {
    this.toolName = options.toolName ?? "call-omo-agent";
    const managerAdapter: BackgroundExecutorManager<TProgress, TOutput> = {
      canStartTask: options.manager.canStartBackgroundTask,
      onQueued: (task) => {
        void options.manager.hooks?.onBackgroundQueued?.(
          task.taskId,
          task.input as unknown as CallOmoAgentRequest,
        );
      },
      onStarted: (task) => {
        void options.manager.hooks?.onBackgroundStarted?.(
          task.taskId,
          task.input as unknown as CallOmoAgentRequest,
        );
      },
      onProgress: (task, progress) => {
        void options.manager.hooks?.onBackgroundProgress?.(
          task.taskId,
          task.input as unknown as CallOmoAgentRequest,
          progress as TProgress,
        );
      },
      onCompleted: (task, result) => {
        void options.manager.hooks?.onBackgroundCompleted?.(
          task.taskId,
          task.input as unknown as CallOmoAgentRequest,
          result?.output as TOutput,
        );
      },
      onFailed: (task, failure) => {
        void options.manager.hooks?.onBackgroundFailed?.(
          task.taskId,
          task.input as unknown as CallOmoAgentRequest,
          new Error(failure.message),
        );
      },
      onCanceled: (task) => {
        void options.manager.hooks?.onBackgroundCanceled?.(
          task.taskId,
          task.input as unknown as CallOmoAgentRequest,
        );
      },
      onSessionTerminated: (session) => {
        this.continuityStore.delete(session.sessionId);
      },
    };
    this.backgroundExecutor = options.backgroundExecutor ?? new BackgroundExecutor(managerAdapter);
  }

  public async call(rawRequest: unknown): Promise<CallOmoAgentResponse<TOutput, TCheckpoint>> {
    const request = CallOmoAgentRequestSchema.parse(rawRequest);

    const sessionContinuity = await this.resolveContinuity(request);
    const executionInput = await this.resolveInput(request);
    const requestedMode = routeExecution(request);

    if (requestedMode === "background") {
      return this.executeInBackground(request, executionInput, sessionContinuity);
    }

    return this.executeInSync(request, executionInput, sessionContinuity);
  }

  public getSessionContinuity(sessionId: string): ContinuityState<TCheckpoint> | undefined {
    return this.continuityStore.get(sessionId);
  }

  private async resolveInput(request: CallOmoAgentRequest): Promise<TInput> {
    if (!this.options.manager.normalizeInput) {
      return request as unknown as TInput;
    }

    return await this.options.manager.normalizeInput(request);
  }

  private async resolveContinuity(request: CallOmoAgentRequest): Promise<ContinuityState<TCheckpoint>> {
    const existing = await this.options.manager.getSessionContinuity?.(request.session_id);
    const current =
      existing ??
      this.continuityStore.get(request.session_id) ??
      this.backgroundExecutor.getSessionState<TCheckpoint>(request.session_id)?.continuity ??
      {
        continuityToken:
          request.continuity_token ?? createContinuityToken(request.session_id),
        checkpoint: undefined as TCheckpoint | undefined,
      };

    if (
      request.continuity_token &&
      request.continuity_token !== current.continuityToken
    ) {
      throw new Error(
        `Continuity token mismatch for session '${request.session_id}'.`
      );
    }

    const merged: ContinuityState<TCheckpoint> = {
      continuityToken: current.continuityToken,
      checkpoint:
        (request.continuation_checkpoint as TCheckpoint | undefined) ??
        current.checkpoint,
    };

    await this.saveContinuity(request.session_id, merged);
    return merged;
  }

  private async saveContinuity(
    sessionId: string,
    continuity: ContinuityState<TCheckpoint>,
  ): Promise<void> {
    this.continuityStore.set(sessionId, continuity);
    await this.options.manager.saveSessionContinuity?.(sessionId, continuity);
  }

  private async executeInSync(
    request: CallOmoAgentRequest,
    input: TInput,
    continuity: ContinuityState<TCheckpoint>,
  ): Promise<CallOmoAgentSyncResponse<TOutput, TCheckpoint>> {
    await this.options.manager.hooks?.onBeforeSync?.(request);

    let latestCheckpoint = continuity.checkpoint;
    const output = await this.options.manager.execute(input, {
      sessionId: request.session_id,
      continuity,
      signal: new AbortController().signal,
      setProgress: () => {
        return;
      },
      updateContinuity: (nextCheckpoint) => {
        latestCheckpoint = nextCheckpoint;
      },
    });

    const finalContinuity: ContinuityState<TCheckpoint> = {
      continuityToken: continuity.continuityToken,
      checkpoint: latestCheckpoint,
    };
    await this.saveContinuity(request.session_id, finalContinuity);
    await this.options.manager.hooks?.onAfterSync?.(request, output);

    return {
      kind: "sync",
      mode: "sync",
      task_id: request.routing.task_id,
      output,
      session: {
        session_id: request.session_id,
        continuity_token: finalContinuity.continuityToken,
        continuation_checkpoint: finalContinuity.checkpoint,
        checkpoint: finalContinuity.checkpoint,
      } as CallOmoAgentResponseContinuity & { checkpoint: TCheckpoint },
      ...(request.routing.request_id
        ? { request_id: request.routing.request_id }
        : {}),
    };
  }

  private async executeInBackground(
    request: CallOmoAgentRequest,
    input: TInput,
    continuity: ContinuityState<TCheckpoint>,
  ): Promise<CallOmoAgentBackgroundResponse> {
    const task = this.backgroundExecutor.start({
      sessionId: request.session_id,
      name: `${this.toolName}:${request.agent_id ?? "agent"}`,
      input,
      continuity,
      runner: async (
        runnerInput: TInput,
        context
      ): Promise<TOutput> => {
        let latestCheckpoint = continuity.checkpoint;
        const output = await this.options.manager.execute(runnerInput, {
          sessionId: context.sessionId,
          continuity,
          signal: context.signal,
          setProgress: context.setProgress,
          updateContinuity: (nextCheckpoint) => {
            latestCheckpoint = nextCheckpoint;
          },
        });
        const updated: ContinuityState<TCheckpoint> = {
          continuityToken: continuity.continuityToken,
          checkpoint: latestCheckpoint,
        };
        await this.saveContinuity(context.sessionId, updated);
        return output;
      },
    });

    return {
      kind: "background",
      mode: "background",
      task_id: task.taskId,
      status: task.status(),
      session: {
        session_id: request.session_id,
        continuity_token: continuity.continuityToken,
        continuation_checkpoint: continuity.checkpoint,
      },
    };
  }
}
