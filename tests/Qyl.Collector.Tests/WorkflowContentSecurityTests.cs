using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Tests;

public sealed class WorkflowContentSecurityTests
{
    private static readonly DateTimeOffset s_startedAt =
        new(2026, 7, 28, 8, 0, 0, TimeSpan.Zero);

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

    [Fact]
    public async Task A_run_cannot_reference_content_captured_by_another_run()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store, "run-victim");
        await CreateRunAsync(store, "run-attacker");

        const string secret = "AWS_SECRET_ACCESS_KEY=not-actually-a-real-key";
        var secretRef = ContentRef(secret);

        await store.AppendWorkflowEventsAsync(
            "project-a", "run-victim", "observer-1",
            [Event("victim-1", 1, WorkflowJournalEventKind.AttemptStarted, "attempt-1", [secretRef])],
            [new WorkflowContentWrite(secretRef, "text/plain", WorkflowContentEncoding.Utf8, secret)],
            TestContext.Current.CancellationToken);

        var reach = await Assert.ThrowsAsync<WorkflowEventConflictException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a", "run-attacker", "observer-2",
                [Event("attacker-1", 1, WorkflowJournalEventKind.AttemptStarted, "attempt-1", [secretRef])],
                [],
                TestContext.Current.CancellationToken));
        Assert.Contains("has not captured", reach.Message, StringComparison.Ordinal);

        Assert.Null(await store.GetWorkflowContentAsync(
            "project-a", "run-attacker", secretRef, TestContext.Current.CancellationToken));
        Assert.NotNull(await store.GetWorkflowContentAsync(
            "project-a", "run-victim", secretRef, TestContext.Current.CancellationToken));
    }

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
