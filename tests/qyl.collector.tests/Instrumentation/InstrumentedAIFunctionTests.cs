using System.Diagnostics;
using Microsoft.Extensions.AI;
using qyl.contracts.Attributes;
using Qyl.Instrumentation.Instrumentation;
using Qyl.Instrumentation.Instrumentation.GenAi;
using Xunit;

namespace Qyl.Collector.Tests.Instrumentation;

/// <summary>
///     Tests for <see cref="InstrumentedAIFunction" /> and
///     <see cref="ChatClientExtensions.AddInstrumentedTools" />.
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

        Assert.True(invoked);
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

        Assert.Single(captured);
        var activity = captured[0];
        Assert.Equal(
            $"{GenAiAttributes.Operations.ExecuteTool} greet_tool",
            activity.DisplayName);
        Assert.Equal(
            GenAiAttributes.Operations.ExecuteTool,
            activity.GetTagItem(GenAiAttributes.OperationName));
        Assert.Equal("greet_tool", activity.GetTagItem(GenAiAttributes.ToolName));
    }

    // -- idempotent wrapping --------------------------------------------------

    [Fact]
    public void InstrumentedAIFunction_is_not_double_wrapped_by_AddInstrumentedTools()
    {
        var original = AIFunctionFactory.Create(static () => 0, "counter");
        var wrapped = new InstrumentedAIFunction(original);

        var options = new ChatOptions { Tools = [wrapped] };
        options.AddInstrumentedTools();

        Assert.Single(options.Tools!);
        // The exact same instance must be returned — no outer InstrumentedAIFunction
        Assert.Same(wrapped, options.Tools![0]);
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

        Assert.Equal(3, options.Tools!.Count);
        Assert.All(options.Tools, t => Assert.IsType<InstrumentedAIFunction>(t));
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await instrumented.InvokeAsync([], TestContext.Current.CancellationToken));

        Assert.Single(captured);
        Assert.Equal(ActivityStatusCode.Error, captured[0].Status);
    }
}
