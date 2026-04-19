using ANcpLua.Agents.Instrumentation;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Xunit;

namespace Qyl.Collector.Tests.Instrumentation;

/// <summary>
///     Verifies that the qyl-specific tool decorator
///     (<see cref="GenAiInstrumentation.WrapTool"/>) wraps every <see cref="AIFunction"/>
///     with a <see cref="TracedAIFunction"/> against the qyl <c>gen_ai</c>
///     <see cref="ActivitySource"/>, emits OTel GenAI semconv 1.40 tags, and is idempotent.
/// </summary>
public sealed class InstrumentedAIFunctionTests
{
    // ActivityListener captures every span on the qyl.genai source — including spans
    // emitted in parallel by sibling tests. Each test below filters the captured list
    // by tool-name to isolate its own activity from cross-test pollution.
    private static ActivityListener CreateListener(List<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == ActivitySources.GenAi,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = captured.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static Activity ForTool(List<Activity> captured, string toolName) =>
        captured.Should().ContainSingle(a => (string?)a.GetTagItem(GenAiAttributes.ToolName) == toolName).Which;

    [Fact]
    public async Task WrapTool_creates_execute_tool_span_on_qyl_source()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = AIFunctionFactory.Create(
            static () => "result",
            new AIFunctionFactoryOptions { Name = "greet_tool_unique_a" });

        var wrapped = GenAiInstrumentation.WrapTool(inner);

        await wrapped.InvokeAsync([], TestContext.Current.CancellationToken);

        var activity = ForTool(captured, "greet_tool_unique_a");
        activity.OperationName.Should().Be($"{GenAiAttributes.Operations.ExecuteTool} greet_tool_unique_a");
        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be(GenAiAttributes.Operations.ExecuteTool);
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public void WrapTool_is_idempotent_for_already_traced_functions()
    {
        var inner = AIFunctionFactory.Create(static () => 0, "counter");
        var first = GenAiInstrumentation.WrapTool(inner);

        var second = GenAiInstrumentation.WrapTool(first);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task WrapTool_sets_error_status_on_exception()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = AIFunctionFactory.Create(
            static string () => throw new InvalidOperationException("boom"),
            new AIFunctionFactoryOptions { Name = "failing_tool_unique_b" });

        var wrapped = GenAiInstrumentation.WrapTool(inner);

        var act = async () => await wrapped.InvokeAsync([], TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        ForTool(captured, "failing_tool_unique_b").Status.Should().Be(ActivityStatusCode.Error);
    }
}
