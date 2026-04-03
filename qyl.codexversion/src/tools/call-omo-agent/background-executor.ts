export type BackgroundTaskId = `background-task-${string}`;

export type BackgroundTaskStatus = "queued" | "running" | "completed" | "failed" | "cancelled";

export interface BackgroundTaskResult<TOutput> {
  output: TOutput;
  finishedAt: number;
}

export interface BackgroundTaskFailure {
  name: string;
  message: string;
  stack?: string;
}

export interface ContinuityState<TCheckpoint> {
  checkpoint?: TCheckpoint;
  continuityToken: string;
}

export interface SessionState<TCheckpoint = unknown> {
  sessionId: string;
  activeTaskId?: BackgroundTaskId;
  lastTaskId?: BackgroundTaskId;
  status: BackgroundTaskStatus;
  continuity: ContinuityState<TCheckpoint>;
  lastUpdatedAt: number;
}

export interface BackgroundTaskSnapshot<TInput = unknown, TProgress = unknown, TOutput = unknown> {
  taskId: BackgroundTaskId;
  sessionId: string;
  input: TInput;
  status: BackgroundTaskStatus;
  hooks?: BackgroundTaskHooks<TProgress, TOutput>;
  name: string;
  createdAt: number;
  updatedAt: number;
  lastProgress?: TProgress;
  result?: BackgroundTaskResult<TOutput>;
  failure?: BackgroundTaskFailure;
}

export interface BackgroundTaskContext<TProgress, TCheckpoint> {
  taskId: BackgroundTaskId;
  sessionId: string;
  signal: AbortSignal;
  setProgress: (progress: TProgress) => void;
  updateContinuation: (checkpoint: TCheckpoint) => void;
}

export type BackgroundTaskRunner<TInput, TProgress, TOutput, TCheckpoint> = (
  input: TInput,
  context: BackgroundTaskContext<TProgress, TCheckpoint>
) => Promise<TOutput>;

export interface BackgroundTaskHooks<TProgress = unknown, TOutput = unknown> {
  onQueued?: (task: BackgroundTaskSnapshot<unknown, TProgress, TOutput>) => void | Promise<void>;
  onStarted?: (task: BackgroundTaskSnapshot<unknown, TProgress, TOutput>) => void | Promise<void>;
  onProgress?: (
    task: BackgroundTaskSnapshot<unknown, TProgress, TOutput>,
    progress: TProgress
  ) => void | Promise<void>;
  onCompleted?: (
    task: BackgroundTaskSnapshot<unknown, TProgress, TOutput>,
    result: BackgroundTaskResult<TOutput>
  ) => void | Promise<void>;
  onFailed?: (
    task: BackgroundTaskSnapshot<unknown, TProgress, TOutput>,
    failure: BackgroundTaskFailure
  ) => void | Promise<void>;
  onCanceled?: (task: BackgroundTaskSnapshot<unknown, TProgress, TOutput>) => void | Promise<void>;
}

export interface BackgroundExecutorManager<TProgress = unknown, TOutput = unknown>
  extends BackgroundTaskHooks<TProgress, TOutput> {
  canStartTask?: (sessionId: string) => boolean | Promise<boolean>;
  onSessionTerminated?: (session: SessionState) => void | Promise<void>;
}

export interface StartBackgroundCallOptions<TInput, TProgress, TOutput, TCheckpoint> {
  sessionId: string;
  name: string;
  input: TInput;
  continuity?: ContinuityState<TCheckpoint>;
  hooks?: BackgroundTaskHooks<TProgress, TOutput>;
  runner: BackgroundTaskRunner<TInput, TProgress, TOutput, TCheckpoint>;
}

export interface BackgroundTaskHandle<TOutput> {
  taskId: BackgroundTaskId;
  result: Promise<TOutput>;
  status: () => BackgroundTaskStatus;
}

