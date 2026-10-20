/**
 * The tools that change something: annotations, saved queries and the one
 * delete tool, `delete_record`, plus `run_saved_query`, the saved-query read
 * that belongs with them.
 *
 * What they change is limited to objects users create in qyl, inside the
 * verified caller's Collector project: a note on a trace, span, session or
 * failure, and a named filter. No tool here edits or deletes a trace, log or
 * metric; retention alone removes telemetry. Reading and writing stay separate
 * tools, so a host can approve them separately: `annotate` and `save_query`
 * each create a record without an `id` and change the one an `id` names with
 * its current etag, and deleting is `delete_record` alone.
 *
 * Identical creates are deduplicated for 24 hours; a new explicit retry key
 * requests another object. A change or a delete replaces or removes the
 * version an etag names, so the write tools are destructive and idempotent:
 * repeating one changes nothing more. `annotate` and `save_query` need
 * `qyl:write`, `delete_record` needs `qyl:delete`, and `run_saved_query` keeps
 * `qyl:read` (tool-scopes.ts).
 */

import type {
  AnnotateInput,
  AnnotationOutput,
  DeleteInput,
  DeleteOutput,
  SaveQueryInput,
  SavedQueryOutput,
} from "./contracts/generated/api.js";
import type {
  CallToolResult,
  McpServer,
  ServerContext,
  ToolAnnotations,
} from "@modelcontextprotocol/server";
import { contractSchema } from "./contracts/tool-schemas.js";
import {
  annotate,
  annotationReadBack,
  deleteResource,
  describeTarget,
  saveQuery,
} from "./collector-resources.js";
import { QYL_MCP_DELETE_SCOPE, QYL_MCP_SCOPE, QYL_MCP_WRITE_SCOPE } from "./oauth.js";
import { runTool } from "./request-scope.js";
import { queryAnalysis } from "./analysis-client.js";
import { telemetryToolResult } from "./telemetry-redaction.js";
import { toolScopeChallenge } from "./tool-scopes.js";
import { READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS, TELEMETRY_TOOL_AUTH_META } from "./tools.js";

/**
 * The one set of annotations every mutating tool carries: a create replays identical
 * retries for 24 hours, a change replaces and a delete removes the version an etag
 * names, so repeating any of them changes nothing more.
 */
export const MUTATING_TOOL_ANNOTATIONS = {
  readOnlyHint: false,
  destructiveHint: true,
  idempotentHint: true,
  openWorldHint: false,
} as const satisfies ToolAnnotations;

export const WRITE_SCOPES = [QYL_MCP_SCOPE, QYL_MCP_WRITE_SCOPE] as const;
const DELETE_SCOPES = [QYL_MCP_SCOPE, QYL_MCP_DELETE_SCOPE] as const;

/** OpenAI's per-tool `securitySchemes`, in the `_meta` form the read tools use. */
export function securityMeta(scopes: readonly string[]): { securitySchemes: { type: "oauth2"; scopes: string[] }[] } {
  return { securitySchemes: [{ type: "oauth2", scopes: [...scopes] }] };
}

/** Registers the annotation and saved-query tools; every handler runs inside runTool. */
export function registerWriteTools(server: McpServer): void {
  server.registerTool(
    "annotate",
    {
      title: "Annotate",
      description: "Create a labeled note on a trace, span, session or failure when recording a finding, or change a note with its id and current etag; get_trace, list_sessions or get_failure_context returns it.",
      inputSchema: contractSchema("Mcp.Tools.AnnotateInput"),
      annotations: MUTATING_TOOL_ANNOTATIONS,
      scopeChallenge: toolScopeChallenge("annotate", WRITE_SCOPES),
      _meta: securityMeta(WRITE_SCOPES),
    },
    (args: AnnotateInput, ctx: ServerContext): Promise<CallToolResult> =>
      runTool(ctx, "annotate", 1, async (scope) => {
        const { annotation, created } = await annotate(args, scope.collector);
        await scope.step(`${created ? "Created" : "Updated"} annotation ${annotation.id}`);
        const output: AnnotationOutput = { annotation };
        return telemetryToolResult(
          `annotate: ${created ? "created" : "updated"} annotation ${annotation.id} on ${describeTarget(annotation.target)}, etag ${annotation.etag}. → ${annotationReadBack(annotation.target)} shows it`,
          output,
        );
      }),
  );

  server.registerTool(
    "save_query",
    {
      title: "Save Query",
      description: "Store a named telemetry filter when saving a reusable question, or change a saved query with its id and current etag; run_saved_query answers it.",
      inputSchema: contractSchema("Mcp.Tools.SaveQueryInput"),
      annotations: MUTATING_TOOL_ANNOTATIONS,
      scopeChallenge: toolScopeChallenge("save_query", WRITE_SCOPES),
      _meta: securityMeta(WRITE_SCOPES),
    },
    (args: SaveQueryInput, ctx: ServerContext): Promise<CallToolResult> =>
      runTool(ctx, "save_query", 1, async (scope) => {
        const { savedQuery, created } = await saveQuery(args, scope.collector);
        await scope.step(`${created ? "Created" : "Updated"} saved query ${savedQuery.id}`);
        const output: SavedQueryOutput = { saved_query: savedQuery };
        return telemetryToolResult(
          `save_query: ${created ? "created" : "updated"} ${savedQuery.name} (${savedQuery.filter.signal}), id ${savedQuery.id}, etag ${savedQuery.etag}. → run_saved_query id=${savedQuery.id}`,
          output,
        );
      }),
  );

  server.registerTool(
    "run_saved_query",
    {
      title: "Run Saved Query",
      description: "Evaluate a saved filter against current telemetry when answering a previously saved question, or without an id list the saved queries with their ids and etags.",
      inputSchema: contractSchema("Mcp.Tools.RunSavedQueryInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "run_saved_query", 1, async (scope) =>
      queryAnalysis("run_saved_query", args, "Mcp.Tools.RunSavedQueryOutput", scope)),
  );

  server.registerTool(
    "delete_record",
    {
      title: "Delete Annotation or Saved Query",
      description: "Delete an annotation or saved query with its current etag when removing a stored note or question, never traces, logs or metrics.",
      inputSchema: contractSchema("Mcp.Tools.DeleteInput"),
      annotations: MUTATING_TOOL_ANNOTATIONS,
      scopeChallenge: toolScopeChallenge("delete_record", DELETE_SCOPES),
      _meta: securityMeta(DELETE_SCOPES),
    },
    (args: DeleteInput, ctx: ServerContext): Promise<CallToolResult> =>
      runTool(ctx, "delete_record", 1, async (scope) => {
        await deleteResource(args.kind, args.id, args.etag, scope.collector);
        await scope.step(`Deleted ${args.kind} ${args.id}`);
        const output: DeleteOutput = { deleted: true };
        return telemetryToolResult(
          `delete_record: deleted ${args.kind === "annotation" ? "annotation" : "saved query"} ${args.id}.${args.kind === "annotation" ? "" : " → run_saved_query lists the rest"}`,
          output,
        );
      }),
  );
}
