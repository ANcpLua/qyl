using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using qyl.mcp.Sentry;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for Sentry identity and navigation.
///     Covers: whoami, organizations, teams, projects, releases.
/// </summary>
[McpServerToolType]
public sealed class qylOrgTools(HttpClient client, IOptions<qylAuthOptions> options)
{
    // ── Identity ──────────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_whoami")]
    [Description("""
                 Get the authenticated Sentry user identity.

                 Returns the user associated with the configured SENTRY_AUTH_TOKEN.
                 Use this to verify authentication is working before running other Sentry tools.

                 Returns: Username, email, and organization memberships
                 """)]
    public Task<string> WhoAmIAsync() =>
        qylHelper.ExecuteAsync(async () =>
        {
            var auth = await client.GetFromJsonAsync<SentryAuthDto>(
                "auth/", SentryOrgJsonContext.Default.SentryAuthDto).ConfigureAwait(false);

            if (auth is null) return "No auth info returned from Sentry.";

            var sb = new StringBuilder();
            sb.AppendLine("# Sentry Identity");
            sb.AppendLine();
            sb.AppendLine($"- **User:** {auth.User?.Username ?? "unknown"}");
            sb.AppendLine($"- **Email:** {auth.User?.Email ?? "unknown"}");
            if (!string.IsNullOrEmpty(auth.User?.Name))
                sb.AppendLine($"- **Name:** {auth.User.Name}");
            if (auth.Scopes is { Count: > 0 })
                sb.AppendLine($"- **Token scopes:** {string.Join(", ", auth.Scopes)}");
            return sb.ToString();
        });

    // ── Organizations ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_list_organizations")]
    [Description("""
                 List all Sentry organizations accessible with the current token.

                 Returns slug, name, and member count for each org.
                 The slug is required for all other Sentry tools.

                 If SENTRY_ORG is set, only that organization is shown.

                 Returns: Organization list with slugs
                 """)]
    public Task<string> ListOrganizationsAsync() =>
        qylHelper.ExecuteAsync(async () =>
        {
            var orgs = await client.GetFromJsonAsync<SentryOrganizationDto[]>(
                "organizations/", SentryOrgJsonContext.Default.SentryOrganizationDtoArray).ConfigureAwait(false);

            if (orgs is null || orgs.Length is 0)
                return "No organizations found. Verify SENTRY_AUTH_TOKEN has org:read scope.";

            // Filter to configured org if set
            var orgSlug = options.Value.OrgSlug;
            if (!string.IsNullOrEmpty(orgSlug))
                orgs = [.. orgs.Where(o => o.Slug == orgSlug)];

            var sb = new StringBuilder();
            sb.AppendLine($"# Sentry Organizations ({orgs.Length})");
            sb.AppendLine();
            sb.AppendLine("| Slug | Name | Members |");
            sb.AppendLine("|------|------|---------|");
            foreach (var org in orgs)
                sb.AppendLine($"| {org.Slug} | {org.Name} | {org.MemberCount} |");
            return sb.ToString();
        });

    // ── Projects ──────────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_list_projects")]
    [Description("""
                 List projects in a Sentry organization.

                 Projects are required for querying issues and events.
                 Use sentry_list_organizations first to find the org slug.

                 Returns: Project slugs, platforms, and creation dates
                 """)]
    public Task<string> ListProjectsAsync(
        [Description("Organization slug (from sentry_list_organizations)")]
        string orgSlug) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var projects = await client.GetFromJsonAsync<SentryProjectDto[]>(
                $"organizations/{Uri.EscapeDataString(orgSlug)}/projects/",
                SentryOrgJsonContext.Default.SentryProjectDtoArray).ConfigureAwait(false);

