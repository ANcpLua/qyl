import { z } from "zod";

import {
  ConcurrencyError,
  ConcurrencyManager,
  type ConcurrencyManagerConfig,
  type ConcurrencyStateSnapshot,
  type PermitCounter,
} from "./concurrency";
import {
  type BoundedAutonomyConfig,
  boundedAutonomySchema,
  type SessionLineage,
  type SpawnBlockReason,
  type SubagentSpawnState,
  createRootLineage,
  emptySpawnState,
  canSpawnSubagent,
  releaseSubagentSpawn,
} from "./subagent-spawn-limits";

const concurrencyDefaultsSchema = z.object({
  defaultMaxConcurrent: z.number().int().positive(),
  defaultMaxQueued: z.number().int().nonnegative(),
  defaultsByProvider: z.record(
    z.string().min(1),
    z.object({
      maxConcurrent: z.number().int().positive(),
      maxQueued: z.number().int().nonnegative(),
    }),
  ).optional(),
  defaultsByModel: z.record(
    z.string().min(1),
    z.object({
      maxConcurrent: z.number().int().positive(),
      maxQueued: z.number().int().nonnegative(),
    }),
  ).optional(),
  defaultsByProviderModel: z.record(
    z.custom<`${string}/${string}`>((value) => {
      if (typeof value !== "string") {
        return false;
      }

      const [provider = "", model = ""] = value.split("/");
      return provider.length > 0 && model.length > 0;
    }, "Provider/model key must be in provider/model format"),
    z.object({
      maxConcurrent: z.number().int().positive(),
      maxQueued: z.number().int().nonnegative(),
    }),
  ).optional(),
});

export const backgroundAgentManagerConfigSchema = z.object({
  spawnLimits: boundedAutonomySchema,
  concurrency: concurrencyDefaultsSchema,
});

export type BackgroundAgentManagerConfig = z.infer<typeof backgroundAgentManagerConfigSchema>;

const spawnRequestSchema = z.object({
  requestId: z.string().trim().min(1).optional(),
  taskId: z.string().trim().min(1).optional(),
  parentSessionId: z.string().trim().min(1),
  parentLineage: z.array(z.string().trim().min(1)).min(1),
  childSessionId: z.string().trim().min(1),
  provider: z.string().trim().min(1),
  model: z.string().trim().min(1),
  payload: z.unknown().optional(),
  timeoutMs: z.number().int().nonnegative().optional(),
});

export type SpawnRequest = z.infer<typeof spawnRequestSchema> & {
  signal?: AbortSignal;
};

export type BackgroundTaskStatus =
  | "running"
  | "completed"
  | "failed"
  | "cancelled";

export type BackgroundTaskRecord = {
  taskId: string;
  requestId: string;
  childSessionId: string;
  provider: string;
  model: string;
  parentSessionId: string;
  lineage: SessionLineage;
  status: BackgroundTaskStatus;
  startedAt: Date;
  completedAt?: Date;
  payload?: unknown;
};

export type BackgroundSessionRecord = {
  sessionId: string;
  lineage: SessionLineage;
  parentSessionId?: string;
  activeTaskIds: ReadonlySet<string>;
  createdAt: Date;
  lastActivityAt: Date;
  status: "active" | "closing" | "closed";
};

type RuntimeBackgroundTaskRecord = {
  taskId: string;
  requestId: string;
  childSessionId: string;
  provider: string;
  model: string;
  parentSessionId: string;
  lineage: SessionLineage;
  status: BackgroundTaskStatus;
  startedAt: Date;
  payload?: unknown;
  releasePermit: () => void;
};

type RuntimeBackgroundSessionRecord = {
  sessionId: string;
  lineage: SessionLineage;
  parentSessionId?: string;
  activeTaskIds: Set<string>;
  createdAt: Date;
  lastActivityAt: Date;
  status: "active" | "closing" | "closed";
};

export type SpawnRequestDeniedReason =
  | SpawnBlockReason
  | "concurrency_queue_full"
  | "concurrency_cancelled"
  | "lineage_mismatch"
  | "invalid_parent_lineage"
  | "parent_session_unknown"
  | "invalid_request";

export type SpawnRequestRejected = {
  ok: false;
  reason: SpawnRequestDeniedReason;
  reasonDetail: string;
};

export type SpawnRequestAccepted = {
  ok: true;
  taskId: string;
  requestId: string;
  childSessionId: string;
  provider: string;
  model: string;
  lineage: SessionLineage;
};

export type SpawnRequestOutcome = SpawnRequestAccepted | SpawnRequestRejected;

