#nullable enable

namespace Qyl.Domains.Workspace;

public sealed class ProjectEntity
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
}

public sealed class ProjectEnvironmentEntity
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Color { get; init; }
    public required int SortOrder { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class LocalNodeEntity
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string EnvironmentId { get; init; }
    public required string Hostname { get; init; }
    public string? MachineId { get; init; }
    public string? AgentVersion { get; init; }
    public string? OsType { get; init; }
    public string? OsVersion { get; init; }
    public required DateTimeOffset FirstSeenAt { get; init; }
    public required DateTimeOffset LastSeenAt { get; init; }
    public required Qyl.Domains.Workspace.NodeStatus Status { get; init; }
}

public sealed class WorkspaceEnvelopeEntity
{
    public required string Id { get; init; }
    public required string ProjectId { get; init; }
    public required string EnvironmentId { get; init; }
    public required string NodeId { get; init; }
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public DateTimeOffset? HeartbeatAt { get; init; }
    public required int HeartbeatIntervalSeconds { get; init; }
    public required Qyl.Domains.Workspace.WorkspaceStatus Status { get; init; }
    public string? ConfigJson { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class HandshakeSessionEntity
{
    public required string Id { get; init; }
    public required string WorkspaceId { get; init; }
    public required string Challenge { get; init; }
    public required string ChallengeMethod { get; init; }
    public string? BrowserFingerprint { get; init; }
    public string? OriginUrl { get; init; }
    public required Qyl.Domains.Workspace.HandshakeState State { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class WorkspaceTokenEntity
{
    public required string Id { get; init; }
    public required string WorkspaceId { get; init; }
    public required string Name { get; init; }
    public required string TokenHash { get; init; }
    public required string TokenPrefix { get; init; }
    public string? ScopesJson { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class ProcessRegistryEntity
{
    public required string Id { get; init; }
    public required string WorkspaceId { get; init; }
    public required Qyl.Domains.Workspace.ProcessType ProcessType { get; init; }
    public required int Pid { get; init; }
    public int? Port { get; init; }
    public string? Protocol { get; init; }
    public string? BinaryPath { get; init; }
    public string? BinaryVersion { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? LastHeartbeatAt { get; init; }
    public required Qyl.Domains.Workspace.ProcessStatus Status { get; init; }
}

public sealed class PortLeaseEntity
{
    public required int Port { get; init; }
    public required string WorkspaceId { get; init; }
    public required string ProcessId { get; init; }
    public required string Protocol { get; init; }
    public required DateTimeOffset LeasedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }
}

public enum NodeStatus
{
    Online,
    Offline,
    Stale
}

public enum WorkspaceStatus
{
    Active,
    Suspended,
    Archived
}

public enum HandshakeState
{
    Pending,
    Verified,
    Expired,
    Rejected
}

public enum ProcessType
{
    Collector,
    Mcp,
    Watch,
    Watchdog,
    Cli
}

public enum ProcessStatus
{
    Running,
    Stopped,
    Crashed
}