            if (projects is null || projects.Length is 0)
                return $"No projects found in org '{orgSlug}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Projects in {orgSlug} ({projects.Length})");
            sb.AppendLine();
            sb.AppendLine("| Slug | Name | Platform | Status |");
            sb.AppendLine("|------|------|----------|--------|");
            foreach (var p in projects)
                sb.AppendLine($"| {p.Slug} | {p.Name} | {p.Platform ?? "—"} | {p.Status ?? "—"} |");
            return sb.ToString();
        });

    // ── Teams ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_list_teams")]
    [Description("""
                 List teams in a Sentry organization.

                 Returns: Team slugs, names, and member counts
                 """)]
    public Task<string> ListTeamsAsync(
        [Description("Organization slug")] string orgSlug) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var teams = await client.GetFromJsonAsync<SentryTeamDto[]>(
                $"organizations/{Uri.EscapeDataString(orgSlug)}/teams/",
                SentryOrgJsonContext.Default.SentryTeamDtoArray).ConfigureAwait(false);

            if (teams is null || teams.Length is 0)
                return $"No teams found in org '{orgSlug}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Teams in {orgSlug} ({teams.Length})");
            sb.AppendLine();
            sb.AppendLine("| Slug | Name | Members |");
            sb.AppendLine("|------|------|---------|");
            foreach (var t in teams)
                sb.AppendLine($"| {t.Slug} | {t.Name} | {t.MemberCount} |");
            return sb.ToString();
        });

    // ── Releases ──────────────────────────────────────────────────────────────

    [McpServerTool(Name = "qyl.sentry_list_releases")]
    [Description("""
                 List recent releases for a Sentry project.

                 Shows version, deployment date, and new issue counts.
                 Useful for correlating issues with deployments.

                 Returns: Release versions with dates and issue stats
                 """)]
    public Task<string> ListReleasesAsync(
        [Description("Organization slug")] string orgSlug,
        [Description("Project slug (optional — returns org-wide releases if omitted)")]
        string? projectSlug = null,
        [Description("Maximum releases to return (default: 20)")]
        int limit = 20) =>
        qylHelper.ExecuteAsync(async () =>
        {
            var url = $"organizations/{Uri.EscapeDataString(orgSlug)}/releases/?limit={limit}";
            if (!string.IsNullOrEmpty(projectSlug))
                url += $"&project={Uri.EscapeDataString(projectSlug)}";

            var releases = await client.GetFromJsonAsync<SentryReleaseDto[]>(
                url, SentryOrgJsonContext.Default.SentryReleaseDtoArray).ConfigureAwait(false);

            if (releases is null || releases.Length is 0)
                return "No releases found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Releases ({releases.Length})");
            sb.AppendLine();
            sb.AppendLine("| Version | Date | New Issues | Deploys |");
            sb.AppendLine("|---------|------|------------|---------|");
            foreach (var r in releases)
                sb.AppendLine($"| {r.Version} | {r.DateReleased?.ToString("yyyy-MM-dd") ?? r.DateCreated?.ToString("yyyy-MM-dd") ?? "—"} | {r.NewGroups} | {r.DeployCount} |");
            return sb.ToString();
        });
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

internal sealed record SentryAuthDto(
    [property: JsonPropertyName("user")] SentryUserDto? User,
    [property: JsonPropertyName("scopes")] List<string>? Scopes);

internal sealed record SentryUserDto(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record SentryOrganizationDto(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("memberCount")] int MemberCount);

internal sealed record SentryProjectDto(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("platform")] string? Platform,
    [property: JsonPropertyName("status")] string? Status);

internal sealed record SentryTeamDto(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("memberCount")] int MemberCount);

internal sealed record SentryReleaseDto(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("dateCreated")] DateTimeOffset? DateCreated,
    [property: JsonPropertyName("dateReleased")] DateTimeOffset? DateReleased,
    [property: JsonPropertyName("newGroups")] int NewGroups,
    [property: JsonPropertyName("deployCount")] int DeployCount);

// ─────────────────────────────────────────────────────────────────────────────
// JSON context (AOT)
// ─────────────────────────────────────────────────────────────────────────────

[JsonSerializable(typeof(SentryAuthDto))]
[JsonSerializable(typeof(SentryOrganizationDto[]))]
[JsonSerializable(typeof(SentryProjectDto[]))]
[JsonSerializable(typeof(SentryTeamDto[]))]
[JsonSerializable(typeof(SentryReleaseDto[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class SentryOrgJsonContext : JsonSerializerContext;
