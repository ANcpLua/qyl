// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    schema/generated/openapi.yaml
//     Generated: 2026-01-13T06:07:10.6792080+00:00
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
        Error = 4
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
        ExecuteTool = 7
    }

    /// <summary>Log severity number following OTel specification (1-24)</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityNumber>))]
    public enum SeverityNumber
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "5")]
        _5 = 2,
        [System.Runtime.Serialization.EnumMember(Value = "9")]
        _9 = 3,
        [System.Runtime.Serialization.EnumMember(Value = "13")]
        _13 = 4,
        [System.Runtime.Serialization.EnumMember(Value = "17")]
        _17 = 5,
        [System.Runtime.Serialization.EnumMember(Value = "21")]
        _21 = 6
    }

    /// <summary>Span kind describing the relationship between spans</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanKind>))]
    public enum SpanKind
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        _2 = 2,
        [System.Runtime.Serialization.EnumMember(Value = "3")]
        _3 = 3,
        [System.Runtime.Serialization.EnumMember(Value = "4")]
        _4 = 4,
        [System.Runtime.Serialization.EnumMember(Value = "5")]
        _5 = 5
    }

    /// <summary>Span status code</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StatusCode>))]
    public enum StatusCode
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        _2 = 2
    }

}

