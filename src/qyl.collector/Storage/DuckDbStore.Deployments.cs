using qyl.collector.Autofix;

namespace qyl.collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with deployment
///     query operations for the regression detection pipeline.
/// </summary>
public sealed partial class DuckDbStore
{
    /// <summary>
    ///     Gets recent deployments ordered by start_time descending.
    /// </summary>
    public async Task<IReadOnlyList<DeploymentRecord>> GetRecentDeploymentsAsync(
        int limit, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT deployment_id, service_name, service_version, environment,
                                 status, strategy, start_time, end_time, duration_s,
                                 deployed_by, git_commit, git_branch, previous_version,
                                 rollback_target, replica_count, healthy_replicas,
                                 error_message, created_at
                          FROM deployments
                          ORDER BY start_time DESC
                          LIMIT $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = limit });

        var results = new List<DeploymentRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapDeployment(reader));

        return results;
    }

    /// <summary>
    ///     Gets deployments that started after the specified time.
    ///     Used by the regression detection service to check for new deployments since the last poll.
    /// </summary>
    public async Task<IReadOnlyList<DeploymentRecord>> GetDeploymentsAfterAsync(
        DateTime since, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT deployment_id, service_name, service_version, environment,
                                 status, strategy, start_time, end_time, duration_s,
                                 deployed_by, git_commit, git_branch, previous_version,
                                 rollback_target, replica_count, healthy_replicas,
                                 error_message, created_at
                          FROM deployments
                          WHERE start_time > $1
                          ORDER BY start_time ASC
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = since });

        var results = new List<DeploymentRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(MapDeployment(reader));

        return results;
    }

    private static DeploymentRecord MapDeployment(DbDataReader reader) =>
        new()
        {
            DeploymentId = reader.GetString(0),
            ServiceName = reader.GetString(1),
            ServiceVersion = reader.GetString(2),
            Environment = reader.GetString(3),
            Status = reader.GetString(4),
            Strategy = reader.GetString(5),
            StartTime = reader.GetDateTime(6),
            EndTime = reader.Col(7).AsDateTime,
            DurationS = reader.Col(8).AsDouble,
            DeployedBy = reader.Col(9).AsString,
            GitCommit = reader.Col(10).AsString,
            GitBranch = reader.Col(11).AsString,
            PreviousVersion = reader.Col(12).AsString,
            RollbackTarget = reader.Col(13).AsString,
            ReplicaCount = reader.Col(14).AsInt32,
            HealthyReplicas = reader.Col(15).AsInt32,
            ErrorMessage = reader.Col(16).AsString,
            CreatedAt = reader.Col(17).AsDateTime
        };
}
