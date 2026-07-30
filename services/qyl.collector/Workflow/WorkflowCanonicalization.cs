using System.Globalization;
using System.Text;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Workflow;

internal static class WorkflowCanonicalization
{
    public static WorkflowRunStorageRow Normalize(WorkflowRunStorageRow run) =>
        run with
        {
            StartedAt = NormalizeTimestamp(run.StartedAt),
            EndedAt = run.EndedAt.HasValue ? NormalizeTimestamp(run.EndedAt.Value) : null,
            MetadataJson = CanonicalizeJson(run.MetadataJson)
        };

    public static WorkflowEventWrite Normalize(WorkflowEventWrite workflowEvent) =>
        workflowEvent with
        {
            Timestamp = NormalizeTimestamp(workflowEvent.Timestamp),
            DataJson = CanonicalizeJson(workflowEvent.DataJson)
        };

    public static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var ticks = value.UtcDateTime.Ticks;
        return new DateTimeOffset(new DateTime(ticks - ticks % 10, DateTimeKind.Utc));
    }

    public static string? CanonicalizeJson(string? json)
    {
        if (json is null)
            return null;
        using var document = JsonDocument.Parse(json);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
            WriteCanonical(writer, document.RootElement);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    public static long MeasureImmutableRunInput(WorkflowRunStorageRow run) =>
        Measure(
            run.ProjectId,
            run.RunId,
            run.ThreadId,
            run.Title,
            run.StartedAt.ToString("O", CultureInfo.InvariantCulture),
            run.MetadataJson);

    public static long MeasureDynamicRunInput(WorkflowRunStorageRow run) =>
        Measure(
            run.Status.ToString(),
            run.EndedAt?.ToString("O", CultureInfo.InvariantCulture),
            run.ActiveAttemptId);

    public static long MeasureEventInput(
        string projectId,
        string runId,
        string clientId,
        WorkflowEventWrite workflowEvent)
    {
        var bytes = Measure(
            projectId,
            runId,
            workflowEvent.EventId,
            clientId,
            workflowEvent.SourceSequence.ToString(CultureInfo.InvariantCulture),
            workflowEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            workflowEvent.Kind.ToString(),
            workflowEvent.ThreadId,
            workflowEvent.TurnId,
            workflowEvent.AttemptId,
            workflowEvent.AgentId,
            workflowEvent.ParentAgentId,
            workflowEvent.ReceiverAgentId,
            workflowEvent.ToolCallId,
            workflowEvent.DataJson);
        foreach (var contentRef in workflowEvent.ContentRefs)
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(contentRef));
        return bytes;
    }

    public static long MeasureEventInput(WorkflowEventStorageRow workflowEvent) =>
        MeasureEventInput(
            workflowEvent.ProjectId,
            workflowEvent.RunId,
            workflowEvent.ClientId,
            new WorkflowEventWrite(
                workflowEvent.EventId,
                workflowEvent.SourceSequence,
                workflowEvent.Timestamp,
                workflowEvent.Kind,
                workflowEvent.ThreadId,
                workflowEvent.TurnId,
                workflowEvent.AttemptId,
                workflowEvent.AgentId,
                workflowEvent.ParentAgentId,
                workflowEvent.ReceiverAgentId,
                workflowEvent.ToolCallId,
                workflowEvent.ContentRefs,
                workflowEvent.DataJson));

    private static long Measure(params string?[] values)
    {
        var bytes = 64L;
        foreach (var value in values)
        {
            if (value is not null)
                bytes = checked(bytes + Encoding.UTF8.GetByteCount(value));
        }
        return bytes;
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(
                             static property => property.Name,
                             StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException("Workflow JSON contains an unsupported value.");
        }
    }
}
