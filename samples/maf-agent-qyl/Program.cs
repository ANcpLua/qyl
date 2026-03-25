using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Qyl.Samples.MafAgent;

/// <summary>
///     Single-file Loom product showcase using Microsoft Agent Framework hosting extensions.
///     The sample is capability-first:
///     context surfaces, settings/controls, investigate/fix, code review, and coding-agent delegation.
///     It still keeps Loom handoff state explicit instead of pretending that one conversation ID
///     creates shared memory across bounded agents.
/// </summary>
internal static class Program
{
    internal const string DiagnosticianAgent = "loom.diagnostician";
    internal const string StrategistAgent = "loom.strategist";
    internal const string CoderAgent = "loom.coder";
    internal const string ReviewerAgent = "loom.reviewer";
    internal const string DelegateAgent = "loom.delegate";

    private static async Task Main()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<IChatClient, MockLoomChatClient>();
        builder.Services.AddSingleton<LoomRunStore>();
        builder.Services.AddSingleton<PullRequestToolService>();
        builder.Services.AddSingleton<LoomGovernancePolicy>();
        builder.Services.AddScoped<LoomSubsystemDemo>();

        builder.AddAIAgent(
                DiagnosticianAgent,
                LoomInstructions.Diagnostician,
                ServiceLifetime.Scoped)
            .WithInMemorySessionStore();

        builder.AddAIAgent(
                StrategistAgent,
                LoomInstructions.Strategist,
                ServiceLifetime.Scoped)
            .WithInMemorySessionStore();

        builder.AddAIAgent(
                CoderAgent,
                LoomInstructions.Coder,
                ServiceLifetime.Scoped)
            .WithInMemorySessionStore()
            .WithAITool(
                static _ => AIFunctionFactory.Create(
                    static (string repository, string branch, string title) =>
                        PullRequestToolService.CreatePullRequest(repository, branch, title),
                    "CreatePullRequest",
                    "Creates a pull request for the proposed fix."),
                ServiceLifetime.Scoped);

        builder.AddAIAgent(
                ReviewerAgent,
                LoomInstructions.Reviewer,
                ServiceLifetime.Scoped)
            .WithInMemorySessionStore();

        builder.AddAIAgent(
                DelegateAgent,
                LoomInstructions.Delegate,
                ServiceLifetime.Scoped)
            .WithInMemorySessionStore();

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LoomSubsystemDemo>().RunAsync().ConfigureAwait(false);
    }

    private static class LoomInstructions
    {
        public const string Diagnostician =
            """
            You are Loom Diagnostician.
            Find the most likely production root cause.
            Cite the specific broken assumption, affected service boundary, and the change that introduced the fault.
            """;

        public const string Strategist =
            """
            You are Loom Strategist.
            Turn the diagnosis into the smallest safe fix plan with explicit regression protection.
            """;

        public const string Coder =
            """
            You are Loom Coder.
            When the plan is concrete enough, call CreatePullRequest exactly once and then summarize the draft.
            """;

        public const string Reviewer =
            """
            You are Loom Reviewer.
            Review the generated patch for regressions, missing validation, and missing tests.
            """;

        public const string Delegate =
            """
            You are Loom Delegate.
            Prepare a compact handoff packet for an external coding agent.
            Preserve the root cause, safe plan, review warnings, and the exact allowed action surface.
            """;
    }
}

