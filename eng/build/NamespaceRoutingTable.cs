// =============================================================================
// Namespace Routing Table - Single source of truth for schema → namespace → file
// =============================================================================
// Consolidates GetCSharpNamespace() and GetFileNameFromNamespace() into one
// data structure. Add new routes here; both mappings stay in sync automatically.
// =============================================================================

using System;
using System.Collections.Frozen;
using System.Linq;

namespace Domain.CodeGen;

/// <summary>
///     Single routing table that maps TypeSpec schema prefixes to C# namespaces and file names.
///     Replaces the manually-synchronized <c>GetCSharpNamespace</c> / <c>GetFileNameFromNamespace</c> pair.
/// </summary>
public static class NamespaceRoutingTable
{
    /// <summary>
    ///     All routes, ordered by prefix length descending so the longest (most specific) match wins.
    ///     To add a new mapping: add one entry here. Both namespace and file name resolution update automatically.
    /// </summary>
    static readonly RouteEntry[] s_routes =
    [
        // ── Common sub-namespaces (most specific first) ──────────────────────
        new("Qyl.Common.Errors.", "Qyl.Common.Errors", "Errors"),
        new("Qyl.Common.Pagination.", "Qyl.Common.Pagination", "Pagination"),
        new("Qyl.Common.", "Qyl.Common", "Common"),

        // ── OTel ─────────────────────────────────────────────────────────────
        new("Qyl.OTel.Enums.", "Qyl.OTel.Enums", "OTelEnums"),
        new("Qyl.OTel.Traces.", "Qyl.OTel.Traces", "OTelTraces"),
        new("Qyl.OTel.Logs.", "Qyl.OTel.Logs", "OTelLogs"),
        new("Qyl.OTel.Metrics.", "Qyl.OTel.Metrics", "OTelMetrics"),
        new("Qyl.OTel.Resource.", "Qyl.OTel.Resource", "OTelResource"),
        new("Qyl.OTel.", "Qyl.OTel", "OTel"),

        // ── Domains → AI ────────────────────────────────────────────────────
        new("Qyl.Domains.AI.Code.", "Qyl.Domains.AI.Code", "DomainsAICode"),
        new("Qyl.Domains.AI.", "Qyl.Domains.AI", "DomainsAI"),

        // ── Domains → Identity ──────────────────────────────────────────────
        new("Qyl.Domains.Identity.", "Qyl.Domains.Identity", "DomainsIdentity"),

        // ── Domains → Observe ───────────────────────────────────────────────
        new("Qyl.Domains.Observe.Error.", "Qyl.Domains.Observe.Error", "DomainsObserveError"),
        new("Qyl.Domains.Observe.Exceptions.", "Qyl.Domains.Observe.Exceptions", "DomainsObserveExceptions"),
        new("Qyl.Domains.Observe.Log.", "Qyl.Domains.Observe.Log", "DomainsObserveLog"),
        new("Qyl.Domains.Observe.Session.", "Qyl.Domains.Observe.Session", "DomainsObserveSession"),
        new("Qyl.Domains.Observe.", "Qyl.Domains.Observe", "DomainsObserve"),

        // ── Domains → Ops ───────────────────────────────────────────────────
        new("Qyl.Domains.Ops.Cicd.", "Qyl.Domains.Ops.Cicd", "DomainsOpsCicd"),
        new("Qyl.Domains.Ops.Deployment.", "Qyl.Domains.Ops.Deployment", "DomainsOpsDeployment"),
        new("Qyl.Domains.Ops.", "Qyl.Domains.Ops", "DomainsOps"),

        // ── Domains → Others ────────────────────────────────────────────────
        new("Qyl.Domains.Transport.", "Qyl.Domains.Transport", "DomainsTransport"),
        new("Qyl.Domains.Security.", "Qyl.Domains.Security", "DomainsSecurity"),
        new("Qyl.Domains.Infra.", "Qyl.Domains.Infra", "DomainsInfra"),
        new("Qyl.Domains.Runtime.", "Qyl.Domains.Runtime", "DomainsRuntime"),
        new("Qyl.Domains.Data.", "Qyl.Domains.Data", "DomainsData"),
        new("Qyl.Domains.", "Qyl.Domains", "Domains"),

        // ── API ─────────────────────────────────────────────────────────────
        new("Qyl.Api.", "Qyl.Api", "Api"),

        // ── Legacy prefixes (backward compatibility) ────────────────────────
        new("Primitives.", "Qyl.Common", "Common"),
        new("Enums.", "Qyl.Enums", "Enums"),
        new("Models.", "Qyl.Models", "Models"),
        new("Api.", "Qyl.Api", "Api"),
        new("Streaming.", "Qyl.Streaming", "Streaming")
    ];

    /// <summary>
    ///     Routes sorted by descending prefix length so longest-prefix-match wins.
    ///     Materialized once at startup.
    /// </summary>
    static readonly RouteEntry[] s_sortedRoutes = [.. s_routes.OrderByDescending(static r => r.Prefix.Length)];

    /// <summary>
    ///     Reverse lookup: namespace → file name. Frozen for O(1) lookups.
    /// </summary>
    static readonly FrozenDictionary<string, string> s_namespaceToFileName = s_routes
        .DistinctBy(static r => r.Namespace)
        .ToFrozenDictionary(static r => r.Namespace, static r => r.FileName);

    /// <summary>
    ///     Maps a TypeSpec schema name to a C# namespace.
    ///     Uses longest-prefix matching (e.g. <c>Qyl.OTel.Traces.Foo</c> → <c>Qyl.OTel.Traces</c>).
    /// </summary>
    public static string GetCSharpNamespace(string schemaName)
    {
        foreach (var route in s_sortedRoutes)
            if (schemaName.StartsWith(route.Prefix, StringComparison.Ordinal))
                return route.Namespace;

        return "Qyl.Models"; // default fallback
    }

    /// <summary>
    ///     Maps a C# namespace to an output file name (without extension).
    /// </summary>
    public static string GetFileNameFromNamespace(string ns) =>
        s_namespaceToFileName.TryGetValue(ns, out var fileName) ? fileName : ns.Replace(".", "");

    /// <summary>One row in the routing table.</summary>
    public readonly record struct RouteEntry(string Prefix, string Namespace, string FileName);
}