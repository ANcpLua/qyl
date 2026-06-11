using System.Text.Json.Nodes;

namespace Qyl.Frankenstein;

internal static class AbilityValidator
{
    private static readonly string[] RequiredFields =
    [
        "id",
        "element",
        "name",
        "badForm",
        "goodForm",
        "rule",
        "workflowEffect"
    ];

    private static readonly HashSet<string> GenericLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "ability",
        "skill",
        "trait",
        "todo",
        "tbd",
        "n/a",
        "none"
    };

    public static IReadOnlyList<string> Validate(JsonArray? abilities, TargetAdapter target)
    {
        var errors = new List<string>();
        if (abilities is null)
        {
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is not JsonObject ability)
            {
                errors.Add($"abilities[{index}] must be an object");
                continue;
            }

            foreach (var field in RequiredFields)
            {
                var value = ability[field]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"abilities[{index}].{field} is required");
                }
            }

            var id = ability["id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
            {
                errors.Add($"duplicate ability id: {id}");
            }

            foreach (var labelField in new[] { "element", "name" })
            {
                var label = ability[labelField]?.GetValue<string>();
                if (label is not null && GenericLabels.Contains(label.Trim()))
                {
                    errors.Add($"abilities[{index}].{labelField} is too generic: {label}");
                }
            }

            var name = ability["name"]?.GetValue<string>() ?? string.Empty;
            if (name.Contains("pokemon", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("pokémon", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"abilities[{index}].name must not copy Pokémon naming");
            }

            var goodForm = ability["goodForm"]?.GetValue<string>() ?? string.Empty;
            var badForm = ability["badForm"]?.GetValue<string>() ?? string.Empty;
            if (badForm.Length > 0 && goodForm.Length is 0)
            {
                errors.Add($"abilities[{index}] is negative-only; goodForm is required");
            }
        }

        if (!target.SupportsRootAbilities && abilities.Count > 0)
        {
            // This is not an error for normalized data; the target adapter must move it safely.
        }

        return errors;
    }
}
