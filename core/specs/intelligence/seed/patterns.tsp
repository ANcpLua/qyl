// =============================================================================
// QYL v2.0 - Seed Diagnostic Patterns (v1)
// =============================================================================
// 10 initial diagnostic patterns. Source: specs/telemetry-intelligence.md §5.1
// These compile to static registries in DiagnosticPatterns.g.cs.
//
// Signal attributes reference:
//   - OTel semconv promoted columns (status_code, duration_ns, etc.)
//   - qyl promoted columns (gen_ai_provider_name, gen_ai_stop_reason, etc.)
//   - Computed fields (occurrence_rate, span_count_under_parent, etc.)
// =============================================================================

namespace Qyl.Intelligence.Seed;

// ---------------------------------------------------------------------------
// Pattern: genai_rate_limit
// Category: genai
// Signals: status_code=2, gen_ai_provider_name exists, error_type contains rate_limit
// Hypothesis: Provider throttling. Check quota, reduce concurrency, add backoff.
// Confidence: 0.9
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: genai_token_exhaustion
// Category: genai
// Signals: gen_ai_stop_reason=length
// Hypothesis: Context window exceeded. Reduce prompt size or switch to larger model.
// Confidence: 0.85
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: genai_content_filter
// Category: genai
// Signals: gen_ai_stop_reason contains content_filter
// Hypothesis: Content policy violation. Review prompt content.
// Confidence: 0.95
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: db_timeout
// Category: data
// Signals: exception_type=TimeoutException, db.system.name exists, duration_ns > 2000000000
// Hypothesis: Database query timeout. Check query plan, connection pool, lock contention.
// Confidence: 0.85
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: db_n_plus_one
// Category: data
// Signals: db.system.name exists, parent_span_id exists, span_count_under_parent > 10
// Hypothesis: N+1 query pattern. Batch or prefetch related data.
// Confidence: 0.80
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: http_5xx_cluster
// Category: error
// Signals: http.response.status_code gte 500, occurrence_rate > baseline * 3
// Hypothesis: Server error spike. Check recent deployments and upstream dependencies.
// Confidence: 0.75
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: deployment_regression
// Category: error
// Signals: error_type exists, first_seen_at > last_deployment_time
// Hypothesis: New error class after deployment. Compare with previous version.
// Confidence: 0.80
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: cascading_timeout
// Category: latency
// Signals: exception_type contains Timeout, downstream_service_error=true
// Hypothesis: Upstream failure causing downstream timeouts. Investigate root service first.
// Confidence: 0.70
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: memory_pressure_latency
// Category: latency
// Signals: process.runtime.dotnet.gc.duration gt 100, avg_latency > p99_baseline
// Hypothesis: GC pressure causing latency. Check memory allocation patterns.
// Confidence: 0.65
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: cost_spike
// Category: cost
// Signals: gen_ai_cost_usd > daily_average * 3
// Hypothesis: Abnormal cost increase. Identify the model, service, and session responsible.
// Confidence: 0.75
// ---------------------------------------------------------------------------
