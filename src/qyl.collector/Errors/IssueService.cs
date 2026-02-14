namespace qyl.collector.Errors;

/// <summary>
///     Service layer for the error issue engine. Operates against the
///     <c>error_issues</c>, <c>error_issue_events</c>, and <c>error_breadcrumbs</c>
///     DuckDB tables using pooled read connections for queries and inline writes.
/// </summary>
public sealed partial class IssueService(DuckDbStore store, ILogger<IssueService> logger)
{
    // ==========================================================================
    // Valid Statuses and Transitions
    // ==========================================================================

    private static readonly FrozenSet<string> ValidStatuses = FrozenSet.ToFrozenSet(
        ["unresolved", "acknowledged", "investigating", "in_progress", "resolved", "ignored", "regressed"]);

    private static readonly FrozenDictionary<string, string[]> AllowedTransitions =
        new Dictionary<string, string[]>
        {
            ["unresolved"] = ["acknowledged", "investigating", "resolved", "ignored"],
            ["acknowledged"] = ["investigating", "in_progress", "resolved", "ignored"],
            ["investigating"] = ["in_progress", "resolved", "ignored"],
            ["in_progress"] = ["resolved", "ignored"],
            ["resolved"] = ["regressed"],
            ["ignored"] = ["unresolved"],
            ["regressed"] = ["acknowledged", "investigating", "resolved", "ignored"]
        }.ToFrozenDictionary();

    // ==========================================================================
    // Issue CRUD
    // ==========================================================================

