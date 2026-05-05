using System.Collections.Frozen;
using System.Text.Json;

namespace qyl.mcp.Scoping;

/// <summary>
///     Auto-injects scoped parameters into MCP tool call arguments.
///     When qyl.mcp is scoped (via <see cref="QylScope" />), matching tool parameters
///     are injected silently so the agent doesn't need to specify them on every call.
/// </summary>
internal static class ConstraintInjector
{
    private static readonly FrozenDictionary<string, Func<QylScope, string?>> s_injectableParameters =
        new Dictionary<string, Func<QylScope, string?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = static s => s.ServiceName, ["sessionId"] = static s => s.SessionId
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static IDictionary<string, JsonElement>? InjectScope(
        IDictionary<string, JsonElement>? arguments,
        QylScope scope)
    {
        if (!scope.HasScope)
            return arguments;

        var dict = arguments ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var (paramName, accessor) in s_injectableParameters)
        {
            var value = accessor(scope);
            if (value is null)
                continue;

            // Only inject if not already provided with a non-empty value
            if (dict.TryGetValue(paramName, out var existing)
                && existing.ValueKind is JsonValueKind.String
                && !string.IsNullOrEmpty(existing.GetString()))
                continue;

            dict[paramName] = JsonSerializer.SerializeToElement(value);
        }

        return dict;
    }
}
