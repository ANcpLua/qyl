using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Qyl.Api.Contracts.Runner.Mcp;
using Qyl.Host.Mcp;

namespace Qyl.Host.Tests;

public sealed class McpContractMapperTests
{
    [Fact]
    public void Invalid_sdk_output_and_relative_contract_input_cannot_cross_the_runner_boundary()
    {
        var invalidOutput = new CallToolResult
        {
            Content =
            [
                new ResourceLinkBlock
                {
                    Uri = "https://example.test/resource",
                    Name = "resource",
                    Size = -1
                }
            ]
        };
        Assert.Throws<InvalidOperationException>(() => McpContractMapper.ToContract(invalidOutput));

        var relativeInput = new RunnerMcpResourceReadRequest { Uri = new Uri("/relative", UriKind.Relative) };
        Assert.Throws<ArgumentException>(() => McpContractMapper.ToSdk(relativeInput));
        Assert.Throws<ArgumentException>(() => McpContractMapper.ToSdk(
            new RunnerMcpResourceReadRequest { Uri = null! }));
        Assert.Throws<ArgumentException>(() => McpContractMapper.ToSdk(
            new RunnerMcpToolCallRequest { Name = null! }));
    }

    [Fact]
    public void Exact_sdk_objects_map_to_generated_runner_contracts_without_protocol_dtos_crossing_http()
    {
        var inputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { count = new { type = "integer" } }
        });
        var sdkTools = new ListToolsResult
        {
            NextCursor = "next-page",
            Meta = new JsonObject { ["catalog"] = "live" },
            Tools =
            [
                new Tool
                {
                    Name = "inspect",
                    Title = "Inspect",
                    Description = "Inspects current state.",
                    InputSchema = inputSchema,
                    Annotations = new ToolAnnotations { ReadOnlyHint = true, IdempotentHint = true },
                    Execution = new ToolExecution { TaskSupport = ToolTaskSupport.Optional },
                    Icons =
                    [
                        new Icon
                        {
                            Source = "https://example.test/icon.png",
                            MimeType = "image/png",
                            Sizes = ["32x32"]
                        }
                    ],
                    Meta = new JsonObject { ["owner"] = "qyl" }
                }
            ]
        };

        var tools = McpContractMapper.ToContract(sdkTools);
        var tool = Assert.Single(tools.Tools);
        Assert.Equal("next-page", tools.NextCursor);
        Assert.Equal("object", Assert.IsType<JsonElement>(tool.InputSchema["type"]).GetString());
        Assert.Equal(RunnerMcpToolTaskSupport.Optional, tool.Execution?.TaskSupport);
        Assert.True(Assert.IsType<JsonElement>(tool.Annotations?["readOnlyHint"]).GetBoolean());
        Assert.Equal("https://example.test/icon.png", Assert.Single(tool.Icons!).Src);
        Assert.Equal("qyl", Assert.IsType<JsonElement>(tool.Metadata?["owner"]).GetString());

        var request = new RunnerMcpToolCallRequest
        {
            Name = tool.Name,
            Arguments = new Dictionary<string, object>
            {
                ["count"] = JsonSerializer.SerializeToElement(3),
                ["optional"] = JsonSerializer.SerializeToElement<object?>(null)
            },
            Task = new RunnerMcpTaskMetadata { Ttl = 15_000 },
            Metadata = new Dictionary<string, object>
            {
                ["progressToken"] = JsonSerializer.SerializeToElement("progress-1")
            }
        };
        var sdkRequest = McpContractMapper.ToSdk(request);
        Assert.Equal(3, sdkRequest.Arguments?["count"].GetInt32());
        Assert.Equal(JsonValueKind.Null, sdkRequest.Arguments?["optional"].ValueKind);
        Assert.Equal(TimeSpan.FromSeconds(15), sdkRequest.Task?.TimeToLive);
        Assert.Equal("progress-1", sdkRequest.Meta?["progressToken"]?.GetValue<string>());

        var now = DateTimeOffset.UtcNow;
        byte[] binary = [0xff, 0x00, 0x80, 0xfe];
        var base64 = Convert.ToBase64String(binary);
        var sdkResult = new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = "complete" },
                ImageContentBlock.FromBytes(binary, "image/png"),
                AudioContentBlock.FromBytes(binary, "audio/wav"),
                new ResourceLinkBlock
                {
                    Uri = "qyl://trace/abc",
                    Name = "trace",
                    Icons = [new Icon { Source = "https://example.test/trace.png" }]
                },
                new EmbeddedResourceBlock
                {
                    Resource = new TextResourceContents
                    {
                        Uri = "qyl://trace/abc",
                        MimeType = "application/json",
                        Text = "{\"ok\":true}"
                    }
                },
                new ToolUseContentBlock
                {
                    Id = "call-1",
                    Name = "inspect",
                    Input = JsonSerializer.SerializeToElement(new { count = 3 })
                },
                new ToolResultContentBlock
                {
                    ToolUseId = "call-1",
                    Content = [new TextContentBlock { Text = "nested" }],
                    StructuredContent = JsonSerializer.SerializeToElement(new { accepted = true }),
                    IsError = false
                }
            ],
            StructuredContent = JsonSerializer.SerializeToElement(new { accepted = true }),
            IsError = false,
            Task = new McpTask
            {
                TaskId = "task-1",
                Status = McpTaskStatus.Working,
                CreatedAt = now,
                LastUpdatedAt = now,
                TimeToLive = TimeSpan.FromMinutes(1),
                PollInterval = TimeSpan.FromSeconds(1)
            },
            Meta = new JsonObject { ["result"] = "live" }
        };

        var result = McpContractMapper.ToContract(sdkResult);
        Assert.False(result.IsError);
        Assert.Equal(7, result.Content.Count);
        Assert.Equal(binary, Assert.IsType<RunnerMcpImageContent>(result.Content[1]).Data.ToArray());
        Assert.Equal(binary, Assert.IsType<RunnerMcpAudioContent>(result.Content[2]).Data.ToArray());
        Assert.Equal(
            "qyl://trace/abc",
            Assert.IsType<RunnerMcpResourceLinkContent>(result.Content[3]).Uri.OriginalString);
        var embedded = Assert.IsType<RunnerMcpEmbeddedResourceContent>(result.Content[4]);
        Assert.Equal("{\"ok\":true}", Assert.IsType<RunnerMcpTextResourceContent>(embedded.Resource).Text);
        Assert.Equal("call-1", Assert.IsType<RunnerMcpToolUseContent>(result.Content[5]).Id);
        var toolResult = Assert.IsType<RunnerMcpToolResultContent>(result.Content[6]);
        Assert.Equal("nested", Assert.IsType<RunnerMcpTextContent>(Assert.Single(toolResult.Content)).Text);
        Assert.Equal(RunnerMcpTaskStatus.Working, result.Task?.Status);
        Assert.Equal(60_000, result.Task?.Ttl);
        Assert.Equal("live", Assert.IsType<JsonElement>(result.Metadata?["result"]).GetString());

        var resources = McpContractMapper.ToContract(new ReadResourceResult
        {
            Contents =
            [
                BlobResourceContents.FromBytes(binary, "qyl://blob/1", "application/octet-stream")
            ],
            Meta = new JsonObject { ["source"] = "server" }
        });
        var blob = Assert.Single(resources.Contents);
        var typedBlob = Assert.IsType<RunnerMcpBlobResourceContent>(blob);
        Assert.Equal(binary, typedBlob.Blob.ToArray());
        Assert.Equal("qyl://blob/1", typedBlob.Uri.OriginalString);
    }
}
