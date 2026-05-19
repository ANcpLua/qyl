using System.Collections.Frozen;
using System.Text.Json;
using ANcpLua.Agents.Mcp;

namespace qyl.mcp.Scoping;

internal sealed class QylScopeInjector : IQylConstraintInjector<QylScope>
{
    private static readonly FrozenDictionary<string, Func<QylScope, string?>> s_injectableParameters =
        new Dictionary<string, Func<QylScope, string?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["serviceName"] = static s => s.ServiceName,
            ["sessionId"] = static s => s.SessionId
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, JsonElement>? Inject(
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

            if (dict.TryGetValue(paramName, out var existing)
                && existing.ValueKind is JsonValueKind.String
                && !string.IsNullOrEmpty(existing.GetString()))
                continue;

            dict[paramName] = JsonSerializer.SerializeToElement(value);
        }

        return dict;
    }
}