export const backgroundSessionFromRuntime = (session: RuntimeBackgroundSessionRecord): BackgroundSessionRecord => ({
  sessionId: session.sessionId,
  lineage: session.lineage,
  parentSessionId: session.parentSessionId,
  activeTaskIds: new Set(session.activeTaskIds),
  createdAt: session.createdAt,
  lastActivityAt: session.lastActivityAt,
  status: session.status,
});

export const backgroundTaskFromRuntime = (task: RuntimeBackgroundTaskRecord): BackgroundTaskRecord => ({
  taskId: task.taskId,
  requestId: task.requestId,
  childSessionId: task.childSessionId,
  provider: task.provider,
  model: task.model,
  parentSessionId: task.parentSessionId,
  lineage: task.lineage,
  status: task.status,
  startedAt: task.startedAt,
});

export class BackgroundAgentManager {
  private readonly concurrencyManager: ConcurrencyManager;
  private readonly boundedAutonomyConfig: BoundedAutonomyConfig;

  private spawnState: SubagentSpawnState = emptySpawnState;
  private readonly activeSessions = new Map<string, RuntimeBackgroundSessionRecord>();
  private readonly activeTasks = new Map<string, RuntimeBackgroundTaskRecord>();

  private taskSequence = 0;

  constructor(config: BackgroundAgentManagerConfig) {
    const validated = backgroundAgentManagerConfigSchema.parse(config);
    this.boundedAutonomyConfig = validated.spawnLimits;
    this.concurrencyManager = new ConcurrencyManager(validated.concurrency as ConcurrencyManagerConfig);
  }

  public openSession(sessionId: string, lineage: SessionLineage = createRootLineage(sessionId)): void {
    const normalizedLineage = this.normalizeLineage(lineage);
    if (normalizedLineage.length === 0 || normalizedLineage[0] !== sessionId) {
      throw new Error("Invalid lineage for root session registration.");
    }

    const existing = this.activeSessions.get(sessionId);
    if (existing && this.lineagesMatch(existing.lineage, normalizedLineage)) {
      existing.status = "active";
      existing.lastActivityAt = new Date();
      return;
    }

    const now = new Date();
    this.activeSessions.set(sessionId, {
      sessionId,
      lineage: normalizedLineage,
      parentSessionId: normalizedLineage.length > 1 ? normalizedLineage[normalizedLineage.length - 2] : undefined,
      activeTaskIds: new Set(),
      createdAt: now,
      lastActivityAt: now,
      status: "active",
    });
  }

  public closeSession(sessionId: string): boolean {
    const session = this.activeSessions.get(sessionId);
    if (!session) {
      return false;
    }

    if (session.activeTaskIds.size > 0) {
      session.status = "closing";
      session.lastActivityAt = new Date();
      return false;
    }

    this.activeSessions.delete(sessionId);
    return true;
  }

  public async requestSpawn(rawRequest: SpawnRequest): Promise<SpawnRequestOutcome> {
    const parsed = spawnRequestSchema.safeParse(rawRequest);
    if (!parsed.success) {
      return {
        ok: false,
        reason: "invalid_request",
        reasonDetail: parsed.error.issues.map((issue) => issue.message).join("; "),
      };
    }

    const request: SpawnRequest = {
      ...parsed.data,
      signal: rawRequest.signal,
    };

    const requestId = request.requestId ?? `spawn-request-${++this.taskSequence}`;
    const taskId = request.taskId ?? `background-task-${this.taskSequence}`;

    if (this.activeTasks.has(taskId)) {
      return {
        ok: false,
        reason: "invalid_request",
        reasonDetail: `Task '${taskId}' is already active.`,
      };
    }

    const parentLineage = this.normalizeLineage(request.parentLineage);
    const parentSession = this.activeSessions.get(request.parentSessionId);

    if (parentSession && !this.lineagesMatch(parentSession.lineage, parentLineage)) {
      return {
        ok: false,
        reason: "lineage_mismatch",
        reasonDetail: `Parent session '${request.parentSessionId}' has lineage ${JSON.stringify(Array.from(parentSession.lineage))}, not ${JSON.stringify(parentLineage)}.`,
      };
    }

    if (!parentSession) {
      if (parentLineage[0] !== request.parentSessionId) {
        return {
          ok: false,
          reason: "parent_session_unknown",
          reasonDetail: `Parent session '${request.parentSessionId}' is not currently registered.`,
        };
      }

      this.openSession(request.parentSessionId, parentLineage);
    }

    const existingChild = this.activeSessions.get(request.childSessionId);
    if (existingChild && existingChild.status !== "closed") {
      return {
        ok: false,
        reason: "lineage_mismatch",
        reasonDetail: `Cannot create child session '${request.childSessionId}' because an active session with that ID already exists.`,
      };
    }

    const spawnDecision = canSpawnSubagent(
      this.boundedAutonomyConfig,
      this.spawnState,
      request.parentSessionId,
      parentLineage,
      request.childSessionId,
    );

    if (!spawnDecision.ok) {
      return {
        ok: false,
        reason: spawnDecision.reason,
        reasonDetail: spawnDecision.reasonDetail,
      };
    }

    try {
      const permit = await this.concurrencyManager.acquire(request.provider, request.model, {
        signal: request.signal,
        timeoutMs: request.timeoutMs,
      });

      const startedAt = new Date();
      const childLineage = spawnDecision.nextLineage;

      const task: RuntimeBackgroundTaskRecord = {
        taskId,
        requestId,
        childSessionId: request.childSessionId,
        provider: request.provider,
        model: request.model,
        parentSessionId: request.parentSessionId,
      lineage: childLineage,
      status: "running",
      startedAt,
      payload: request.payload,
      releasePermit: permit.release,
      };

      this.spawnState = spawnDecision.nextState;
      this.activeTasks.set(taskId, task);

      const childSession = this.activeSessions.get(request.childSessionId) ?? {
        sessionId: request.childSessionId,
        lineage: childLineage,
        parentSessionId: request.parentSessionId,
        activeTaskIds: new Set<string>(),
        createdAt: startedAt,
        lastActivityAt: startedAt,
        status: "active" as const,
      };

      childSession.activeTaskIds.add(taskId);
      childSession.lastActivityAt = startedAt;
      childSession.status = "active";
      this.activeSessions.set(request.childSessionId, childSession);

      this.touchSession(request.parentSessionId);
      this.touchTask(taskId);

      return {
        ok: true,
        taskId,
        requestId,
        childSessionId: request.childSessionId,
        provider: request.provider,
        model: request.model,
        lineage: [...childLineage],
      };
    } catch (error) {
      if (error instanceof ConcurrencyError) {
        if (error.code === "cancelled") {
          return {
            ok: false,
            reason: "concurrency_cancelled",
            reasonDetail: error.message,
          };
        }

        return {
          ok: false, 
          reason: "concurrency_queue_full",
          reasonDetail: error.message,
        };
      }

      throw error;
    }
  }

