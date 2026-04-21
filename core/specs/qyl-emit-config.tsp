// =============================================================================
// QYL Emit Config — central augment-decorator file for the three qyl-native
// TypeSpec emitters (@qyl/typespec-emit-csharp, emit-duckdb, emit-ts-types).
//
// This file decorates the existing hand-authored TypeSpec models so the new
// emitters know:
//   - which C# namespace each model maps to (matches NamespaceRoutingTable.cs
//     exactly, mirroring the current contract-drift outputs)
//   - which models are DuckDB tables and what their SQL names are
//   - which scalars are opaque branded string IDs on the TypeScript side
//
// Commit 5 of the TypeSpec-native-emitters mandate. Decoration-only: the old
// @extension("x-duckdb-table", ...) / @extension("x-csharp-type", ...) calls
// remain in place until Commit 10 deletes the old SchemaGenerator.cs path.
// Both paths emit identical shapes until Commit 6 cuts tspconfig over.
// =============================================================================

import "@qyl/typespec-emit-csharp";
import "@qyl/typespec-emit-duckdb";
import "@qyl/typespec-emit-ts-types";

import "./main.tsp";
import "./models/agent/agent-run.tsp";
import "./models/agent/tool-call.tsp";
import "./models/agent/workflow-checkpoint.tsp";
import "./models/agent/workflow-execution.tsp";
import "./models/alerting.tsp";
import "./models/configurator.tsp";
import "./models/issues.tsp";
import "./models/retention.tsp";
import "./models/search.tsp";
import "./models/triage.tsp";
import "./models/workflow.tsp";
import "./models/workspace.tsp";
import "./intelligence/main.tsp";

using Qyl.Emit.CSharp;
using Qyl.Emit.DuckDb;
using Qyl.Emit.TsTypes;

// -----------------------------------------------------------------------------
// C# namespace routing — one @@csharpNamespace per namespace declared in any
// hand-authored .tsp file. Values mirror NamespaceRoutingTable.cs exactly.
// -----------------------------------------------------------------------------

@@csharpNamespace(Qyl.Api, "Qyl.Api");
@@csharpNamespace(Qyl.Api.Streaming, "Qyl.Api.Streaming");

@@csharpNamespace(Qyl.Common, "Qyl.Common");
@@csharpNamespace(Qyl.Common.Errors, "Qyl.Common.Errors");
@@csharpNamespace(Qyl.Common.Pagination, "Qyl.Common.Pagination");

@@csharpNamespace(Qyl.Intelligence, "Qyl.Intelligence");

@@csharpNamespace(Qyl.Domains, "Qyl.Domains");
@@csharpNamespace(Qyl.Domains.AI.GenAi, "Qyl.Domains.AI.GenAi");
@@csharpNamespace(Qyl.Domains.Agent.Run, "Qyl.Domains.Agent.Run");
@@csharpNamespace(Qyl.Domains.Agent.ToolCall, "Qyl.Domains.Agent.ToolCall");
@@csharpNamespace(Qyl.Domains.Agent.Checkpoint, "Qyl.Domains.Agent.Checkpoint");
@@csharpNamespace(Qyl.Domains.Agent.Workflow, "Qyl.Domains.Agent.Workflow");
@@csharpNamespace(Qyl.Domains.Alerting, "Qyl.Domains.Alerting");
@@csharpNamespace(Qyl.Domains.Configurator, "Qyl.Domains.Configurator");
@@csharpNamespace(Qyl.Domains.Data.Db, "Qyl.Domains.Data.Db");
@@csharpNamespace(Qyl.Domains.Identity, "Qyl.Domains.Identity");
@@csharpNamespace(Qyl.Domains.Issues, "Qyl.Domains.Issues");
@@csharpNamespace(Qyl.Domains.Loom.Triage, "Qyl.Domains.Loom.Triage");
@@csharpNamespace(Qyl.Domains.Observe.Error, "Qyl.Domains.Observe.Error");
@@csharpNamespace(Qyl.Domains.Observe.Log, "Qyl.Domains.Observe.Log");
@@csharpNamespace(Qyl.Domains.Observe.Otel, "Qyl.Domains.Observe.Otel");
@@csharpNamespace(Qyl.Domains.Observe.Session, "Qyl.Domains.Observe.Session");
@@csharpNamespace(Qyl.Domains.Observe.Test, "Qyl.Domains.Observe.Test");
@@csharpNamespace(Qyl.Domains.Ops.Deployment, "Qyl.Domains.Ops.Deployment");
@@csharpNamespace(Qyl.Domains.Ops.Retention, "Qyl.Domains.Ops.Retention");
@@csharpNamespace(Qyl.Domains.Runtime.System, "Qyl.Domains.Runtime.System");
@@csharpNamespace(Qyl.Domains.Search, "Qyl.Domains.Search");
@@csharpNamespace(Qyl.Domains.Transport.Http, "Qyl.Domains.Transport.Http");
@@csharpNamespace(Qyl.Domains.Transport.Messaging, "Qyl.Domains.Transport.Messaging");
@@csharpNamespace(Qyl.Domains.Transport.Rpc, "Qyl.Domains.Transport.Rpc");
@@csharpNamespace(Qyl.Domains.Workflow, "Qyl.Domains.Workflow");
@@csharpNamespace(Qyl.Domains.Workspace, "Qyl.Domains.Workspace");

@@csharpNamespace(Qyl.OTel, "Qyl.OTel");
@@csharpNamespace(Qyl.OTel.Enums, "Qyl.OTel.Enums");
@@csharpNamespace(Qyl.OTel.Logs, "Qyl.OTel.Logs");
@@csharpNamespace(Qyl.OTel.Metrics, "Qyl.OTel.Metrics");
@@csharpNamespace(Qyl.OTel.Profiles, "Qyl.OTel.Profiles");
@@csharpNamespace(Qyl.OTel.Resource, "Qyl.OTel.Resource");
@@csharpNamespace(Qyl.OTel.Traces, "Qyl.OTel.Traces");

@@csharpNamespace(Qyl.Storage, "Qyl.Storage");

// -----------------------------------------------------------------------------
// TypeScript brands for opaque string IDs. Mandate §Commit-4 exact list plus
// any scalar in core/specs/common/types.tsp with x-csharp-struct extension.
// -----------------------------------------------------------------------------

@@tsBrand(Qyl.Common.TraceId);
@@tsBrand(Qyl.Common.SpanId);
@@tsBrand(Qyl.Common.SessionId);
@@tsBrand(Qyl.Common.UserId);