    /// <summary>
    ///     Creates or updates an error issue using fingerprint-based grouping.
    ///     If an issue with the same fingerprint exists for the project,
    ///     increments its occurrence count and updates last_seen_at.
    /// </summary>
    /// <returns>The issue ID (existing or newly created).</returns>
    public async Task<string> UpsertIssueAsync(
        string projectId,
        string fingerprint,
        string title,
        string errorType,
        string category = "unknown",
        string level = "error",
        string? culprit = null,
        string? platform = null,
        CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        // Read on read connection
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var checkCmd = lease.Connection.CreateCommand();
        checkCmd.CommandText = """
            SELECT id FROM error_issues
            WHERE project_id = $1 AND fingerprint = $2
            LIMIT 1
            """;
        checkCmd.Parameters.Add(new DuckDBParameter { Value = projectId });
        checkCmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
        var existingId = await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;

        if (existingId is not null)
        {
            // Write through write channel
            await store.ExecuteWriteAsync(async (con, token) =>
            {
                await using var updateCmd = con.CreateCommand();
                updateCmd.CommandText = """
                    UPDATE error_issues SET
                        occurrence_count = occurrence_count + 1,
                        last_seen_at = $1,
                        updated_at = $1
                    WHERE id = $2
                    """;
                updateCmd.Parameters.Add(new DuckDBParameter { Value = now });
                updateCmd.Parameters.Add(new DuckDBParameter { Value = existingId });
                await updateCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            LogIssueUpserted(existingId, fingerprint);
            return existingId;
        }

        var issueId = Guid.NewGuid().ToString("N");
        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var insertCmd = con.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO error_issues
                    (id, project_id, fingerprint, title, culprit, error_type, category,
                     level, platform, first_seen_at, last_seen_at, occurrence_count,
                     status, priority, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, 1, 'unresolved', 'medium', $12, $13)
                """;
            insertCmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = projectId });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = title });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = culprit ?? (object)DBNull.Value });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = errorType });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = category });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = level });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = platform ?? (object)DBNull.Value });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = now });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = now });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = now });
            insertCmd.Parameters.Add(new DuckDBParameter { Value = now });
            await insertCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        LogIssueUpserted(issueId, fingerprint);
        return issueId;
    }

    /// <summary>
    ///     Gets a single error issue by ID.
    /// </summary>
    public async Task<ErrorIssueRow?> GetIssueByIdAsync(string issueId, CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = IssueSelectSql + " WHERE id = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapIssue(reader) : null;
    }

    /// <summary>
    ///     Lists issues with optional filtering by project, status, priority, and level.
    /// </summary>
    public async Task<IReadOnlyList<ErrorIssueRow>> ListIssuesAsync(
        string? projectId = null,
        string? status = null,
        string? priority = null,
        string? level = null,
        string? assignedTo = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);

        var conditions = new List<string>();
        var parameters = new List<DuckDBParameter>();
        var paramIndex = 1;

        if (!string.IsNullOrEmpty(projectId))
        {
            conditions.Add($"project_id = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = projectId });
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add($"status = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = status });
        }

        if (!string.IsNullOrEmpty(priority))
        {
            conditions.Add($"priority = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = priority });
        }

        if (!string.IsNullOrEmpty(level))
        {
            conditions.Add($"level = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = level });
        }

        if (!string.IsNullOrEmpty(assignedTo))
        {
            conditions.Add($"assigned_to = ${paramIndex++}");
            parameters.Add(new DuckDBParameter { Value = assignedTo });
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        var clampedLimit = Math.Clamp(limit, 1, 1000);
        var clampedOffset = Math.Max(offset, 0);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            {IssueSelectSql}
            {whereClause}
            ORDER BY last_seen_at DESC
            LIMIT ${paramIndex++} OFFSET ${paramIndex}
            """;

        cmd.Parameters.AddRange(parameters);
        cmd.Parameters.Add(new DuckDBParameter { Value = clampedLimit });
        cmd.Parameters.Add(new DuckDBParameter { Value = clampedOffset });

        var results = new List<ErrorIssueRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapIssue(reader));

        return results;
    }

    // ==========================================================================
    // Issue Lifecycle
    // ==========================================================================

    /// <summary>
    ///     Transitions an issue status, enforcing valid lifecycle transitions.
    /// </summary>
    /// <returns><c>true</c> if the transition was applied; <c>false</c> if the issue was not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transition is invalid.</exception>
    public async Task<bool> TransitionStatusAsync(
        string issueId,
        string newStatus,
        string? reason = null,
        CancellationToken ct = default)
    {
        newStatus = newStatus.Trim().ToLowerInvariant();
        if (!ValidStatuses.Contains(newStatus))
            throw new ArgumentException($"Invalid status: '{newStatus}'", nameof(newStatus));

        var existing = await GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (existing is null)
            return false;

        if (!AllowedTransitions.TryGetValue(existing.Status, out var allowed) ||
            !allowed.AsSpan().Contains(newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition from '{existing.Status}' to '{newStatus}'.");
        }

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();

            cmd.CommandText = newStatus == "resolved"
                ? "UPDATE error_issues SET status = $1, resolved_at = $2, updated_at = $3 WHERE id = $4"
                : "UPDATE error_issues SET status = $1, updated_at = $2 WHERE id = $3";

            cmd.Parameters.Add(new DuckDBParameter { Value = newStatus });
            if (newStatus == "resolved")
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = now });
                cmd.Parameters.Add(new DuckDBParameter { Value = now });
                cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            }
            else
            {
                cmd.Parameters.Add(new DuckDBParameter { Value = now });
                cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            }

            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        LogStatusTransition(issueId, existing.Status, newStatus, reason);
        return true;
    }

    /// <summary>
    ///     Assigns an owner to an issue.
    /// </summary>
    public async Task<bool> AssignOwnerAsync(string issueId, string owner, CancellationToken ct = default)
    {
        var existing = await GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (existing is null)
            return false;

        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE error_issues SET assigned_to = $1, updated_at = $2 WHERE id = $3";
            cmd.Parameters.Add(new DuckDBParameter { Value = owner });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        LogOwnerAssigned(issueId, owner);
        return true;
    }

    /// <summary>
    ///     Updates priority for an issue.
    /// </summary>
    public async Task<bool> SetPriorityAsync(string issueId, string priority, CancellationToken ct = default)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        return await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE error_issues SET priority = $1, updated_at = $2 WHERE id = $3";
            cmd.Parameters.Add(new DuckDBParameter { Value = priority });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
        }, ct).ConfigureAwait(false);
    }

    // ==========================================================================
    // Issue Events
    // ==========================================================================

    /// <summary>
    ///     Links an error event occurrence to an issue.
    /// </summary>
    /// <returns>The newly created event ID.</returns>
    public async Task<string> LinkEventAsync(
        string issueId,
        string? traceId = null,
        string? spanId = null,
        string? message = null,
        string? stackTrace = null,
        string? environment = null,
        string? releaseVersion = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        var eventId = Guid.NewGuid().ToString("N");
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        await store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO error_issue_events
                    (id, issue_id, trace_id, span_id, message, stack_trace,
                     environment, release_version, user_id, timestamp)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = eventId });
            cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
            cmd.Parameters.Add(new DuckDBParameter { Value = traceId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = spanId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = message ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = stackTrace ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = environment ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = releaseVersion ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = userId ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return eventId;
    }

    /// <summary>
    ///     Gets error events linked to an issue, ordered by timestamp descending.
    /// </summary>
    public async Task<IReadOnlyList<ErrorIssueEventRow>> GetEventsAsync(
        string issueId,
        int limit = 100,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, issue_id, trace_id, span_id, message, stack_trace,
                   stack_frames_json, environment, release_version,
                   user_id, user_ip, request_url, request_method,
                   browser, os, device, runtime, runtime_version,
                   context_json, tags_json, timestamp
            FROM error_issue_events
            WHERE issue_id = $1
            ORDER BY timestamp DESC
            LIMIT $2
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = issueId });
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 1000) });

        var results = new List<ErrorIssueEventRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapIssueEvent(reader));

        return results;
    }

    // ==========================================================================
    // Breadcrumbs
    // ==========================================================================

    /// <summary>
    ///     Gets breadcrumbs for an error event, ordered by timestamp ascending (oldest first).
    /// </summary>
    public async Task<IReadOnlyList<ErrorBreadcrumbRow>> GetBreadcrumbsAsync(
        string eventId,
        int limit = 200,
        CancellationToken ct = default)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, event_id, breadcrumb_type, category, message,
                   level, data_json, timestamp
            FROM error_breadcrumbs
            WHERE event_id = $1
            ORDER BY timestamp ASC
            LIMIT $2
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = eventId });
        cmd.Parameters.Add(new DuckDBParameter { Value = Math.Clamp(limit, 1, 1000) });

        var results = new List<ErrorBreadcrumbRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ErrorBreadcrumbRow
            {
                Id = reader.GetString(0),
                EventId = reader.GetString(1),
                BreadcrumbType = reader.GetString(2),
                Category = reader.Col(3).AsString,
                Message = reader.Col(4).AsString,
                Level = reader.GetString(5),
                DataJson = reader.Col(6).AsString,
                Timestamp = reader.GetDateTime(7)
            });
        }

        return results;
    }

    // ==========================================================================
    // Private Methods - SQL & Mapping
    // ==========================================================================

    private const string IssueSelectSql = """
        SELECT id, project_id, fingerprint, title, culprit, error_type, category,
               level, platform, first_seen_at, last_seen_at, occurrence_count,
               affected_users_count, status, substatus, priority, assigned_to,
               resolved_at, resolved_by, regression_count, last_release,
               tags_json, metadata_json, created_at, updated_at
        FROM error_issues
        """;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ErrorIssueRow MapIssue(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            ProjectId = reader.GetString(1),
            Fingerprint = reader.GetString(2),
            Title = reader.GetString(3),
            Culprit = reader.Col(4).AsString,
            ErrorType = reader.GetString(5),
            Category = reader.GetString(6),
            Level = reader.GetString(7),
            Platform = reader.Col(8).AsString,
            FirstSeenAt = reader.GetDateTime(9),
            LastSeenAt = reader.GetDateTime(10),
            OccurrenceCount = reader.GetInt64(11),
            AffectedUsersCount = reader.GetInt32(12),
            Status = reader.GetString(13),
            Substatus = reader.Col(14).AsString,
            Priority = reader.GetString(15),
            AssignedTo = reader.Col(16).AsString,
            ResolvedAt = reader.Col(17).AsDateTime,
            ResolvedBy = reader.Col(18).AsString,
            RegressionCount = reader.GetInt32(19),
            LastRelease = reader.Col(20).AsString,
            TagsJson = reader.Col(21).AsString,
            MetadataJson = reader.Col(22).AsString,
            CreatedAt = reader.GetDateTime(23),
            UpdatedAt = reader.GetDateTime(24)
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ErrorIssueEventRow MapIssueEvent(IDataReader reader) =>
        new()
        {
            Id = reader.GetString(0),
            IssueId = reader.GetString(1),
            TraceId = reader.Col(2).AsString,
            SpanId = reader.Col(3).AsString,
            Message = reader.Col(4).AsString,
            StackTrace = reader.Col(5).AsString,
            StackFramesJson = reader.Col(6).AsString,
            Environment = reader.Col(7).AsString,
            ReleaseVersion = reader.Col(8).AsString,
            UserId = reader.Col(9).AsString,
            UserIp = reader.Col(10).AsString,
            RequestUrl = reader.Col(11).AsString,
            RequestMethod = reader.Col(12).AsString,
            Browser = reader.Col(13).AsString,
            Os = reader.Col(14).AsString,
            Device = reader.Col(15).AsString,
            Runtime = reader.Col(16).AsString,
            RuntimeVersion = reader.Col(17).AsString,
            ContextJson = reader.Col(18).AsString,
            TagsJson = reader.Col(19).AsString,
            Timestamp = reader.GetDateTime(20)
        };

    // ==========================================================================
    // Log Messages
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Issue {IssueId} upserted for fingerprint {Fingerprint}")]
    private partial void LogIssueUpserted(string issueId, string fingerprint);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Issue {IssueId} transitioned: {From} -> {To} (reason: {Reason})")]
    private partial void LogStatusTransition(string issueId, string from, string to, string? reason);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Issue {IssueId} assigned to {Owner}")]
    private partial void LogOwnerAssigned(string issueId, string owner);
}

