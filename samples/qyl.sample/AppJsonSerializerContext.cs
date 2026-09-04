using System.Text.Json.Serialization;

namespace Qyl.Sample;

/// <summary>Every JSON shape that crosses the HTTP boundary of this API, resolved at compile time.</summary>
[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(Todo))]
[JsonSerializable(typeof(CreateTodoRequest))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
