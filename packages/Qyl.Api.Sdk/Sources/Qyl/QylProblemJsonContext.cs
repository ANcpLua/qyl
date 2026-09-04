using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Qyl;

/// <summary>Problem-details shapes a Qyl API can write, resolved at compile time for the reflection-free JSON pipeline.</summary>
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal sealed partial class QylProblemJsonContext : JsonSerializerContext;
