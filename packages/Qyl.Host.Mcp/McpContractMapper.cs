using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ContractAudioContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpAudioContent;
using ContractBlobResourceContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpBlobResourceContent;
using ContractContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpContent;
using ContractEmbeddedResourceContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpEmbeddedResourceContent;
using ContractIcon = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpIcon;
using ContractImageContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpImageContent;
using ContractReadRequest = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpResourceReadRequest;
using ContractReadResponse = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpResourceReadResponse;
using ContractResourceContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpResourceContent;
using ContractResourceLinkContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpResourceLinkContent;
using ContractTask = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpTask;
using ContractTaskMetadata = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpTaskMetadata;
using ContractTaskStatus = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpTaskStatus;
using ContractTextContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpTextContent;
using ContractTextResourceContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpTextResourceContent;
using ContractTool = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpTool;
using ContractToolCallRequest = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpToolCallRequest;
using ContractToolCallResponse = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpToolCallResponse;
using ContractToolExecution = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpToolExecution;
using ContractToolResultContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpToolResultContent;
using ContractToolsResponse = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpToolsResponse;
using ContractToolTaskSupport = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpToolTaskSupport;
using ContractToolUseContent = Qyl.Api.Contracts.Runner.Mcp.RunnerMcpToolUseContent;

namespace Qyl.Host.Mcp;

internal static class McpContractMapper
{
    internal static ContractToolsResponse ToContract(ListToolsResult result) => new()
    {
        Tools = [.. result.Tools.Select(ToContract)],
        NextCursor = result.NextCursor,
        Metadata = ToRecord(result.Meta)
    };

    internal static CallToolRequestParams ToSdk(ContractToolCallRequest request) => new()
    {
        Name = RequireToolName(request.Name),
        Arguments = ToElements(request.Arguments),
        Task = request.Task is null ? null : ToSdk(request.Task),
        Meta = ToJsonObject(request.Metadata)
    };

    internal static ContractToolCallResponse ToContract(CallToolResult result) => new()
    {
        Content = [.. result.Content.Select(ToContract)],
        StructuredContent = ToRecord(result.StructuredContent),
        IsError = result.IsError ?? false,
        Task = result.Task is null ? null : ToContract(result.Task),
        Metadata = ToRecord(result.Meta)
    };

    internal static ReadResourceRequestParams ToSdk(ContractReadRequest request) => new()
    {
        Uri = RequireAbsoluteUri(request.Uri),
        Meta = ToJsonObject(request.Metadata)
    };

    internal static ContractReadResponse ToContract(ReadResourceResult result) => new()
    {
        Contents = [.. result.Contents.Select(ToContract)],
        Metadata = ToRecord(result.Meta)
    };

    private static ContractTool ToContract(Tool tool) => new()
    {
        Name = tool.Name,
        Title = tool.Title,
        Description = tool.Description,
        InputSchema = ToRequiredRecord(tool.InputSchema),
        OutputSchema = ToRecord(tool.OutputSchema),
        Annotations = ToRecord(tool.Annotations),
        Execution = tool.Execution is null
            ? null
            : new ContractToolExecution { TaskSupport = ToContract(tool.Execution.TaskSupport) },
        Icons = ToContract(tool.Icons),
        Metadata = ToRecord(tool.Meta)
    };

