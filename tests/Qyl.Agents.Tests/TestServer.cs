namespace Qyl.Agents.Tests;

using System.ComponentModel;

/// <summary>A calculator MCP server for end-to-end testing.</summary>
[McpServer]
public partial class CalcServer
{
    public int CallCount { get; private set; }

    /// <summary>
    ///     Adds two integers and returns their sum.
    ///     Use this tool to verify the MCP tool-invocation round trip with primitive arguments
    ///     and to count how many times tools are dispatched during end-to-end tests.
    /// </summary>
    [Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
    public int Add([Description("First number")] int a, [Description("Second number")] int b)
    {
        CallCount++;
        return a + b;
    }

    /// <summary>
    ///     Multiplies two integers and returns the product as an awaitable Task.
    ///     Use this tool in integration tests that need to confirm the dispatcher correctly
    ///     handles async tool methods and cancellation tokens over the MCP transport.
    /// </summary>
    [Tool(ReadOnly = ToolHint.True, Idempotent = ToolHint.True)]
    public Task<int> Multiply([Description("First factor")] int a, [Description("Second factor")] int b,
        CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(a * b);
    }

    /// <summary>
    ///     Always throws an InvalidOperationException with the supplied message.
    ///     Use this tool in integration tests to verify that exceptions raised inside a tool
    ///     method are propagated to the MCP client as an error response rather than silently swallowed.
    /// </summary>
    [Tool(Destructive = ToolHint.False)]
    public string Fail(
        [Description("Error message payload that the tool will wrap in the thrown exception")] string message)
    {
        CallCount++;
        throw new InvalidOperationException(message);
    }
}
