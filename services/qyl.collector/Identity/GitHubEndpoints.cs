using Microsoft.AspNetCore.Mvc;

namespace Qyl.Collector.Identity;

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

            return TypedResults.Json(
                new GitHubStatusResponse(service.IsConfigured, user, service.AuthMethod),
                GitHubJsonContext.Default.GitHubStatusResponse);
        });

        group.MapPost("/token", static async Task<IResult> (
            [FromBody] GitHubTokenRequest request,
            [FromServices] GitHubService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return TypedResults.BadRequest(new { error = "Token is required" });

            if (await service.SetTokenAsync(request.Token, "pat", ct) is not { } user)
                return TypedResults.BadRequest(new { error = "Invalid GitHub token" });

            return TypedResults.Json(
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

            return TypedResults.Json(
                new GitHubStatusResponse(service.IsConfigured, user, service.AuthMethod),
                GitHubJsonContext.Default.GitHubStatusResponse);
        });

        group.MapGet("/device/available", static (
                [FromServices] GitHubService service) =>
            TypedResults.Json(
                new DeviceAvailableResponse(service.IsDeviceFlowAvailable),
                GitHubJsonContext.Default.DeviceAvailableResponse));

        group.MapPost("/device/start", static async Task<IResult> (
            [FromServices] GitHubService service, CancellationToken ct) =>
        {
            var response = await service.StartDeviceFlowAsync(ct);
            return response is null
                ? TypedResults.BadRequest(new { error = "Device flow not available. Set QYL_GITHUB_CLIENT_ID." })
                : TypedResults.Json(response, GitHubJsonContext.Default.DeviceCodeResponse);
        });

        group.MapGet("/device/poll", static async Task<IResult> (
            [FromQuery] string deviceCode,
            [FromServices] GitHubService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(deviceCode))
                return TypedResults.BadRequest(new { error = "deviceCode is required" });

            var response = await service.PollDeviceFlowAsync(deviceCode, ct);
            return TypedResults.Json(response, GitHubJsonContext.Default.DevicePollResponse);
        });

        group.MapGet("/repos", static async Task<IResult> (
            [FromServices] GitHubService service, CancellationToken ct) =>
        {
            if (!service.IsConfigured)
            {
                return TypedResults.Json(
                    new { error = "GitHub token not configured" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var repos = await service.GetRepositoriesAsync(ct);
            return TypedResults.Json(repos, GitHubJsonContext.Default.GitHubRepoArray);
        });
    }
}