internal sealed class LoomSubsystemDemo(
    IServiceProvider services,
    LoomRunStore runStore,
    LoomGovernancePolicy governancePolicy,
    TimeProvider timeProvider)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        const string issueId = "issue-checkout-fetch";

        LoomIssueContext context = new(
            """
            React checkout fails with "TypeError: Failed to fetch".
            2.4k events, 2.2k affected users, /checkout path.
            """,
            """
            frontend -> checkout-api -> inventory-api
            inventory-api returns 500 on low stock and checkout-api stopped degrading gracefully.
            """,
            """
            error: backend checkout returned 500
            warning: inventory response was null
            breadcrumb: recent refactor removed response guard
            """,
            """
            checkout-api latency spiked after a refactor two hours ago.
            most time is spent waiting on inventory-api.
            """,
            """
            checkout-api/CheckoutController.cs removed a null/status guard around inventory responses.
            frontend checkout page still assumes a stable response contract.
            """);

        LoomControls controls = new(
            true,
            true,
            true,
            true,
            true,
            ["ancplua/qyl-sample-checkout"]);

        var run = runStore.GetOrCreate(issueId, context, controls);
        run.Governance = governancePolicy.Evaluate(run.Context, run.Controls);

        WriteBanner("Loom / MAF hosted subsystem");
        Console.WriteLine($"Run: {run.RunId}");
        Console.WriteLine($"Started: {timeProvider.GetUtcNow():O}");
        Console.WriteLine();

        WriteBanner("Context Surfaces");
        Console.WriteLine(run.Context.FormatSummary());
        Console.WriteLine();

        WriteBanner("Settings / Controls");
        Console.WriteLine(run.Governance.FormatSummary());
        Console.WriteLine();

        WriteBanner("Investigate / Fix");
        run.Diagnosis = await RunStreamingAgentAsync(
                Program.DiagnosticianAgent,
                run,
                """
                Investigate the production issue.
                Focus on the root cause, causal chain, and service boundary that broke.
                """,
                "diagnosis",
                ct)
            .ConfigureAwait(false);

        run.Plan = await RunAgentAsync(
                Program.StrategistAgent,
                run,
                $"""
                 Build the minimal safe fix plan for this diagnosis:

                 {run.Diagnosis}
                 """,
                "plan",
                ct)
            .ConfigureAwait(false);

        if (run.Governance.AllowCodeGeneration)
        {
            run.PullRequestSummary = await RunAgentAsync(
                    Program.CoderAgent,
                    run,
                    $"""
                     Implement the approved fix plan and draft the pull request.

                     Repository: ancplua/qyl-sample-checkout
                     Branch: autofix/checkout-fetch
                     Title: fix(checkout): restore inventory failure guard

                     Approved plan:
                     {run.Plan}
                     """,
                    "fix",
                    ct)
                .ConfigureAwait(false);
        }
        else
        {
            run.PullRequestSummary = "Code generation blocked by Loom governance controls.";
            Console.WriteLine("[fix]");
            Console.WriteLine(run.PullRequestSummary);
            Console.WriteLine();
        }

        WriteBanner("Code Review");
        run.Review = await RunAgentAsync(
                Program.ReviewerAgent,
                run,
                $"""
                 Review this generated pull request summary and call out the highest-risk gap first.

                 {run.PullRequestSummary}
                 """,
                "review",
                ct)
            .ConfigureAwait(false);

        WriteBanner("Coding Agent Delegation");
        if (run.Governance.AllowExternalDelegation)
        {
            run.DelegationPacket = await RunAgentAsync(
                    Program.DelegateAgent,
                    run,
                    $"""
                     Prepare an external coding-agent handoff packet.

                     Context surfaces:
                     {run.Context.FormatSummary()}

                     Governance:
                     {run.Governance.FormatSummary()}

                     Diagnosis:
                     {run.Diagnosis}

                     Plan:
                     {run.Plan}

                     Review:
                     {run.Review}
                     """,
                    "delegate",
                    ct)
                .ConfigureAwait(false);
        }
        else
        {
            run.DelegationPacket = "External delegation blocked by Loom governance controls.";
            Console.WriteLine("[delegate]");
            Console.WriteLine(run.DelegationPacket);
            Console.WriteLine();
        }

        Console.WriteLine();
        WriteBanner("Final Summary");
        Console.WriteLine(run.FormatSummary());
    }

    private async Task<string> RunStreamingAgentAsync(
        string agentKey,
        LoomRunState run,
        string prompt,
        string label,
        CancellationToken ct)
    {
        var agent = services.GetRequiredKeyedService<AIAgent>(agentKey);
        var session = await run.GetOrCreateSessionAsync(agentKey, agent, ct).ConfigureAwait(false);

        Console.WriteLine($"[{label}]");
        Console.WriteLine();

        var text = new StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(prompt, session, cancellationToken: ct)
                           .ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(update.Text))
                continue;

            text.Append(update.Text);
            Console.Write(update.Text);
        }

        Console.WriteLine();
        Console.WriteLine();
        return text.ToString().Trim();
    }

    private async Task<string> RunAgentAsync(
        string agentKey,
        LoomRunState run,
        string prompt,
        string label,
        CancellationToken ct)
    {
        var agent = services.GetRequiredKeyedService<AIAgent>(agentKey);
        var session = await run.GetOrCreateSessionAsync(agentKey, agent, ct).ConfigureAwait(false);
        var response = await agent.RunAsync(prompt, session, cancellationToken: ct).ConfigureAwait(false);

        Console.WriteLine($"[{label}]");
        Console.WriteLine(response.Text);
        Console.WriteLine();

        return response.Text;
    }

    private static void WriteBanner(string title)
    {
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
    }
}

internal sealed class LoomRunStore(TimeProvider timeProvider)
{
    private readonly Dictionary<string, LoomRunState> _runs = new(StringComparer.Ordinal);

