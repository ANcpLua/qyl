using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qyl.Frankenstein;

internal sealed class FrankensteinCli
{
    private readonly string _workingDirectory;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly PetPipeline _pipeline;

    private FrankensteinCli(string workingDirectory, TextWriter output, TextWriter error)
    {
        _workingDirectory = workingDirectory;
        _output = output;
        _error = error;
        _pipeline = new PetPipeline(workingDirectory);
    }

    public static int Run(string[] args, string workingDirectory, TextWriter output, TextWriter error)
    {
        var cli = new FrankensteinCli(workingDirectory, output, error);

        try
        {
            return cli.Run(ParsedArguments.Parse(args));
        }
        catch (FrankensteinException exception)
        {
            error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
        catch (IOException exception)
        {
            error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException exception)
        {
            error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
    }

    private int Run(ParsedArguments arguments)
    {
        if (arguments.Command.Length is 0 || arguments.Has("--help") || arguments.Has("-h"))
        {
            WriteUsage();
            return arguments.Command.Length is 0 ? 1 : 0;
        }

        return arguments.Command switch
        {
            "doctor" => Doctor(arguments),
            "plan" => Plan(arguments),
            "repair" => Repair(arguments),
            "validate" => Validate(arguments),
            "import" => Import(arguments),
            "export" => Export(arguments),
            "diff-normalized" => DiffNormalized(arguments),
            "roundtrip" => RoundTrip(arguments),
            "abilities" => Abilities(arguments),
            "inspect-atlas" => InspectAtlas(arguments),
            "quarantine" => Quarantine(arguments),
            "report" => Report(arguments),
            _ => UnknownCommand(arguments.Command)
        };
    }

    private int Doctor(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var target = arguments.Value("--target") ?? "codex";
        var result = _pipeline.Doctor(source, target);

        if (arguments.Value("--report") is { Length: > 0 } reportPath)
        {
            WriteText(reportPath, ReportWriter.WriteDoctorReport(result));
        }

        if (arguments.Value("--plan") is { Length: > 0 } planPath)
        {
            WriteText(planPath, ReportWriter.WriteRepairPlan(result));
        }

        _output.Write(ReportWriter.WriteDoctorConsole(result));
        return 0;
    }

    private int Plan(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var target = arguments.Value("--target") ?? "codex";
        var result = _pipeline.Doctor(source, target);
        var plan = ReportWriter.WriteRepairPlan(result);

        if (arguments.Value("--out") is { Length: > 0 } outputPath)
        {
            WriteText(outputPath, plan);
        }
        else
        {
            _output.Write(plan);
        }

        return result.Repairable ? 0 : 1;
    }

    private int Repair(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var target = arguments.Value("--target") ?? "codex";
        var planPath = RequiredOption(arguments, "--plan");
        var outputPath = RequiredOption(arguments, "--out");
        var result = _pipeline.Repair(source, target, planPath, outputPath);

        _output.WriteLine(result.SourceMutated ? "REPAIR: FAILED" : "REPAIR: PASS");
        _output.WriteLine($"source mutated: {(result.SourceMutated ? "yes" : "no")}");
        _output.WriteLine($"output: {result.OutputPath}");
        _output.WriteLine($"manifest: {result.ManifestPath}");
        return result.SourceMutated ? 1 : 0;
    }

    private int Validate(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var target = arguments.Value("--target") ?? "codex";
        var result = _pipeline.Validate(source, target);

        if (arguments.Has("--json"))
        {
            _output.WriteLine(result.ToJson().ToJsonString(JsonFormatter.Options));
        }
        else
        {
            _output.Write(ReportWriter.WriteValidationConsole(result));
        }

        return result.Valid ? 0 : 1;
    }

    private int Import(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var outputPath = RequiredOption(arguments, "--out");
        var normalized = _pipeline.Import(source);
        WriteJson(outputPath, normalized);
        _output.WriteLine("IMPORT: PASS");
        _output.WriteLine($"out: {ResolvePath(outputPath)}");
        return 0;
    }

    private int Export(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path or imported JSON");
        var target = arguments.Value("--target") ?? "codex";
        var outputPath = RequiredOption(arguments, "--out");
        var result = _pipeline.Export(source, target, outputPath);

        _output.WriteLine(result.Valid ? "EXPORT: PASS" : "EXPORT: FAILED");
        _output.WriteLine($"target: {target}");
        _output.WriteLine($"out: {result.OutputPath}");
        return result.Valid ? 0 : 1;
    }

    private int DiffNormalized(ParsedArguments arguments)
    {
        var left = RequiredPositional(arguments, 0, "left package or imported JSON");
        var right = RequiredPositional(arguments, 1, "right package or imported JSON");
        var diff = _pipeline.DiffNormalized(left, right);

        if (diff.IsEmpty)
        {
            _output.WriteLine("normalized diff: empty");
            return 0;
        }

        _output.WriteLine("normalized diff:");
        foreach (var line in diff.Lines)
        {
            _output.WriteLine(line);
        }

        return 1;
    }

    private int RoundTrip(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var target = arguments.Value("--target") ?? "codex";
        var result = _pipeline.RoundTrip(source, target);

        if (arguments.Value("--report") is { Length: > 0 } reportPath)
        {
            WriteText(reportPath, ReportWriter.WriteFinalReport(result));
            ReportWriter.WriteAgentReports(Path.GetDirectoryName(ResolvePath(reportPath))!, result);
        }

        _output.Write(ReportWriter.WriteRoundTripConsole(result));
        return result.Pass ? 0 : 1;
    }

    private int Abilities(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path or imported JSON");
        var target = arguments.Value("--target") ?? "generic-agent";
        var result = _pipeline.CheckAbilities(source, target);

        if (arguments.Has("--json"))
        {
            _output.WriteLine(result.ToJson().ToJsonString(JsonFormatter.Options));
        }
        else
        {
            _output.Write(ReportWriter.WriteAbilitiesConsole(result));
        }

        return result.Valid ? 0 : 1;
    }

    private int InspectAtlas(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var result = _pipeline.InspectAtlas(source);
        _output.Write(ReportWriter.WriteAtlasConsole(result, arguments.Has("--states")));
        return result.Valid ? 0 : 1;
    }

    private int Quarantine(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var outputPath = RequiredOption(arguments, "--out");
        var result = _pipeline.Quarantine(source, outputPath);
        _output.WriteLine("QUARANTINE: PASS");
        _output.WriteLine($"source: {result.SourcePath}");
        _output.WriteLine($"output: {result.OutputPath}");
        _output.WriteLine($"source hash: {result.SourceHash}");
        return 0;
    }

    private int Report(ParsedArguments arguments)
    {
        var source = RequiredPositional(arguments, 0, "package path");
        var target = arguments.Value("--target") ?? "codex";
        var outputPath = arguments.Value("--out") ?? Path.Combine(_workingDirectory, "reports", "frankenstein-final.md");
        var result = _pipeline.RoundTrip(source, target);
        WriteText(outputPath, ReportWriter.WriteFinalReport(result));
        ReportWriter.WriteAgentReports(Path.GetDirectoryName(ResolvePath(outputPath))!, result);
        _output.WriteLine($"report: {ResolvePath(outputPath)}");
        return result.Pass ? 0 : 1;
    }

    private int UnknownCommand(string command)
    {
        _error.WriteLine($"ERROR: unknown command '{command}'.");
        WriteUsage();
        return 1;
    }

    private void WriteUsage()
    {
        _output.WriteLine("Usage:");
        _output.WriteLine("  frankenstein doctor <package> --target codex [--report <path>] [--plan <path>]");
        _output.WriteLine("  frankenstein plan <package> --target codex --out <path>");
        _output.WriteLine("  frankenstein repair <package> --target codex --plan <path> --out <dir>");
        _output.WriteLine("  frankenstein validate <package> --target codex [--json]");
        _output.WriteLine("  frankenstein import <package> --out <path>");
        _output.WriteLine("  frankenstein export <package-or-json> --target codex --out <dir>");
        _output.WriteLine("  frankenstein roundtrip <package> --target codex [--report <path>]");
        _output.WriteLine("  frankenstein diff-normalized <left> <right>");
        _output.WriteLine("  frankenstein abilities <package> --check [--target generic-agent]");
        _output.WriteLine("  frankenstein inspect-atlas <package> --states");
    }

    private static string RequiredPositional(ParsedArguments arguments, int index, string name)
    {
        if (arguments.Positionals.Count > index)
        {
            return arguments.Positionals[index];
        }

        throw new FrankensteinException($"missing required {name}.");
    }

    private static string RequiredOption(ParsedArguments arguments, string name)
    {
        if (arguments.Value(name) is { Length: > 0 } value)
        {
            return value;
        }

        throw new FrankensteinException($"missing required option {name}.");
    }

    private void WriteText(string path, string text)
    {
        var resolved = ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
        File.WriteAllText(resolved, text, TextEncodings.Utf8NoBom);
    }

    private void WriteJson(string path, JsonObject json)
    {
        WriteText(path, json.ToJsonString(JsonFormatter.Options) + Environment.NewLine);
    }

    private string ResolvePath(string path) =>
        Path.GetFullPath(path, _workingDirectory);
}

internal sealed class ParsedArguments
{
    private readonly Dictionary<string, string?> _options;

    private ParsedArguments(string command, List<string> positionals, Dictionary<string, string?> options)
    {
        Command = command;
        Positionals = positionals;
        _options = options;
    }

    public string Command { get; }

    public IReadOnlyList<string> Positionals { get; }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Value(string name) =>
        _options.TryGetValue(name, out var value) ? value : null;

    public static ParsedArguments Parse(string[] args)
    {
        if (args.Length is 0)
        {
            return new ParsedArguments(string.Empty, [], new Dictionary<string, string?>(StringComparer.Ordinal));
        }

        var command = args[0];
        var positionals = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);

        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("-", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            var equalsIndex = token.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex > 0)
            {
                options[token[..equalsIndex]] = token[(equalsIndex + 1)..];
                continue;
            }

            if (index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                options[token] = args[index + 1];
                index++;
            }
            else
            {
                options[token] = "true";
            }
        }

        return new ParsedArguments(command, positionals, options);
    }
}

internal static class JsonFormatter
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string Canonical(JsonNode? node)
    {
        var canonical = Sort(node);
        return canonical?.ToJsonString(Options) ?? "null";
    }

    private static JsonNode? Sort(JsonNode? node)
    {
        return node switch
        {
            JsonObject jsonObject => SortObject(jsonObject),
            JsonArray jsonArray => SortArray(jsonArray),
            JsonValue jsonValue => jsonValue.DeepClone(),
            null => null,
            _ => node.DeepClone()
        };
    }

    private static JsonObject SortObject(JsonObject source)
    {
        var output = new JsonObject();
        foreach (var property in source.OrderBy(static property => property.Key, StringComparer.Ordinal))
        {
            output[property.Key] = Sort(property.Value);
        }

        return output;
    }

    private static JsonArray SortArray(JsonArray source)
    {
        var output = new JsonArray();
        foreach (var item in source)
        {
            output.Add(Sort(item));
        }

        return output;
    }
}

internal static class Invariant
{
    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
