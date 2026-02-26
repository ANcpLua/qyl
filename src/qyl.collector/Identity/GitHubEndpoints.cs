using Microsoft.AspNetCore.Mvc;

namespace qyl.collector.Identity;

public static class GitHubEndpoints
{
    public static void MapGitHubEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/github");

        group.MapGet("/status", static async (
            [FromServices] GitHubService service, CancellationToken ct) =>
        {
            var user = service.IsConfigured
                ? await service.GetUserAsync(ct)
                : null;

            return Results.Json(
                new GitHubStatusResponse(service.IsConfigured, user, service.AuthMethod),
                GitHubJsonContext.Default.GitHubStatusResponse);
        });

        group.MapPost("/token", static async (
            [FromBody] GitHubTokenRequest request,
            [FromServices] GitHubService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "Token is required" });

            var user = await service.SetTokenAsync(request.Token, "pat", ct);
            if (user is null)
                return Results.BadRequest(new { error = "Invalid GitHub token" });

            return Results.Json(
                new GitHubStatusResponse(true, user, "pat"),
                GitHubJsonContext.Default.GitHubStatusResponse);
        });

        group.MapDelete("/token", static async (
            [FromServices] GitHubService service, CancellationToken ct) =>
        {
            await service.ClearTokenAsync(ct);

            var user = service.IsConfigured
                ? await service.GetUserAsync(ct)
                : null;

            return Results.Json(
                new GitHubStatusResponse(service.IsConfigured, user, service.AuthMethod),
                GitHubJsonContext.Default.GitHubStatusResponse);
        });

        group.MapGet("/device/available", static (
            [FromServices] GitHubService service) =>
            Results.Json(
                new DeviceAvailableResponse(service.IsDeviceFlowAvailable),
                GitHubJsonContext.Default.DeviceAvailableResponse));

        group.MapPost("/device/start", static async (
            [FromServices] GitHubService service, CancellationToken ct) =>
        {
            var response = await service.StartDeviceFlowAsync(ct);
            return response is null
                ? Results.BadRequest(new { error = "Device flow not available. Set QYL_GITHUB_CLIENT_ID." })
                : Results.Json(response, GitHubJsonContext.Default.DeviceCodeResponse);
        });

        group.MapGet("/device/poll", static async (
            [FromQuery] string deviceCode,
            [FromServices] GitHubService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(deviceCode))
                return Results.BadRequest(new { error = "deviceCode is required" });

            var response = await service.PollDeviceFlowAsync(deviceCode, ct);
            return Results.Json(response, GitHubJsonContext.Default.DevicePollResponse);
        });

        group.MapGet("/repos", static async (
            [FromServices] GitHubService service, CancellationToken ct) =>
        {
            if (!service.IsConfigured)
                return Results.Json(
                    new { error = "GitHub token not configured" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            var repos = await service.GetRepositoriesAsync(ct);
            return Results.Json(repos, GitHubJsonContext.Default.GitHubRepoArray);
        });
    }
}