    public LoomRunState GetOrCreate(string issueId, LoomIssueContext context, LoomControls controls)
    {
        if (_runs.TryGetValue(issueId, out var existing))
            return existing;

        var now = timeProvider.GetUtcNow();
        var created = new LoomRunState
        {
            RunId = issueId,
            Context = context,
            Controls = controls,
            CreatedAt = now,
            UpdatedAt = now
        };

        _runs.Add(issueId, created);
        return created;
    }
}

internal sealed class LoomRunState
{
    private readonly Dictionary<string, AgentSession> _agentSessions = new(StringComparer.Ordinal);

    public required string RunId { get; init; }
    public required LoomIssueContext Context { get; init; }
    public required LoomControls Controls { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public LoomGovernanceDecision Governance { get; set; } = LoomGovernanceDecision.AllowAll;
    public string Diagnosis { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string PullRequestSummary { get; set; } = string.Empty;
    public string Review { get; set; } = string.Empty;
    public string DelegationPacket { get; set; } = string.Empty;

    public async ValueTask<AgentSession> GetOrCreateSessionAsync(
        string agentKey,
        AIAgent agent,
        CancellationToken ct)
    {
        if (_agentSessions.TryGetValue(agentKey, out var existing))
            return existing;

        var created = await agent.CreateSessionAsync(ct).ConfigureAwait(false);
        _agentSessions.Add(agentKey, created);
        return created;
    }

    public string FormatSummary() =>
        $"""
         Context Surfaces
         ----------------
         {Context.FormatSummary()}

         Settings / Controls
         -------------------
         {Governance.FormatSummary()}

         Diagnosis
         ---------
         {Diagnosis}

         Plan
         ----
         {Plan}

         Pull Request
         ------------
         {PullRequestSummary}

         Review
         ------
         {Review}

         Delegation
         ----------
         {DelegationPacket}
         """;
}

internal sealed record LoomIssueContext(
    string IssueDetails,
    string TraceSummary,
    string Logs,
    string Profiles,
    string LinkedCode)
{
    public string FormatSummary() =>
        $"""
         Issue Details:
         {IssueDetails}

         Traces:
         {TraceSummary}

         Logs:
         {Logs}

         Profiles:
         {Profiles}

         Linked Code:
         {LinkedCode}
         """;
}

internal sealed record LoomControls(
    bool RecordInputs,
    bool RecordOutputs,
    bool AllowCodeGeneration,
    bool AllowPullRequestCreation,
    bool AllowExternalDelegation,
    IReadOnlyList<string> AllowedRepositories);

internal sealed record LoomGovernanceDecision(
    bool AllowInvestigate,
    bool AllowCodeGeneration,
    bool AllowPullRequestCreation,
    bool AllowExternalDelegation,
    string Reason)
{
    public static readonly LoomGovernanceDecision AllowAll =
        new(true, true, true, true, "All product capabilities are enabled.");

    public string FormatSummary() =>
        $"""
         Investigate: {AllowInvestigate}
         Code generation: {AllowCodeGeneration}
         PR creation: {AllowPullRequestCreation}
         External delegation: {AllowExternalDelegation}
         Reason: {Reason}
         """;
}

internal sealed class LoomGovernancePolicy
{
    public LoomGovernanceDecision Evaluate(LoomIssueContext context, LoomControls controls)
    {
        var hasRuntimeContext =
            !string.IsNullOrWhiteSpace(context.IssueDetails) &&
            !string.IsNullOrWhiteSpace(context.TraceSummary) &&
            !string.IsNullOrWhiteSpace(context.Logs);

        var allowInvestigate = hasRuntimeContext;
        var allowCodeGeneration = allowInvestigate && controls.AllowCodeGeneration;
        var allowPullRequestCreation =
            allowCodeGeneration &&
            controls.AllowPullRequestCreation &&
            controls.AllowedRepositories.Count > 0;

        var allowExternalDelegation =
            allowInvestigate &&
            controls.AllowExternalDelegation &&
            controls.RecordInputs &&
            controls.RecordOutputs;

        var reason = allowPullRequestCreation
            ? "Runtime context, output recording, and repository allow-list are present."
            : "At least one control gate is closed; Loom may analyze but cannot use every write surface.";

        return new LoomGovernanceDecision(
            allowInvestigate,
            allowCodeGeneration,
            allowPullRequestCreation,
            allowExternalDelegation,
            reason);
    }
}

internal sealed class PullRequestToolService
{
    public static PullRequestDraft CreatePullRequest(string repository, string branch, string title) =>
        new(
            repository,
            branch,
            title,
            """
            diff --git a/src/CheckoutController.cs b/src/CheckoutController.cs
            @@
            - return await _inventoryClient.FetchAsync(request.ProductId, ct);
            + var result = await _inventoryClient.FetchAsync(request.ProductId, ct);
            + if (result is null || result.StatusCode >= 500)
            + {
            +     return NoContent();
            + }
            + return result;
            @@
            + // Added regression test for low-inventory backend failures.
            """,
            $"https://example.invalid/{repository}/pull/{branch.Replace('/', '-')}");
}

internal sealed record PullRequestDraft(
    string Repository,
    string Branch,
    string Title,
    string Diff,
    string Url);

internal sealed class MockLoomChatClient : IChatClient
{
    private const string SampleModelId = "mock-seer-agent";

    public ChatClientMetadata Metadata { get; } = new("mock-loom", null, SampleModelId);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        var instructions = options?.Instructions ?? string.Empty;
        var lastToolResult = messageList
            .Where(static message => message.Role == ChatRole.Tool)
            .SelectMany(static message => message.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault();

        if (lastToolResult is not null)
        {
            return Task.FromResult(CreateTextResponse(
                $"""
                 Pull request drafted successfully.
                 Tool output: {lastToolResult.Result}
                 The fix is small, reviewable, and ready for human approval.
                 """,
                messageList));
        }

        if (ContainsOrdinal(instructions, "Diagnostician"))
        {
            return Task.FromResult(CreateTextResponse(
                """
                Root cause: checkout-api stopped guarding inventory-api failures after a refactor.
                Causal chain: frontend calls checkout-api -> checkout-api calls inventory-api -> inventory-api returns 500 on low stock -> checkout-api now forwards the broken response instead of degrading gracefully -> frontend surfaces Failed to fetch.
                The introducing change is the removed null/status guard around the inventory response.
                """,
                messageList));
        }

        if (ContainsOrdinal(instructions, "Strategist"))
        {
            return Task.FromResult(CreateTextResponse(
                """
                1. Restore the inventory response guard in checkout-api.
                2. Convert upstream inventory 5xx or null results into a stable no-content / unavailable response.
                3. Add a regression test covering low-inventory backend failure handling.
                4. Keep the patch local to checkout-api unless traces prove a broader contract bug.
                """,
                messageList));
        }

        if (ContainsOrdinal(instructions, "Coder") && options?.Tools is { Count: > 0 })
        {
            Dictionary<string, object?> arguments = new(StringComparer.Ordinal)
            {
                ["repository"] = "ancplua/qyl-sample-checkout",
                ["branch"] = "autofix/checkout-fetch",
                ["title"] = "fix(checkout): restore inventory failure guard"
            };

            var toolCall = new FunctionCallContent("pr-call-1", "CreatePullRequest", arguments);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [toolCall]))
            {
                ModelId = SampleModelId, Usage = CreateUsage(messageList, "tool-call")
            });
        }

