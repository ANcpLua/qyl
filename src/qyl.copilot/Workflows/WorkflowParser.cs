// =============================================================================
// qyl.copilot - Workflow Parser
// Parses .qyl/workflows/*.md files with YAML frontmatter
// Extracts workflow definitions for execution
// =============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using qyl.protocol.Copilot;

namespace qyl.copilot.Workflows;

/// <summary>
///     Parses workflow definition files from .qyl/workflows/*.md format.
/// </summary>
public static partial class WorkflowParser
{
    /// <summary>
    ///     Regex pattern for YAML frontmatter extraction.
    /// </summary>
    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    /// <summary>
    ///     Regex pattern for YAML key-value pairs (simple parser).
    /// </summary>
    [GeneratedRegex(@"^(\w+):\s*(.*)$", RegexOptions.Multiline)]
    private static partial Regex YamlKeyValueRegex();

    /// <summary>
    ///     Regex pattern for YAML array items.
    /// </summary>
    [GeneratedRegex(@"^\s*-\s*['""]?([^'""]+)['""]?\s*$", RegexOptions.Multiline)]
    private static partial Regex YamlArrayItemRegex();

    /// <summary>
    ///     Parses a workflow definition from markdown content.
    /// </summary>
    /// <param name="content">The markdown content with YAML frontmatter.</param>
    /// <param name="filePath">Optional file path for error messages.</param>
    /// <returns>Parsed workflow definition.</returns>
    public static CopilotWorkflow Parse(string content, string? filePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var match = FrontmatterRegex().Match(content);

        if (!match.Success)
        {
            // No frontmatter - use content as instructions, derive name from filename
            var name = filePath is not null
                ? Path.GetFileNameWithoutExtension(filePath)
                : "unnamed-workflow";

            return new CopilotWorkflow { Name = name, Instructions = content.Trim(), FilePath = filePath };
        }

        var frontmatter = match.Groups[1].Value;
        var body = match.Groups[2].Value.Trim();

        var yaml = ParseYaml(frontmatter);

        // Extract workflow properties
        var workflowName = yaml.TryGetValue("name", out var n) ? n : GetDefaultName(filePath);
        var description = yaml.TryGetValue("description", out var d) ? d : null;
        var schedule = yaml.TryGetValue("schedule", out var s) ? s : null;

        // Parse trigger
        var trigger = WorkflowTrigger.Manual;
        if (yaml.TryGetValue("trigger", out var triggerStr))
        {
            trigger = triggerStr.ToUpperInvariant() switch
            {
                "MANUAL" => WorkflowTrigger.Manual,
                "SCHEDULED" or "CRON" => WorkflowTrigger.Scheduled,
                "EVENT" => WorkflowTrigger.Event,
                "WEBHOOK" => WorkflowTrigger.Webhook,
                _ => WorkflowTrigger.Manual
            };
        }

        // Parse tools list
        IReadOnlyList<string> tools = [];
        if (yaml.TryGetValue("tools", out var toolsStr))
        {
            tools = ParseYamlArray(toolsStr);
        }

        // Build metadata from remaining YAML keys
        var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name",
            "description",
            "tools",
            "trigger",
            "schedule"
        };

        var metadata = yaml
            .Where(kvp => !knownKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        return new CopilotWorkflow
        {
            Name = workflowName,
            Description = description,
            Tools = tools,
            Trigger = trigger,
            Instructions = body,
            FilePath = filePath,
            Schedule = schedule,
            Metadata = metadata.Count > 0 ? metadata : null
        };
    }

