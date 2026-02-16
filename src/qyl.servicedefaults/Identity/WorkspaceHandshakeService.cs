using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qyl.ServiceDefaults.Internal;

namespace Qyl.ServiceDefaults.Identity;

/// <summary>
///     Hosted service that performs the workspace handshake on startup
///     and sends periodic heartbeats to the collector.
/// </summary>
internal sealed partial class WorkspaceHandshakeService(
    WorkspaceContext context,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment environment,
    ILogger<WorkspaceHandshakeService> logger) : IHostedService, IDisposable
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);
    private Task? _backgroundTask;

    private CancellationTokenSource? _cts;

    public void Dispose() => _cts?.Dispose();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_backgroundTask is not null)
        {
            await Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken))
                .ConfigureAwait(false);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // Perform handshake with retry
        await PerformHandshakeWithRetryAsync(ct).ConfigureAwait(false);

        // If registered, start heartbeat loop
        if (context.IsRegistered)
        {
            await HeartbeatLoopAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task PerformHandshakeWithRetryAsync(CancellationToken ct)
    {
        var delay = InitialRetryDelay;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await PerformHandshakeAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogHandshakeFailed(attempt, MaxRetries, ex.Message);

                if (attempt < MaxRetries)
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    delay *= 2; // Exponential backoff
                }
            }
        }

        LogHandshakeGaveUp(MaxRetries);
    }

    private async Task PerformHandshakeAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("qyl-handshake");
        var serviceName = environment.ApplicationName;

        var entryAssembly = Assembly.GetEntryAssembly();
        var sdkAssembly = typeof(WorkspaceHandshakeService).Assembly;

        var request = new HandshakeRequestDto
        {
            ServiceName = serviceName,
            SdkVersion = sdkAssembly.TryGetPackageVersion(out var sdkVer) ? sdkVer : "unknown",
            RuntimeVersion = Environment.Version.ToString(),
            Framework = entryAssembly?.GetCustomAttribute<TargetFrameworkAttribute>()
                    ?.FrameworkName ?? $".NET {Environment.Version}",
            GitCommit = Environment.GetEnvironmentVariable("GIT_COMMIT")
                        ?? entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                            ?.InformationalVersion
        };

        var response = await client.PostAsJsonAsync(
            "/api/v1/onboarding/handshake/start", request, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<HandshakeResponseDto>(ct).ConfigureAwait(false);

        if (result?.WorkspaceId is not null)
        {
            context.WorkspaceId = result.WorkspaceId;
            context.ServiceName = serviceName;
            LogHandshakeSucceeded(result.WorkspaceId, serviceName);
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var client = httpClientFactory.CreateClient("qyl-handshake");
                var response = await client.PostAsync(
                    $"/api/v1/workspaces/{context.WorkspaceId}/heartbeat", null, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    LogHeartbeatFailed((int)response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogHeartbeatError(ex.Message);
            }
        }
    }

    // =========================================================================
    // LoggerMessage — structured, zero-allocation logging
    // =========================================================================

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Workspace handshake attempt {Attempt}/{MaxAttempts} failed: {Error}")]
    private partial void LogHandshakeFailed(int attempt, int maxAttempts, string error);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Workspace handshake gave up after {MaxAttempts} attempts. App continues without identity.")]
    private partial void LogHandshakeGaveUp(int maxAttempts);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Workspace registered: {WorkspaceId} for service {ServiceName}")]
    private partial void LogHandshakeSucceeded(string workspaceId, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Workspace heartbeat failed with status {StatusCode}")]
    private partial void LogHeartbeatFailed(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Workspace heartbeat error: {Error}")]
    private partial void LogHeartbeatError(string error);
}

// Local DTOs — avoids coupling to collector types
internal sealed class HandshakeRequestDto
{
    [JsonPropertyName("serviceName")] public required string ServiceName { get; init; }

    [JsonPropertyName("sdkVersion")] public string? SdkVersion { get; init; }

    [JsonPropertyName("runtimeVersion")] public string? RuntimeVersion { get; init; }

    [JsonPropertyName("framework")] public string? Framework { get; init; }

    [JsonPropertyName("gitCommit")] public string? GitCommit { get; init; }
}

internal sealed class HandshakeResponseDto
{
    [JsonPropertyName("workspaceId")] public string? WorkspaceId { get; init; }

    [JsonPropertyName("status")] public string? Status { get; init; }
}