        if (ContainsOrdinal(instructions, "Reviewer"))
        {
            return Task.FromResult(CreateTextResponse(
                """
                Highest-risk gap: the patch handles backend failure, but the review still needs an assertion that the frontend receives a stable response shape instead of an implicit transport failure.
                Add one integration test around the checkout contract and this diff is mergeable.
                """,
                messageList));
        }

        if (ContainsOrdinal(instructions, "Delegate"))
        {
            return Task.FromResult(CreateTextResponse(
                """
                External coding agent handoff packet
                - objective: restore the checkout boundary guard without widening scope
                - approved action surface: checkout-api only, plus one regression test
                - root cause: removed inventory null/status guard after refactor
                - must preserve: stable frontend response contract and reviewability
                - review warning: add a contract-level assertion for the frontend-visible response shape
                """,
                messageList));
        }

        return Task.FromResult(CreateTextResponse("No scenario matched.", messageList));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var chunk in SplitIntoChunks(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk) { ModelId = SampleModelId };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? Metadata : null;

    public void Dispose() { }

    private static ChatResponse CreateTextResponse(string text, IReadOnlyList<ChatMessage> messages) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { ModelId = SampleModelId, Usage = CreateUsage(messages, text) };

    private static UsageDetails CreateUsage(IReadOnlyList<ChatMessage> messages, string responseText)
    {
        var inputChars = messages.Sum(static message => message.Text?.Length ?? 0);
        return new UsageDetails
        {
            InputTokenCount = Math.Max(1, inputChars / 4),
            OutputTokenCount = Math.Max(1, responseText.Length / 4),
            TotalTokenCount = Math.Max(1, (inputChars + responseText.Length) / 4)
        };
    }

    private static IEnumerable<string> SplitIntoChunks(string text)
    {
        const int chunkSize = 48;

        for (var offset = 0; offset < text.Length; offset += chunkSize)
            yield return text.Substring(offset, Math.Min(chunkSize, text.Length - offset));
    }

    private static bool ContainsOrdinal(string value, string substring) =>
        value.AsSpan().IndexOf(substring.AsSpan(), StringComparison.Ordinal) >= 0;
}