export class BackgroundExecutor {
  private readonly tasks = new Map<BackgroundTaskId, BackgroundTaskSnapshot<unknown, unknown, unknown>>();
  private readonly aborters = new Map<BackgroundTaskId, AbortController>();
  private readonly cancellations = new Set<BackgroundTaskId>();
  private readonly sessions = new Map<string, SessionState>();
  private taskCounter = 0;

  constructor(private readonly manager?: BackgroundExecutorManager) {}

  public getTaskStatus(taskId: BackgroundTaskId): BackgroundTaskStatus | undefined {
    const task = this.tasks.get(taskId);
    return task?.status;
  }

  public getSessionState<TCheckpoint>(sessionId: string): SessionState<TCheckpoint> | undefined {
    const state = this.sessions.get(sessionId);
    if (!state) return undefined;

    return {
      ...state,
      continuity: {
        ...state.continuity,
        checkpoint: state.continuity.checkpoint as TCheckpoint | undefined,
      },
    };
  }

  public isCanceled(taskId: BackgroundTaskId): boolean {
    return this.cancellations.has(taskId);
  }

  public start<TInput, TProgress, TOutput, TCheckpoint>(
    options: StartBackgroundCallOptions<TInput, TProgress, TOutput, TCheckpoint>
  ): BackgroundTaskHandle<TOutput> {
    const canStartTask = this.manager?.canStartTask;
    const canStart = canStartTask
      ? canStartTask(options.sessionId)
      : Promise.resolve(true);

    if (typeof canStart === "boolean" && canStart === false) {
      throw new Error(
        `Session ${options.sessionId} is currently disallowed from starting background tasks.`
      );
    }

    const taskId = this.createTaskId();
    const now = Date.now();
    const abortController = new AbortController();

    const canStartState = Promise.resolve(canStart)
      .then((allowed) => Boolean(allowed))
      .catch((error) => {
        throw new Error(
          `CanStartTask check for session ${options.sessionId} failed: ${
            error instanceof Error ? error.message : String(error)
          }`
        );
      });

    const previousSession = this.getOrCreateSession<TCheckpoint>(options.sessionId, options.continuity);
    const snapshot: BackgroundTaskSnapshot<TInput, TProgress, TOutput> = {
      taskId,
      sessionId: options.sessionId,
      input: options.input,
      status: "queued",
      hooks: options.hooks,
      name: options.name,
      createdAt: now,
      updatedAt: now,
    };

    if (canStart instanceof Promise) {
      canStartState
        .then((allowed) => {
          if (!allowed) {
            this.cancel(taskId);
          }
        })
        .catch(() => {
          this.cancel(taskId);
        });
    }

    this.tasks.set(taskId, snapshot as BackgroundTaskSnapshot<unknown, unknown, unknown>);
    this.aborters.set(taskId, abortController);
    this.updateSessionState({
      sessionId: options.sessionId,
      activeTaskId: taskId,
      status: "queued",
      continuity: options.continuity ?? previousSession.continuity,
    });
    void this.dispatch("onQueued", snapshot, { taskId, progress: undefined, result: undefined });

    const result = (async () => {
      const allowed = await canStartState;
      if (!allowed) {
        return await this.throwIfCanceled(taskId);
      }

      const started = await this.markRunning(taskId, snapshot);
      if (!started) return await this.throwIfCanceled(taskId);

      const sessionState = this.sessions.get(options.sessionId);
      const token =
        sessionState?.continuity?.continuityToken ??
        this.createContinuityToken(options.sessionId, options.name);

      try {
        const output = await options.runner(options.input, {
          taskId,
          sessionId: options.sessionId,
          signal: abortController.signal,
          setProgress: (progress) => {
            const current = this.tasks.get(taskId);
            if (!current || current.status !== "running") return;
            current.lastProgress = progress;
            current.updatedAt = Date.now();
            void this.dispatch("onProgress", current, { progress });
          },
          updateContinuation: (checkpoint) => {
            this.updateSessionState({
              sessionId: options.sessionId,
              continuity: {
                continuityToken: token,
                checkpoint,
              },
            });
          },
        });

        const finishedAt = Date.now();
        const finishedSnapshot = this.tasks.get(taskId) as BackgroundTaskSnapshot<unknown, TProgress, TOutput>;
        if (!finishedSnapshot) return await this.throwIfCanceled(taskId);

        const result: BackgroundTaskResult<TOutput> = { output, finishedAt };
        finishedSnapshot.status = "completed";
        finishedSnapshot.updatedAt = finishedAt;
        finishedSnapshot.result = result as BackgroundTaskResult<unknown>;
        this.tasks.set(taskId, finishedSnapshot as BackgroundTaskSnapshot<unknown, unknown, unknown>);
        this.updateSessionState({
          sessionId: options.sessionId,
          activeTaskId: undefined,
          lastTaskId: taskId,
          status: "completed",
        });
        this.cancellations.delete(taskId);
        await this.dispatch("onCompleted", finishedSnapshot, {
          result,
          taskId: undefined,
          progress: undefined,
        });
        return output;
      } catch (error) {
        const failedSnapshot = this.tasks.get(taskId) as BackgroundTaskSnapshot<unknown, TProgress, TOutput>;
        const failure = this.toFailure(error);

        if (this.cancellations.has(taskId) || (failedSnapshot?.status === "cancelled")) {
          if (failedSnapshot) {
            failedSnapshot.status = "cancelled";
            failedSnapshot.updatedAt = Date.now();
          }
          this.updateSessionState({
            sessionId: options.sessionId,
            activeTaskId: undefined,
            lastTaskId: taskId,
            status: "cancelled",
          });
          await this.dispatch("onCanceled", failedSnapshot ?? snapshot as any, {
            taskId: undefined,
            progress: undefined,
          });
          throw error instanceof DOMException && error.name === "AbortError"
            ? new Error(`Task ${taskId} was cancelled.`)
            : error;
        }

        if (failedSnapshot) {
          failedSnapshot.status = "failed";
          failedSnapshot.updatedAt = Date.now();
          failedSnapshot.failure = failure;
        }
        this.updateSessionState({
          sessionId: options.sessionId,
          activeTaskId: undefined,
          lastTaskId: taskId,
          status: "failed",
        });
        this.cancellations.delete(taskId);
        await this.dispatch("onFailed", failedSnapshot ?? snapshot as any, {
          failure,
          taskId: undefined,
          progress: undefined,
        });
        throw error instanceof Error ? error : new Error(String(error));
      } finally {
        this.aborters.delete(taskId);
      }
    })();

    return {
      taskId,
      result,
      status: () => this.getTaskStatus(taskId) ?? "queued",
    };
  }

