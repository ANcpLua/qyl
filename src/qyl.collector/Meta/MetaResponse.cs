namespace qyl.collector.Meta;

/// <summary>
///     Server capabilities, configuration status, and connection metadata.
///     Capabilities = what the binary can do (static/compiled-in).
///     Status = what is currently enabled (dynamic/runtime).
/// </summary>
public sealed record MetaResponse
{
    public required string Version { get; init; }
    public required string Runtime { get; init; }
    public required MetaBuild Build { get; init; }
    public required MetaCapabilities Capabilities { get; init; }
    public required MetaStatus Status { get; init; }
    public required MetaLinks Links { get; init; }
    public required MetaPorts Ports { get; init; }
}

/// <summary>
///     Build information for version tracking and diagnostics.
/// </summary>
public sealed record MetaBuild
{
    public string? Commit { get; init; }
    public string? InformationalVersion { get; init; }
}

/// <summary>
///     Static capabilities — what features are compiled into this binary.
/// </summary>
public sealed record MetaCapabilities
{
    public bool Tracing { get; init; }
    public bool Grpc { get; init; }
    public bool Alerting { get; init; }
    public bool GenAi { get; init; }
    public bool Copilot { get; init; }
    public bool EmbeddedDashboard { get; init; }
}

/// <summary>
///     Dynamic status — what is currently enabled and how.
/// </summary>
public sealed record MetaStatus
{
    public bool GrpcEnabled { get; init; }
    public required string AuthMode { get; init; }
}

/// <summary>
///     Connection URLs for service discovery (A2A-style).
/// </summary>
public sealed record MetaLinks
{
    public string? Dashboard { get; init; }
    public string? OtlpHttp { get; init; }
    public string? OtlpGrpc { get; init; }
}

/// <summary>
///     Port assignments for the current instance.
/// </summary>
public sealed record MetaPorts
{
    public int Http { get; init; }
    public int Grpc { get; init; }
}