    private static ContractContent ToContract(ContentBlock content) => content switch
    {
        TextContentBlock text => new ContractTextContent
        {
            Text = text.Text,
            Annotations = ToRecord(content.Annotations),
            Metadata = ToRecord(content.Meta)
        },
        ImageContentBlock image => new ContractImageContent
        {
            Data = image.DecodedData,
            MimeType = image.MimeType,
            Annotations = ToRecord(content.Annotations),
            Metadata = ToRecord(content.Meta)
        },
        AudioContentBlock audio => new ContractAudioContent
        {
            Data = audio.DecodedData,
            MimeType = audio.MimeType,
            Annotations = ToRecord(content.Annotations),
            Metadata = ToRecord(content.Meta)
        },
        EmbeddedResourceBlock embedded => new ContractEmbeddedResourceContent
        {
            Resource = ToContract(embedded.Resource),
            Annotations = ToRecord(content.Annotations),
            Metadata = ToRecord(content.Meta)
        },
        ResourceLinkBlock link => new ContractResourceLinkContent
        {
            Uri = ToAbsoluteUri(link.Uri),
            Name = link.Name,
            Title = link.Title,
            Description = link.Description,
            MimeType = link.MimeType,
            Size = RequireNonNegativeSize(link.Size),
            Icons = ToContract(link.Icons),
            Annotations = ToRecord(content.Annotations),
            Metadata = ToRecord(content.Meta)
        },
        ToolUseContentBlock use => new ContractToolUseContent
        {
            Name = use.Name,
            Id = use.Id,
            Input = use.Input.Clone(),
            Annotations = ToRecord(content.Annotations),
            Metadata = ToRecord(content.Meta)
        },
        ToolResultContentBlock toolResult => new ContractToolResultContent
        {
            ToolUseId = toolResult.ToolUseId,
            Content = [.. toolResult.Content.Select(ToContract)],
            StructuredContent = ToRecord(toolResult.StructuredContent),
            IsError = toolResult.IsError,
            Annotations = ToRecord(content.Annotations),
            Metadata = ToRecord(content.Meta)
        },
        _ => throw new NotSupportedException($"MCP content type '{content.GetType().Name}' is not in the pinned runner contract.")
    };

    private static ContractResourceContent ToContract(ResourceContents content) => content switch
    {
        TextResourceContents text => new ContractTextResourceContent
        {
            Uri = ToAbsoluteUri(text.Uri),
            MimeType = text.MimeType,
            Text = text.Text,
            Metadata = ToRecord(text.Meta)
        },
        BlobResourceContents blob => new ContractBlobResourceContent
        {
            Uri = ToAbsoluteUri(blob.Uri),
            MimeType = blob.MimeType,
            Blob = blob.DecodedData,
            Metadata = ToRecord(blob.Meta)
        },
        _ => throw new NotSupportedException($"MCP resource type '{content.GetType().Name}' is not in the pinned runner contract.")
    };

