// =============================================================================
// QYL v2.0 - Configurator & Code Generation Entities
// =============================================================================
// Generation profiles, selections, jobs, schema promotions.
// =============================================================================

import "@typespec/openapi";
import "../common/types.tsp";

using TypeSpec.OpenAPI;
using Qyl.Common;

namespace Qyl.Domains.Configurator;

// =============================================================================
// Generation Profile
// =============================================================================

@doc("Named instrumentation profile for code generation")
@extension("x-duckdb-table", "generation_profiles")
@extension("x-csharp-type", "GenerationProfileEntity")
model GenerationProfileEntity {
  @doc("Profile ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Profile name")
  name: string;

  @doc("Profile description")
  description?: string;

  @doc("Target framework (e.g. net10.0)")
  @encodedName("application/json", "target_framework")
  targetFramework: string;

  @doc("Target language")
  @encodedName("application/json", "target_language")
  targetLanguage: string;

  @doc("Semantic conventions version")
  @encodedName("application/json", "semconv_version")
  semconvVersion: string;

  @doc("Enabled features/modules")
  @encodedName("application/json", "features_json")
  @extension("x-duckdb-type", "JSON")
  featuresJson: string;

  @doc("Template customizations")
  @encodedName("application/json", "template_overrides_json")
  @extension("x-duckdb-type", "JSON")
  templateOverridesJson?: string;

  @doc("Whether this is the default profile")
  @encodedName("application/json", "is_default")
  isDefault: boolean;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

// =============================================================================
// Generation Selection
// =============================================================================

@doc("Selected semconv/feature per workspace for code generation")
@extension("x-duckdb-table", "generation_selections")
@extension("x-csharp-type", "GenerationSelectionEntity")
model GenerationSelectionEntity {
  @doc("Selection ID")
  @key
  id: string;

  @doc("Workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Profile")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("Selection type (semconv_group, feature, custom_attribute)")
  @encodedName("application/json", "selection_type")
  selectionType: string;

  @doc("Selection key (e.g. http, db, genai)")
  @encodedName("application/json", "selection_key")
  selectionKey: string;

  @doc("Whether enabled")
  enabled: boolean;

  @doc("Selection-specific configuration")
  @encodedName("application/json", "config_json")
  @extension("x-duckdb-type", "JSON")
  configJson?: string;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

// =============================================================================
// Generation Job
// =============================================================================

@doc("Code generation job entry")
@extension("x-duckdb-table", "generation_jobs")
@extension("x-csharp-type", "GenerationJobEntity")
model GenerationJobEntity {
  @doc("Job ID")
  @key
  id: string;

  @doc("Workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Profile")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("Job type")
  @encodedName("application/json", "job_type")
  jobType: GenerationJobType;

  @doc("Job status")
  status: JobStatus;

  @doc("Priority (higher = more urgent)")
  priority: int32;

  @doc("Hash of inputs for dedup")
  @encodedName("application/json", "input_hash")
  inputHash?: string;

  @doc("Local path where output was written")
  @encodedName("application/json", "output_path")
  outputPath?: string;

  @doc("Hash of generated output")
  @encodedName("application/json", "output_hash")
  outputHash?: string;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Queue timestamp")
  @encodedName("application/json", "queued_at")
  queuedAt: utcDateTime;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;
}

@doc("Generation job types")
enum GenerationJobType {
  @doc("Full regeneration")
  full: "full",

  @doc("Incremental update")
  incremental: "incremental",

  @doc("Preview/dry-run")
  preview: "preview",
}

@doc("Job lifecycle status")
enum JobStatus {
  @doc("Queued for execution")
  queued: "queued",

  @doc("Currently executing")
  running: "running",

  @doc("Successfully completed")
  completed: "completed",

  @doc("Failed with error")
  failed: "failed",

  @doc("Cancelled by user")
  cancelled: "cancelled",
}

// =============================================================================
// Generation Job Event
// =============================================================================

@doc("Job lifecycle event")
@extension("x-duckdb-table", "generation_job_events")
@extension("x-csharp-type", "GenerationJobEventEntity")
model GenerationJobEventEntity {
  @doc("Event ID")
  @key
  id: string;

  @doc("Parent job")
  @encodedName("application/json", "job_id")
  jobId: string;

  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: string;

  @doc("Human-readable message")
  message?: string;

  @doc("Event-specific data")
  @encodedName("application/json", "details_json")
  @extension("x-duckdb-type", "JSON")
  detailsJson?: string;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Schema Promotion
// =============================================================================

@doc("Attribute-to-column promotion from profiles")
@extension("x-duckdb-table", "schema_promotions")
@extension("x-csharp-type", "SchemaPromotionEntity")
model SchemaPromotionEntity {
  @doc("Promotion ID")
  @key
  id: string;

  @doc("Source profile")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("JSON path in attributes_json")
  @encodedName("application/json", "source_attribute")
  sourceAttribute: string;

  @doc("Promoted column name")
  @encodedName("application/json", "target_column")
  targetColumn: string;

  @doc("DuckDB type for the column")
  @encodedName("application/json", "target_type")
  targetType: string;

  @doc("Target table (spans, logs, etc.)")
  @encodedName("application/json", "target_table")
  targetTable: string;

  @doc("Index type (btree, hash, or null)")
  @encodedName("application/json", "index_type")
  indexType?: string;

  @doc("Promotion status")
  status: PromotionStatus;

  @doc("Application timestamp")
  @encodedName("application/json", "applied_at")
  appliedAt?: utcDateTime;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

@doc("Schema promotion status")
enum PromotionStatus {
  @doc("Pending application")
  pending: "pending",

  @doc("Successfully applied")
  applied: "applied",

  @doc("Rolled back")
  rolledBack: "rolled_back",
}

// =============================================================================
// Schema Change Job
// =============================================================================

@doc("Schema adaptation DDL job")
@extension("x-duckdb-table", "schema_change_jobs")
@extension("x-csharp-type", "SchemaChangeJobEntity")
model SchemaChangeJobEntity {
  @doc("Job ID")
  @key
  id: string;

  @doc("Link to promotion if applicable")
  @encodedName("application/json", "promotion_id")
  promotionId?: string;

  @doc("Change type")
  @encodedName("application/json", "change_type")
  changeType: SchemaChangeType;

  @doc("Target table")
  @encodedName("application/json", "target_table")
  targetTable: string;

  @doc("DDL statement to execute")
  @encodedName("application/json", "ddl_statement")
  ddlStatement: string;

  @doc("Rollback DDL")
  @encodedName("application/json", "rollback_ddl")
  rollbackDdl?: string;

  @doc("Job status")
  status: JobStatus;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Queue timestamp")
  @encodedName("application/json", "queued_at")
  queuedAt: utcDateTime;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;
}

@doc("Schema change types")
enum SchemaChangeType {
  @doc("Add a column")
  addColumn: "add_column",

  @doc("Drop a column")
  dropColumn: "drop_column",

  @doc("Add an index")
  addIndex: "add_index",

  @doc("Drop an index")
  dropIndex: "drop_index",

  @doc("Alter column type")
  alterType: "alter_type",
}

// =============================================================================
// Schema Change Event
// =============================================================================

@doc("DDL execution audit event")
@extension("x-duckdb-table", "schema_change_events")
@extension("x-csharp-type", "SchemaChangeEventEntity")
model SchemaChangeEventEntity {
  @doc("Event ID")
  @key
  id: string;

  @doc("Parent job")
  @encodedName("application/json", "job_id")
  jobId: string;

  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: string;

  @doc("Human-readable message")
  message?: string;

  @doc("Event-specific data")
  @encodedName("application/json", "details_json")
  @extension("x-duckdb-type", "JSON")
  detailsJson?: string;

  @doc("Rows affected by DDL")
  @encodedName("application/json", "rows_affected")
  rowsAffected?: int64;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}
