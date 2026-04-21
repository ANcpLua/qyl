namespace Qyl.Collector.Autofix;

/// <summary>
///     Receives GitHub webhook events, verifies HMAC-SHA256 signatures,
///     stores events, and routes to appropriate handlers.
/// </summary>
public static class GitHubWebhookEndpoints
{
    [QylMapEndpoints]
    public static void MapGitHubWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/github/webhooks", HandleWebhookAsync);

        app.MapGet("/api/v1/github/events", static async Task<IResult> (
            int? limit, string? eventType, string? repoFullName,
            DuckDbStore store, CancellationToken ct) =>
        {
            try
            {
                var clampedLimit = Math.Clamp(limit ?? 50, 1, 1000);
                var items = await store
                    .GetGitHubEventsAsync(clampedLimit, eventType, repoFullName, ct).ConfigureAwait(false);
                return TypedResults.Ok(new { items, total = items.Count });
            }
            catch
            {
                // Avoid breaking Loom dashboard when webhook storage is not initialized.
                return TypedResults.Ok(new { items = Array.Empty<GitHubEventRecord>(), total = 0 });
            }
        });
    }

    private static async Task<IResult> HandleWebhookAsync(
        HttpRequest request,
        IConfiguration configuration,
        DuckDbStore store,
        CancellationToken ct)
    {
        // 1. Read raw request body
        byte[] payload;
        await using (MemoryStream ms = new())
        {
            await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
            payload = ms.ToArray();
        }

        // 2. Read GitHub headers
        var signatureHeader = request.Headers["X-Hub-Signature-256"].ToString();
        var eventType = request.Headers["X-GitHub-Event"].ToString();
        var deliveryId = request.Headers["X-GitHub-Delivery"].ToString();

        if (string.IsNullOrEmpty(signatureHeader))
            signatureHeader = null;
        if (string.IsNullOrEmpty(eventType))
            eventType = "unknown";
        if (string.IsNullOrEmpty(deliveryId))
            deliveryId = Guid.NewGuid().ToString("N");

        // 3. Verify HMAC-SHA256 signature
        var webhookSecret = configuration["QYL_GITHUB_WEBHOOK_SECRET"];
        if (!string.IsNullOrEmpty(webhookSecret))
        {
            if (!VerifySignature(payload, signatureHeader, webhookSecret))
                return TypedResults.Unauthorized();
        }

        // 4. Parse JSON payload
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        // 5. Extract common fields
        var action = root.TryGetProperty("action", out var actionEl)
            ? actionEl.GetString()
            : null;

        var repoFullName = root.TryGetProperty("repository", out var repoEl)
                           && repoEl.TryGetProperty("full_name", out var fullNameEl)
            ? fullNameEl.GetString() ?? "unknown"
            : "unknown";

        var sender = root.TryGetProperty("sender", out var senderEl)
                     && senderEl.TryGetProperty("login", out var loginEl)
            ? loginEl.GetString()
            : null;

        // 6. Extract event-specific fields
        int? prNumber = null;
        string? prUrl = null;
        string? gitRef = null;

        switch (eventType)
        {
            case "pull_request"
                when root.TryGetProperty("pull_request", out var prEl):
            {
                if (prEl.TryGetProperty("number", out var numEl))
                    prNumber = numEl.GetInt32();
                if (prEl.TryGetProperty("html_url", out var urlEl))
                    prUrl = urlEl.GetString();
                break;
            }
            case "push"
                when root.TryGetProperty("ref", out var refEl):
                gitRef = refEl.GetString();
                break;
        }

        // 7. Store event
        GitHubEventRecord record = new()
        {
            EventId = deliveryId,
            EventType = eventType,
            Action = action,
            RepoFullName = repoFullName,
            Sender = sender,
            PrNumber = prNumber,
            PrUrl = prUrl,
            Ref = gitRef,
            PayloadJson = Encoding.UTF8.GetString(payload),
            CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };

        await store.InsertGitHubEventAsync(record, ct).ConfigureAwait(false);

        return TypedResults.Ok(new { eventId = deliveryId, eventType, action });
    }

    private static bool VerifySignature(byte[] payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
            return false;

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(secretBytes, payload);
        var expected = "sha256=" + Convert.ToHexStringLower(hash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHeader));
    }
}
