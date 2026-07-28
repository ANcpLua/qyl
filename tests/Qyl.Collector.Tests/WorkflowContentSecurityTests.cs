using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Tests;

/// <summary>
/// Captured workflow content is agent output — tool results, file contents, messages. These
/// assert the three properties that decide whether storing it is safe: the key outlives the
/// data, a run cannot read another run's payload, and a malformed reference is rejected rather
/// than crashing the ingest path.
/// </summary>
public sealed class WorkflowContentSecurityTests
{
    private static readonly DateTimeOffset s_startedAt =
        new(2026, 7, 28, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The production fallback used to derive the content key from QYL_OTLP_PRIMARY_API_KEY.
    /// That key rotates; the data it encrypts does not get re-encrypted, so a routine ingest-key
    /// rotation silently destroyed every previously captured payload — surfacing later as an
    /// AES-GCM tag mismatch, which reads like corruption rather than a key-management mistake.
    /// Refusing to boot is the correct trade: a collector that will not start is recoverable.
    /// </summary>
    [Fact]
    public void Content_key_is_never_derived_from_the_rotatable_ingest_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QYL_OTLP_PRIMARY_API_KEY"] = "an-ingest-key-that-will-be-rotated",
            })
            .Build();

        var failure = Assert.Throws<InvalidOperationException>(
            () => WorkflowContentProtector.FromConfiguration(configuration, new ProductionEnvironment()));

        Assert.Contains("QYL_WORKFLOW_CONTENT_KEY", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An explicit, independently rotatable key is still accepted.</summary>
    [Fact]
    public void An_explicit_content_key_is_accepted_in_production()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QYL_WORKFLOW_CONTENT_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            })
            .Build();

        Assert.NotNull(WorkflowContentProtector.FromConfiguration(configuration, new ProductionEnvironment()));
    }

    /// <summary>
    /// workflow_content is deduplicated per project by digest, so the existence check used to
    /// pass for any content captured by ANY run in the project. Because GetWorkflowContentAsync
    /// authorises on the reference row existing for the asking run, minting that row was the
    /// whole exploit: reference another run's digest, then read its payload back as your own.
    /// </summary>
    [Fact]
    public async Task A_run_cannot_reference_content_captured_by_another_run()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store, "run-victim");
        await CreateRunAsync(store, "run-attacker");

        const string secret = "AWS_SECRET_ACCESS_KEY=not-actually-a-real-key";
        var secretRef = ContentRef(secret);

        // The victim run captures the payload and references it legitimately.
        await store.AppendWorkflowEventsAsync(
            "project-a", "run-victim", "observer-1",
            [Event("victim-1", 1, WorkflowJournalEventKind.AttemptStarted, "attempt-1", [secretRef])],
            [new WorkflowContentWrite(secretRef, "text/plain", WorkflowContentEncoding.Utf8, secret)],
            TestContext.Current.CancellationToken);

        // The attacker run knows only the digest and captures nothing.
        var reach = await Assert.ThrowsAsync<WorkflowEventConflictException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a", "run-attacker", "observer-2",
                [Event("attacker-1", 1, WorkflowJournalEventKind.AttemptStarted, "attempt-1", [secretRef])],
                [],
                TestContext.Current.CancellationToken));
        Assert.Contains("has not captured", reach.Message, StringComparison.Ordinal);

        // And the payload stays unreadable through the attacker's run scope.
        Assert.Null(await store.GetWorkflowContentAsync(
            "project-a", "run-attacker", secretRef, TestContext.Current.CancellationToken));
        Assert.NotNull(await store.GetWorkflowContentAsync(
            "project-a", "run-victim", secretRef, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The ^sha256:[a-f0-9]{64}$ pattern is an OpenAPI constraint with no runtime enforcement in
    /// the generated contract, so an observer can post anything. A ref shorter than the prefix
    /// threw ArgumentOutOfRangeException out of the digest slice — a 500 on attacker-controlled
    /// input rather than a rejected request.
    /// </summary>
    [Theory]
    [InlineData("sha")]
    [InlineData("")]
    [InlineData("sha256:")]
    [InlineData("sha256:tooshort")]
    [InlineData("md5:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("sha256:0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    public async Task A_malformed_content_reference_is_rejected_not_crashed_on(string contentRef)
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store, "run-1");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "observer-1",
                [Event("e1", 1, WorkflowJournalEventKind.AttemptStarted, "attempt-1", [])],
                [new WorkflowContentWrite(contentRef, "text/plain", WorkflowContentEncoding.Utf8, "payload")],
                TestContext.Current.CancellationToken));
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "qyl.collector";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private static string ContentRef(string plaintext) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)))}";

    private static Task<WorkflowRunStorageRow> CreateRunAsync(DuckDbStore store, string runId) =>
        store.CreateWorkflowRunAsync(
            new WorkflowRunStorageRow(
                "project-a", runId, "thread-1", "Content security fixture",
                WorkflowRunStatus.Active, s_startedAt, null, 0, null, null),
            TestContext.Current.CancellationToken);

    private static WorkflowEventWrite Event(
        string eventId,
        ulong sourceSequence,
        WorkflowJournalEventKind kind,
        string attemptId,
        IReadOnlyList<string> contentRefs) =>
        new(
            eventId, sourceSequence, s_startedAt, kind, "thread-1", null, attemptId,
            null, null, null, null, contentRefs, null);
}