  public cancel(taskId: BackgroundTaskId): void {
    const task = this.tasks.get(taskId);
    if (!task || task.status === "completed" || task.status === "failed" || task.status === "cancelled") {
      return;
    }

    this.cancellations.add(taskId);
    task.status = "cancelled";
    task.updatedAt = Date.now();
    this.tasks.set(taskId, task as BackgroundTaskSnapshot<unknown, unknown, unknown>);
    this.updateSessionState({
      sessionId: task.sessionId,
      activeTaskId: task.status === "cancelled" ? undefined : undefined,
      lastTaskId: taskId,
      status: "cancelled",
    });
    const aborter = this.aborters.get(taskId);
    aborter?.abort();
    void this.dispatch("onCanceled", task as BackgroundTaskSnapshot<unknown, unknown, unknown>, {
      taskId,
      progress: undefined,
    });
  }

  private async markRunning(
    taskId: BackgroundTaskId,
    snapshot: BackgroundTaskSnapshot<unknown, unknown, unknown>
  ): Promise<boolean> {
    const current = this.tasks.get(taskId);
    if (!current || this.cancellations.has(taskId)) {
      return false;
    }

    current.status = "running";
    current.updatedAt = Date.now();
    await this.dispatch("onStarted", current, {});
    return true;
  }

