using System.Diagnostics;
using AwesomeAssertions;
using Xunit;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;

namespace Qyl.Collector.Tests.Instrumentation;

/// <summary>
///     Tests for <see cref="InstrumentedAIFunction" /> and the <c>AddInstrumentedTools</c>
///     extension method on <see cref="ChatOptions" />.
/// </summary>
public sealed class InstrumentedAIFunctionTests
{
    // -- helper ----------------------------------------------------------------

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

    // -- delegation -----------------------------------------------------------

    [Fact]
    public async Task InstrumentedAIFunction_delegates_InvokeAsync_to_inner()
    {
        var invoked = false;
        var recordingInner = AIFunctionFactory.Create(
            () =>
            {
                invoked = true;
                return "result";
            },
            new AIFunctionFactoryOptions { Name = "recording_tool" });

        var instrumented = new InstrumentedAIFunction(recordingInner);

        await instrumented.InvokeAsync([], TestContext.Current.CancellationToken);

        invoked.Should().BeTrue();
    }

    // -- span creation --------------------------------------------------------

    [Fact]
    public async Task InstrumentedAIFunction_creates_execute_tool_span()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = AIFunctionFactory.Create(
            static () => "tool result",
            new AIFunctionFactoryOptions { Name = "greet_tool" });

        var instrumented = new InstrumentedAIFunction(inner);

        await instrumented.InvokeAsync([], TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        var activity = captured[0];
        activity.DisplayName.Should().Be($"{GenAiAttributes.Operations.ExecuteTool} greet_tool");
        activity.GetTagItem(GenAiAttributes.OperationName).Should().Be(GenAiAttributes.Operations.ExecuteTool);
        activity.GetTagItem(GenAiAttributes.ToolName).Should().Be("greet_tool");
    }

    // -- idempotent wrapping --------------------------------------------------

    [Fact]
    public void InstrumentedAIFunction_is_not_double_wrapped_by_AddInstrumentedTools()
    {
        var original = AIFunctionFactory.Create(static () => 0, "counter");
        var wrapped = new InstrumentedAIFunction(original);

        var options = new ChatOptions { Tools = [wrapped] };
        options.AddInstrumentedTools();

        options.Tools!.Should().ContainSingle();
        // The exact same instance must be returned — no outer InstrumentedAIFunction
        options.Tools![0].Should().BeSameAs(wrapped);
    }

    // -- AddInstrumentedTools extension ---------------------------------------

    [Fact]
    public void AddInstrumentedTools_wraps_all_functions_in_ChatOptions()
    {
        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(static () => 1, "fn_one"),
                AIFunctionFactory.Create(static () => 2, "fn_two"),
                AIFunctionFactory.Create(static () => 3, "fn_three")
            ]
        };

        options.AddInstrumentedTools();

        options.Tools!.Count.Should().Be(3);
        options.Tools.Should().AllSatisfy(t => t.Should().BeOfType<InstrumentedAIFunction>());
    }

    // -- error span status ----------------------------------------------------

    [Fact]
    public async Task InstrumentedAIFunction_sets_error_status_on_exception()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var inner = AIFunctionFactory.Create(
            static string () => throw new InvalidOperationException("boom"),
            new AIFunctionFactoryOptions { Name = "failing_tool" });

        var instrumented = new InstrumentedAIFunction(inner);

        var act = async () => await instrumented.InvokeAsync([], TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        captured.Should().ContainSingle();
        captured[0].Status.Should().Be(ActivityStatusCode.Error);
    }
}