    /// <summary>
    ///     Parses a workflow from a file path.
    /// </summary>
    public static async Task<CopilotWorkflow> ParseFileAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct).ConfigureAwait(false);
        return Parse(content, filePath);
    }

    /// <summary>
    ///     Discovers and parses all workflows in a directory.
    /// </summary>
    /// <param name="directoryPath">Directory containing workflow files.</param>
    /// <param name="pattern">File pattern (default: *.md).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of parsed workflows.</returns>
    public static async Task<IReadOnlyList<CopilotWorkflow>> DiscoverWorkflowsAsync(
        string directoryPath,
        string pattern = "*.md",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        var files = Directory.GetFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly);
        var workflows = new List<CopilotWorkflow>(files.Length);

        foreach (var file in files)
        {
            try
            {
                var workflow = await ParseFileAsync(file, ct).ConfigureAwait(false);
                workflows.Add(workflow);
            }
            catch (Exception ex)
            {
                // Log but continue with other files
                Debug.WriteLine($"Failed to parse workflow {file}: {ex.Message}");
            }
        }

        return workflows;
    }

    /// <summary>
    ///     Gets the default qyl workflows directory.
    /// </summary>
    public static string GetDefaultWorkflowsDirectory(string? basePath = null)
    {
        var root = basePath ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, ".qyl", "workflows");
    }

    /// <summary>
    ///     Validates a workflow definition.
    /// </summary>
    public static IReadOnlyList<string> Validate(CopilotWorkflow workflow)
    {
        Throw.IfNull(workflow);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            errors.Add("Workflow name is required.");
        }

        if (string.IsNullOrWhiteSpace(workflow.Instructions))
        {
            errors.Add("Workflow instructions are required.");
        }

        if (workflow.Trigger == WorkflowTrigger.Scheduled && string.IsNullOrWhiteSpace(workflow.Schedule))
        {
            errors.Add("Scheduled workflows require a schedule (cron expression).");
        }

        return errors;
    }

    // =========================================================================
    // YAML parsing helpers (simple, no external dependencies)
    // =========================================================================

    private static Dictionary<string, string> ParseYaml(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = yaml.Split('\n');
        string? currentKey = null;
        var currentValue = new StringBuilder();
        var inArray = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Check for new key-value pair
            var kvMatch = YamlKeyValueRegex().Match(line);
            if (kvMatch.Success && !line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                // Save previous key-value
                if (currentKey is not null)
                {
                    result[currentKey] = currentValue.ToString().Trim();
                }

                currentKey = kvMatch.Groups[1].Value;
                var value = kvMatch.Groups[2].Value.Trim();

                currentValue.Clear();

                // Check if this is an array start
                if (value.StartsWith('[') && value.EndsWith(']'))
                {
                    // Inline array: [item1, item2]
                    currentValue.Append(value);
                    inArray = false;
                }
                else if (string.IsNullOrEmpty(value) || value == "|" || value == ">")
                {
                    // Multi-line value or array follows
                    inArray = true;
                }
                else
                {
                    // Simple value
                    currentValue.Append(value.Trim('"', '\''));
                    inArray = false;
                }
            }
            else if (currentKey is not null && (line.StartsWith(' ') || line.StartsWith('\t') || inArray))
            {
                // Continuation of previous value
                if (currentValue.Length > 0)
                {
                    currentValue.Append('\n');
                }

                currentValue.Append(line.TrimStart());
            }
        }

        // Save last key-value
        if (currentKey is not null)
        {
            result[currentKey] = currentValue.ToString().Trim();
        }

        return result;
    }

    private static IReadOnlyList<string> ParseYamlArray(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        // Handle inline JSON-style array: ['item1', 'item2']
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            try
            {
                return JsonSerializer.Deserialize(value, CopilotJsonContext.Default.StringArray) ?? [];
            }
            catch
            {
                // Try simple comma-separated parse
                var inner = value[1..^1];
                return inner
                    .Split(',')
                    .Select(static s => s.Trim().Trim('"', '\''))
                    .Where(static s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
        }

        // Handle YAML-style array:
        // - item1
        // - item2
        var matches = YamlArrayItemRegex().Matches(value);
        if (matches.Count > 0)
        {
            return matches
                .Select(static m => m.Groups[1].Value.Trim())
                .Where(static s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        // Single item
        return [value.Trim('"', '\'')];
    }

    private static string GetDefaultName(string? filePath)
    {
        if (filePath is null) return "unnamed-workflow";
        return Path.GetFileNameWithoutExtension(filePath);
    }
}
