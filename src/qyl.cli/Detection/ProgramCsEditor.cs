namespace qyl.cli.Detection;

/// <summary>
///     Safe Program.cs text manipulation — injects builder.AddServiceDefaults() call.
/// </summary>
public sealed class ProgramCsEditor
{
    private readonly string _path;
    private string _content;

    public ProgramCsEditor(string path)
    {
        _path = path;
        _content = File.ReadAllText(path);
    }

    /// <summary>
    ///     Returns true if AddServiceDefaults() is not already present and a builder pattern is detected.
    /// </summary>
    public bool NeedsServiceDefaults()
    {
        if (_content.ContainsOrdinal("AddServiceDefaults"))
        {
            return false;
        }

        return HasBuilderPattern();
    }

    /// <summary>
    ///     Injects builder.AddServiceDefaults() after the builder creation line.
    /// </summary>
    public void InjectServiceDefaults()
    {
        if (_content.ContainsOrdinal("AddServiceDefaults"))
        {
            return;
        }

        // Match common patterns: WebApplication.CreateBuilder, WebApplication.CreateSlimBuilder, Host.CreateApplicationBuilder
        string[] builderPatterns =
        [
            "WebApplication.CreateBuilder",
            "WebApplication.CreateSlimBuilder",
            "Host.CreateApplicationBuilder",
            "Host.CreateDefaultBuilder"
        ];

        var lines = _content.Split('\n');
        var insertIndex = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var pattern in builderPatterns)
            {
                if (lines[i].ContainsOrdinal(pattern))
                {
                    // Handle multi-line builder calls: find the line with the semicolon
                    insertIndex = i;
                    for (var j = i; j < lines.Length; j++)
                    {
                        if (lines[j].ContainsOrdinal(";"))
                        {
                            insertIndex = j;
                            break;
                        }
                    }

                    break;
                }
            }

            if (insertIndex >= 0)
            {
                break;
            }
        }

        if (insertIndex < 0)
        {
            return;
        }

        // Detect indentation from the builder line
        var builderLine = lines[insertIndex];
        var indent = "";
        foreach (var ch in builderLine)
        {
            if (ch is ' ' or '\t')
            {
                indent += ch;
            }
            else
            {
                break;
            }
        }

        // Detect the variable name (var builder = ... or var app = ...)
        var varName = DetectBuilderVariableName(builderLine);

        var injection = $"{indent}{varName}.AddServiceDefaults();";

        var newLines = new List<string>(lines.Length + 1);
        for (var i = 0; i <= insertIndex; i++)
        {
            newLines.Add(lines[i]);
        }

        newLines.Add(injection);
        for (var i = insertIndex + 1; i < lines.Length; i++)
        {
            newLines.Add(lines[i]);
        }

        _content = string.Join('\n', newLines);
        File.WriteAllText(_path, _content);
    }

    private bool HasBuilderPattern() =>
        _content.ContainsOrdinal("WebApplication.CreateBuilder")
        || _content.ContainsOrdinal("WebApplication.CreateSlimBuilder")
        || _content.ContainsOrdinal("Host.CreateApplicationBuilder")
        || _content.ContainsOrdinal("Host.CreateDefaultBuilder");

    private static string DetectBuilderVariableName(string line)
    {
        // Match patterns like "var builder = ..." or "var app = ..."
        // Split: ["var", "builder", "=", "WebApplication.CreateBuilder(args);"]
        var trimmed = line.TrimStart();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i + 1] == "=")
            {
                return parts[i];
            }
        }

        return "builder";
    }
}