  private async throwIfCanceled(taskId: BackgroundTaskId): Promise<never> {
    if (this.cancellations.has(taskId)) {
      throw new Error(`Task ${taskId} was cancelled.`);
    }

    throw new Error(`Task ${taskId} could not be started.`);
  }

  private async dispatch(
    event: keyof BackgroundTaskHooks,
    task: BackgroundTaskSnapshot<unknown, unknown, unknown>,
    extras?: { progress?: unknown; result?: unknown; failure?: unknown; taskId?: BackgroundTaskId }
  ): Promise<void> {
    const managerHandler = this.manager?.[event] as ((...args: unknown[]) => void | Promise<void>) | undefined;
    if (managerHandler) {
      if (event === "onStarted" || event === "onQueued") {
        await managerHandler(task);
      } else if (event === "onProgress") {
        await managerHandler(task, extras?.progress);
      } else if (event === "onCompleted") {
        await managerHandler(task, extras?.result);
      } else if (event === "onFailed") {
        await managerHandler(task, extras?.failure);
      } else if (event === "onCanceled") {
        await managerHandler(task);
      }
    }

    const taskHooks = (task as any).hooks as BackgroundTaskHooks | undefined;
    const taskHandler = taskHooks?.[event];
    if (!taskHandler) return;

    if (event === "onStarted" || event === "onQueued") {
      await taskHandler(task);
    } else if (event === "onProgress") {
      await taskHandler(task, extras?.progress);
    } else if (event === "onCompleted") {
      await taskHandler(task, extras?.result);
    } else if (event === "onFailed") {
      await taskHandler(task, extras?.failure);
    } else if (event === "onCanceled") {
      await taskHandler(task);
    }
  }

  private getOrCreateSession<TCheckpoint>(
    sessionId: string,
    continuity?: ContinuityState<TCheckpoint>
  ): SessionState<TCheckpoint> {
    const existing = this.sessions.get(sessionId);
    if (existing) return existing as SessionState<TCheckpoint>;

    const created: SessionState<TCheckpoint> = {
      sessionId,
      status: "queued",
      lastUpdatedAt: Date.now(),
      continuity: continuity
        ? continuity
        : {
            continuityToken: this.createContinuityToken(sessionId, "initial"),
            checkpoint: undefined,
          },
    };

    this.sessions.set(sessionId, created as SessionState);
    return created;
  }

  private updateSessionState(changes: {
    sessionId: string;
    activeTaskId?: BackgroundTaskId;
    lastTaskId?: BackgroundTaskId;
    status?: BackgroundTaskStatus;
    continuity?: ContinuityState<unknown>;
  }) {
    const current = this.getOrCreateSession(changes.sessionId);
    const next: SessionState = {
      ...current,
      ...changes,
      lastUpdatedAt: Date.now(),
    };

    if (!next.continuity) {
      next.continuity = { ...current.continuity };
    }

    if (next.status === "cancelled" && this.sessions.has(changes.sessionId)) {
      void this.manager?.onSessionTerminated?.(next);
    }

    this.sessions.set(changes.sessionId, next);
  }

  private createTaskId(): BackgroundTaskId {
    this.taskCounter += 1;
    return `background-task-${Date.now().toString(36)}-${this.taskCounter.toString(16)}`;
  }

  private createContinuityToken(sessionId: string, action: string) {
    return `${sessionId}:${action}:${Date.now().toString(36)}:${Math.random().toString(16).slice(2, 10)}`;
  }

  private toFailure(error: unknown): BackgroundTaskFailure {
    if (error instanceof Error) {
      return {
        name: error.name,
        message: error.message,
        stack: error.stack,
      };
    }

    return {
      name: "Error",
      message: String(error),
    };
  }
}