    private static ContractTask ToContract(McpTask task) => new()
    {
        TaskId = task.TaskId,
        Status = task.Status switch
        {
            McpTaskStatus.Working => ContractTaskStatus.Working,
            McpTaskStatus.InputRequired => ContractTaskStatus.InputRequired,
            McpTaskStatus.Completed => ContractTaskStatus.Completed,
            McpTaskStatus.Failed => ContractTaskStatus.Failed,
            McpTaskStatus.Cancelled => ContractTaskStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(task), task.Status, "Unknown MCP task status")
        },
        StatusMessage = task.StatusMessage,
        CreatedAt = task.CreatedAt,
        LastUpdatedAt = task.LastUpdatedAt,
        Ttl = task.TimeToLive?.TotalMilliseconds,
        PollInterval = task.PollInterval?.TotalMilliseconds
    };

    private static McpTaskMetadata ToSdk(ContractTaskMetadata metadata) => new()
    {
        TimeToLive = metadata.Ttl is null ? null : TimeSpan.FromMilliseconds(metadata.Ttl.Value)
    };

    private static ContractToolTaskSupport? ToContract(ToolTaskSupport? support) => support switch
    {
        ToolTaskSupport.Forbidden => ContractToolTaskSupport.Forbidden,
        ToolTaskSupport.Optional => ContractToolTaskSupport.Optional,
        ToolTaskSupport.Required => ContractToolTaskSupport.Required,
        null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(support), support, "Unknown task-support value")
    };

    private static IReadOnlyList<ContractIcon>? ToContract(IList<Icon>? icons) => icons is null
        ? null
        : [.. icons.Select(static icon => new ContractIcon
        {
            Src = icon.Source,
            MimeType = icon.MimeType,
            Sizes = icon.Sizes is null ? null : [.. icon.Sizes],
            Theme = icon.Theme
        })];

    private static IReadOnlyDictionary<string, object>? ToRecord(ToolAnnotations? annotations)
    {
        if (annotations is null) return null;
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        Add(values, "title", annotations.Title);
        Add(values, "destructiveHint", annotations.DestructiveHint);
        Add(values, "idempotentHint", annotations.IdempotentHint);
        Add(values, "openWorldHint", annotations.OpenWorldHint);
        Add(values, "readOnlyHint", annotations.ReadOnlyHint);
        return values;
    }

    private static IReadOnlyDictionary<string, object>? ToRecord(Annotations? annotations)
    {
        if (annotations is null) return null;
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        if (annotations.Audience is not null)
            values["audience"] = JsonSerializer.SerializeToElement(
                annotations.Audience.Select(static role => role switch
                {
                    Role.User => "user",
                    Role.Assistant => "assistant",
                    _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown MCP audience role")
                }).ToArray(),
                McpJsonUtilities.DefaultOptions);
        Add(values, "priority", annotations.Priority);
        Add(values, "lastModified", annotations.LastModified?.ToString("O", CultureInfo.InvariantCulture));
        return values;
    }

    private static IReadOnlyDictionary<string, object>? ToRecord(JsonObject? value)
    {
        if (value is null) return null;
        using var document = JsonDocument.Parse(value.ToJsonString());
        return ToRequiredRecord(document.RootElement);
    }

    private static IReadOnlyDictionary<string, object>? ToRecord(JsonElement? value)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        return ToRequiredRecord(value.Value);
    }

    private static IReadOnlyDictionary<string, object> ToRequiredRecord(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Object)
            throw new InvalidOperationException("The runner contract accepts an MCP record only when the SDK value is a JSON object.");

        return value.EnumerateObject().ToDictionary(
            static property => property.Name,
            static property => (object)property.Value.Clone(),
            StringComparer.Ordinal);
    }

    private static IDictionary<string, JsonElement>? ToElements(IReadOnlyDictionary<string, object>? values) =>
        values?.ToDictionary(
            static pair => pair.Key,
            static pair => ToElement(pair.Value),
            StringComparer.Ordinal);

    private static JsonObject? ToJsonObject(IReadOnlyDictionary<string, object>? values)
    {
        if (values is null) return null;
        var result = new JsonObject();
        foreach (var (key, value) in values)
            result[key] = JsonNode.Parse(ToElement(value).GetRawText());
        return result;
    }

    private static JsonElement ToElement(object? value) => value switch
    {
        null => JsonSerializer.SerializeToElement<object?>(null, McpJsonUtilities.DefaultOptions),
        JsonElement element => element.Clone(),
        JsonNode node => JsonSerializer.SerializeToElement(node, McpJsonUtilities.DefaultOptions),
        _ => JsonSerializer.SerializeToElement(value, value.GetType(), McpJsonUtilities.DefaultOptions)
    };

    private static Uri ToAbsoluteUri(string value) => new(value, UriKind.Absolute);

    private static string RequireAbsoluteUri(Uri? value) => value?.IsAbsoluteUri == true
        ? value.OriginalString
        : throw new ArgumentException("The MCP resource URI must be absolute.", nameof(value));

    private static string RequireToolName(string? value) => !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException("The MCP tool name is required.", nameof(value));

    private static long? RequireNonNegativeSize(long? value) => value is null or >= 0
        ? value
        : throw new InvalidOperationException("The MCP resource size cannot be negative.");

    private static void Add<T>(Dictionary<string, object> values, string name, T? value)
    {
        if (value is not null) values[name] = ToElement(value);
    }
}
