namespace qyl.collector.Autofix;

/// <summary>
///     Receives GitHub webhook events, verifies HMAC-SHA256 signatures,
///     stores events, and routes to appropriate handlers.
/// </summary>
public static class GitHubWebhookEndpoints
{
    public static void MapGitHubWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/github/webhooks", HandleWebhookAsync);

        app.MapGet("/api/v1/github/events", static async (
            int? limit, string? eventType, string? repoFullName,
            DuckDbStore store, CancellationToken ct) =>
        {
            try
            {
                int clampedLimit = Math.Clamp(limit ?? 50, 1, 1000);
                IReadOnlyList<GitHubEventRecord> items = await store
                    .GetGitHubEventsAsync(clampedLimit, eventType, repoFullName, ct).ConfigureAwait(false);
                return Results.Ok(new { items, total = items.Count });
            }
            catch
            {
                // Avoid breaking Seer dashboard when webhook storage is not initialized.
                return Results.Ok(new { items = Array.Empty<GitHubEventRecord>(), total = 0 });
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
        using (MemoryStream ms = new())
        {
            await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
            payload = ms.ToArray();
        }

        // 2. Read GitHub headers
        string? signatureHeader = request.Headers["X-Hub-Signature-256"].ToString();
        string? eventType = request.Headers["X-GitHub-Event"].ToString();
        string? deliveryId = request.Headers["X-GitHub-Delivery"].ToString();

        if (string.IsNullOrEmpty(signatureHeader))
            signatureHeader = null;
        if (string.IsNullOrEmpty(eventType))
            eventType = "unknown";
        if (string.IsNullOrEmpty(deliveryId))
            deliveryId = Guid.NewGuid().ToString("N");

        // 3. Verify HMAC-SHA256 signature
        string? webhookSecret = configuration["QYL_GITHUB_WEBHOOK_SECRET"];
        if (!string.IsNullOrEmpty(webhookSecret))
        {
            if (!VerifySignature(payload, signatureHeader, webhookSecret))
                return Results.Unauthorized();
        }

        // 4. Parse JSON payload
        using JsonDocument doc = JsonDocument.Parse(payload);
        JsonElement root = doc.RootElement;

        // 5. Extract common fields
        string? action = root.TryGetProperty("action", out JsonElement actionEl)
            ? actionEl.GetString()
            : null;

        string repoFullName = root.TryGetProperty("repository", out JsonElement repoEl)
            && repoEl.TryGetProperty("full_name", out JsonElement fullNameEl)
                ? fullNameEl.GetString() ?? "unknown"
                : "unknown";

        string? sender = root.TryGetProperty("sender", out JsonElement senderEl)
            && senderEl.TryGetProperty("login", out JsonElement loginEl)
                ? loginEl.GetString()
                : null;

        // 6. Extract event-specific fields
        int? prNumber = null;
        string? prUrl = null;
        string? gitRef = null;

        if (eventType == "pull_request"
            && root.TryGetProperty("pull_request", out JsonElement prEl))
        {
            if (prEl.TryGetProperty("number", out JsonElement numEl))
                prNumber = numEl.GetInt32();
            if (prEl.TryGetProperty("html_url", out JsonElement urlEl))
                prUrl = urlEl.GetString();
        }

        if (eventType == "push"
            && root.TryGetProperty("ref", out JsonElement refEl))
        {
            gitRef = refEl.GetString();
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

        return Results.Ok(new { eventId = deliveryId, eventType, action });
    }

    private static bool VerifySignature(byte[] payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
            return false;

        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        byte[] hash = HMACSHA256.HashData(secretBytes, payload);
        string expected = "sha256=" + Convert.ToHexStringLower(hash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHeader));
    }
}
