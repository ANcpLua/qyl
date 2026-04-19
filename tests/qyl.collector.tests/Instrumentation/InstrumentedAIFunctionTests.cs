using System.Diagnostics;
using ANcpLua.Agents.Instrumentation;
using AwesomeAssertions;
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

    [Fact]
    public async Task WrapTool_creates_execute_tool_span_on_qyl_source()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = AIFunctionFactory.Create(
            static () => "result",
            new AIFunctionFactoryOptions { Name = "greet_tool" });

        var wrapped = GenAiInstrumentation.WrapTool(inner);

        await wrapped.InvokeAsync([], TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        var activity = captured[0];
        activity.OperationName.Should().Be($"{GenAiAttributes.Operations.ExecuteTool} greet_tool");
        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be(GenAiAttributes.Operations.ExecuteTool);
        activity.GetTagItem(GenAiAttributes.ToolName).Should().Be("greet_tool");
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
            new AIFunctionFactoryOptions { Name = "failing_tool" });

        var wrapped = GenAiInstrumentation.WrapTool(inner);

        var act = async () => await wrapped.InvokeAsync([], TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        captured.Should().ContainSingle();
        captured[0].Status.Should().Be(ActivityStatusCode.Error);
    }
}
