// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    schema/generated/openapi.yaml
//     Generated: 2026-01-13T17:07:48.6334300+00:00
//     Enumeration types (OTel 1.38 semconv)
// =============================================================================
// To modify: update TypeSpec in schema/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Enums
{
    /// <summary>GenAI finish reason</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<GenAiFinishReason>))]
    public enum GenAiFinishReason
    {
        [System.Runtime.Serialization.EnumMember(Value = "stop")]
        Stop = 0,
        [System.Runtime.Serialization.EnumMember(Value = "max_tokens")]
        MaxTokens = 1,
        [System.Runtime.Serialization.EnumMember(Value = "tool_calls")]
        ToolCalls = 2,
        [System.Runtime.Serialization.EnumMember(Value = "content_filter")]
        ContentFilter = 3,
        [System.Runtime.Serialization.EnumMember(Value = "error")]
        Error = 4,
    }

    /// <summary>GenAI operation name</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<GenAiOperationName>))]
    public enum GenAiOperationName
    {
        [System.Runtime.Serialization.EnumMember(Value = "chat")]
        Chat = 0,
        [System.Runtime.Serialization.EnumMember(Value = "completion")]
        Completion = 1,
        [System.Runtime.Serialization.EnumMember(Value = "embedding")]
        Embedding = 2,
        [System.Runtime.Serialization.EnumMember(Value = "image_generation")]
        ImageGeneration = 3,
        [System.Runtime.Serialization.EnumMember(Value = "text_to_speech")]
        TextToSpeech = 4,
        [System.Runtime.Serialization.EnumMember(Value = "speech_to_text")]
        SpeechToText = 5,
        [System.Runtime.Serialization.EnumMember(Value = "invoke_agent")]
        InvokeAgent = 6,
        [System.Runtime.Serialization.EnumMember(Value = "execute_tool")]
        ExecuteTool = 7,
    }

    /// <summary>Log severity number following OTel specification (1-24)</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityNumber>))]
    public enum SeverityNumber
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        Unspecified = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        Trace = 1,
        [System.Runtime.Serialization.EnumMember(Value = "5")]
        Debug = 2,
        [System.Runtime.Serialization.EnumMember(Value = "9")]
        Info = 3,
        [System.Runtime.Serialization.EnumMember(Value = "13")]
        Warn = 4,
        [System.Runtime.Serialization.EnumMember(Value = "17")]
        Error = 5,
        [System.Runtime.Serialization.EnumMember(Value = "21")]
        Fatal = 6,
    }

    /// <summary>Span kind describing the relationship between spans</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanKind>))]
    public enum SpanKind
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        Unspecified = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        Internal = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        Server = 2,
        [System.Runtime.Serialization.EnumMember(Value = "3")]
        Client = 3,
        [System.Runtime.Serialization.EnumMember(Value = "4")]
        Producer = 4,
        [System.Runtime.Serialization.EnumMember(Value = "5")]
        Consumer = 5,
    }

    /// <summary>Span status code</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StatusCode>))]
    public enum StatusCode
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        Unset = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        Ok = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        Error = 2,
    }

}

