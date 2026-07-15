using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Qyl.Host.Mcp;

namespace Qyl.Host.Tests;

public sealed class McpSdkJsonTests
{
    [Fact]
    public void Official_passthrough_types_have_sdk_source_generated_metadata()
    {
        Assert.NotNull(McpSdkJson.TypeInfo<ListToolsResult>());
        Assert.NotNull(McpSdkJson.TypeInfo<CallToolRequestParams>());
        Assert.NotNull(McpSdkJson.TypeInfo<CallToolResult>());
        Assert.NotNull(McpSdkJson.TypeInfo<ReadResourceRequestParams>());
        Assert.NotNull(McpSdkJson.TypeInfo<ReadResourceResult>());
    }

    [Fact]
    public void Required_official_request_members_are_rejected_by_sdk_deserialization()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            "{}"u8,
            McpSdkJson.TypeInfo<CallToolRequestParams>()));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            "{}"u8,
            McpSdkJson.TypeInfo<ReadResourceRequestParams>()));
    }

    [Fact]
    public void Official_list_and_call_protocol_objects_round_trip_without_a_qyl_dto_layer()
    {
        var inputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { count = new { type = "integer" } }
        });
        var list = RoundTrip(new ListToolsResult
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
        });

        var tool = Assert.Single(list.Tools);
        Assert.Equal("next-page", list.NextCursor);
        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
        Assert.Equal(ToolTaskSupport.Optional, tool.Execution?.TaskSupport);
        Assert.True(tool.Annotations?.ReadOnlyHint);
        Assert.Equal("https://example.test/icon.png", Assert.Single(tool.Icons!).Source);
        Assert.Equal("qyl", tool.Meta?["owner"]?.GetValue<string>());

        var request = RoundTrip(new CallToolRequestParams
        {
            Name = tool.Name,
            Arguments = new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement(3),
                ["optional"] = JsonSerializer.SerializeToElement<object?>(null)
            },
            Meta = new JsonObject { ["progressToken"] = "progress-1" }
        });

        Assert.Equal(3, request.Arguments?["count"].GetInt32());
        Assert.Equal(JsonValueKind.Null, request.Arguments?["optional"].ValueKind);
        Assert.Equal("progress-1", request.Meta?["progressToken"]?.GetValue<string>());

        byte[] binary = [0xff, 0x00, 0x80, 0xfe];
        var result = RoundTrip(new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = "complete" },
                ImageContentBlock.FromBytes(binary, "image/png"),
                new EmbeddedResourceBlock
                {
                    Resource = new TextResourceContents
                    {
                        Uri = "qyl://trace/abc",
                        MimeType = "application/json",
                        Text = "{\"ok\":true}"
                    }
                }
            ],
            StructuredContent = JsonSerializer.SerializeToElement(new { accepted = true }),
            IsError = false,
            Meta = new JsonObject { ["result"] = "live" }
        });

        Assert.False(result.IsError);
        Assert.Equal("complete", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.Equal(binary, Assert.IsType<ImageContentBlock>(result.Content[1]).DecodedData.ToArray());
        Assert.Equal(
            "{\"ok\":true}",
            Assert.IsType<TextResourceContents>(
                Assert.IsType<EmbeddedResourceBlock>(result.Content[2]).Resource).Text);
        Assert.Equal("live", result.Meta?["result"]?.GetValue<string>());
    }

    [Fact]
    public void Official_resource_protocol_objects_round_trip_without_a_qyl_dto_layer()
    {
        var request = RoundTrip(new ReadResourceRequestParams
        {
            Uri = "qyl://blob/1",
            Meta = new JsonObject { ["request"] = "live" }
        });
        Assert.Equal("qyl://blob/1", request.Uri);

        byte[] binary = [0xff, 0x00, 0x80, 0xfe];
        var result = RoundTrip(new ReadResourceResult
        {
            Contents =
            [
                BlobResourceContents.FromBytes(binary, "qyl://blob/1", "application/octet-stream")
            ],
            Meta = new JsonObject { ["source"] = "server" }
        });

        var blob = Assert.IsType<BlobResourceContents>(Assert.Single(result.Contents));
        Assert.Equal(binary, blob.DecodedData.ToArray());
        Assert.Equal("qyl://blob/1", blob.Uri);
        Assert.Equal("server", result.Meta?["source"]?.GetValue<string>());
    }

    private static T RoundTrip<T>(T value)
    {
        var jsonTypeInfo = McpSdkJson.TypeInfo<T>();
        return JsonSerializer.Deserialize(McpSdkJson.Serialize(value), jsonTypeInfo)
               ?? throw new InvalidOperationException($"{typeof(T).Name} deserialized to null.");
    }
}