// =============================================================================
// Issue Storage Records
// =============================================================================

/// <summary>
///     Storage row for the <c>error_issues</c> table.
/// </summary>
public sealed record ErrorIssueRow
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string Fingerprint { get; init; }
    public required string Title { get; init; }
    public string? Culprit { get; init; }
    public required string ErrorType { get; init; }
    public required string Category { get; init; }
    public required string Level { get; init; }
    public string? Platform { get; init; }
    public required DateTime FirstSeenAt { get; init; }
    public required DateTime LastSeenAt { get; init; }
    public required long OccurrenceCount { get; init; }
    public required int AffectedUsersCount { get; init; }
    public required string Status { get; init; }
    public string? Substatus { get; init; }
    public required string Priority { get; init; }
    public string? AssignedTo { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public required int RegressionCount { get; init; }
    public string? LastRelease { get; init; }
    public string? TagsJson { get; init; }
    public string? MetadataJson { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
///     Storage row for the <c>error_issue_events</c> table.
/// </summary>
public sealed record ErrorIssueEventRow
{
    public required string Id { get; init; }
    public required string IssueId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    public string? StackFramesJson { get; init; }
    public string? Environment { get; init; }
    public string? ReleaseVersion { get; init; }
    public string? UserId { get; init; }
    public string? UserIp { get; init; }
    public string? RequestUrl { get; init; }
    public string? RequestMethod { get; init; }
    public string? Browser { get; init; }
    public string? Os { get; init; }
    public string? Device { get; init; }
    public string? Runtime { get; init; }
    public string? RuntimeVersion { get; init; }
    public string? ContextJson { get; init; }
    public string? TagsJson { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
///     Storage row for the <c>error_breadcrumbs</c> table.
/// </summary>
public sealed record ErrorBreadcrumbRow
{
    public required string Id { get; init; }
    public required string EventId { get; init; }
    public required string BreadcrumbType { get; init; }
    public string? Category { get; init; }
    public string? Message { get; init; }
    public required string Level { get; init; }
    public string? DataJson { get; init; }
    public required DateTime Timestamp { get; init; }
}