  public completeTask(taskId: string): boolean {
    return this.finalizeTask(taskId, "completed");
  }

  public failTask(taskId: string): boolean {
    return this.finalizeTask(taskId, "failed");
  }

  public cancelTask(taskId: string): boolean {
    return this.finalizeTask(taskId, "cancelled");
  }

  public getActiveSessionSnapshot(): BackgroundSessionRecord[] {
    return Array.from(this.activeSessions.values()).map(backgroundSessionFromRuntime);
  }

  public getActiveTaskSnapshot(): BackgroundTaskRecord[] {
    return Array.from(this.activeTasks.values()).map(backgroundTaskFromRuntime);
  }

  public getConcurrencyStateSnapshot(): ConcurrencyStateSnapshot {
    return this.concurrencyManager.getSnapshot();
  }

  public getPermitCounter(): PermitCounter {
    return this.concurrencyManager.getPermitCounter();
  }

  public clearCompletedState(): void {
    for (const [taskId, task] of this.activeTasks.entries()) {
      if (task.status !== "running") {
        this.completeTask(taskId);
      }
    }
  }

  private normalizeLineage(lineage: SessionLineage): SessionLineage {
    return lineage.map((id) => id.trim()).filter((id) => id.length > 0);
  }

  private lineagesMatch(left: SessionLineage, right: SessionLineage): boolean {
    if (left.length !== right.length) {
      return false;
    }

    for (let i = 0; i < left.length; i++) {
      if (left[i] !== right[i]) {
        return false;
      }
    }

    return true;
  }

  private touchTask(taskId: string): void {
    const task = this.activeTasks.get(taskId);
    if (!task) {
      return;
    }

    const session = this.activeSessions.get(task.childSessionId);
    if (!session) {
      return;
    }

    session.lastActivityAt = new Date();
  }

  private touchSession(sessionId: string): void {
    const session = this.activeSessions.get(sessionId);
    if (!session) {
      return;
    }

    session.lastActivityAt = new Date();
  }

  private finalizeTask(taskId: string, status: "completed" | "failed" | "cancelled"): boolean {
    const task = this.activeTasks.get(taskId);
    if (!task) {
      return false;
    }

    const session = this.activeSessions.get(task.childSessionId);
    if (session) {
      session.activeTaskIds.delete(taskId);
      session.lastActivityAt = new Date();
      if (session.status === "closing" && session.activeTaskIds.size === 0) {
        this.activeSessions.delete(session.sessionId);
      }
    }

    task.status = status;
    task.releasePermit();
    this.spawnState = releaseSubagentSpawn(this.spawnState, task.lineage);
    this.activeTasks.delete(taskId);
    return true;
  }
}
