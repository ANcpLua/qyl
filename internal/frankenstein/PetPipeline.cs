using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qyl.Frankenstein;

internal sealed class PetPipeline
{
    private const string PetJsonFileName = "pet.json";
    private const string RepairManifestFileName = "frankenstein-repair-manifest.json";
    private readonly string _workingDirectory;

    public PetPipeline(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    public DoctorResult Doctor(string sourcePath, string target)
    {
        var source = ResolvePath(sourcePath);
        var targetAdapter = TargetAdapter.Resolve(target);
        var quarantinePath = Path.Combine(
            _workingDirectory,
            ".tmp",
            "frankenstein",
            $"{Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}-quarantine");
        var quarantine = Quarantine(source, quarantinePath);
        var inspection = InspectPackage(quarantine.OutputPath, targetAdapter, displayRoot: source);
        var actions = RepairPlanner.Plan(inspection, targetAdapter);

        return new DoctorResult(
            SourcePath: source,
            QuarantinePath: quarantine.OutputPath,
            Target: targetAdapter.Name,
            Inspection: inspection,
            RepairActions: actions,
            Repairable: inspection.Errors.Count > 0 && actions.Count == inspection.RepairableErrorCount,
            SourceHashBefore: quarantine.SourceHash,
            SourceHashAfter: DirectoryHasher.HashDirectory(source));
    }

    public ValidationResult Validate(string sourcePath, string target)
    {
        var targetAdapter = TargetAdapter.Resolve(target);
        var inspection = InspectPackage(ResolvePath(sourcePath), targetAdapter, displayRoot: ResolvePath(sourcePath));
        return ValidationResult.FromInspection(inspection, targetAdapter.Name);
    }

    public RepairResult Repair(string sourcePath, string target, string planPath, string outputPath)
    {
        var source = ResolvePath(sourcePath);
        var plan = ResolvePath(planPath);
        var output = ResolvePath(outputPath);

        if (!File.Exists(plan))
        {
            throw new FrankensteinException($"repair plan not found: {plan}");
        }

        var beforeHash = DirectoryHasher.HashDirectory(source);
        var targetAdapter = TargetAdapter.Resolve(target);
        var inspection = InspectPackage(source, targetAdapter, displayRoot: source);
        var actions = RepairPlanner.Plan(inspection, targetAdapter);

        if (inspection.Errors.Count > 0 && actions.Count == 0)
        {
            throw new FrankensteinException("package has no safe repair actions.");
        }

        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        CopyDirectory(source, output);
        var petJsonPath = Path.Combine(output, PetJsonFileName);
        var petJson = JsonFile.ReadObject(petJsonPath);
        var changes = new JsonArray();

        foreach (var action in actions)
        {
            ApplyRepairAction(petJson, action);
            changes.Add(action.ToManifestJson());
        }

        JsonFile.WriteObject(petJsonPath, petJson);
        var afterHash = DirectoryHasher.HashDirectory(source);
        var sourceMutated = !string.Equals(beforeHash, afterHash, StringComparison.Ordinal);
        var manifestPath = Path.Combine(output, RepairManifestFileName);
        var manifest = new JsonObject
        {
            ["source"] = DisplayPath(source),
            ["output"] = DisplayPath(output),
            ["target"] = targetAdapter.Name,
            ["changed"] = changes,
            ["sourceMutated"] = sourceMutated
        };
        JsonFile.WriteObject(manifestPath, manifest);

        return new RepairResult(
            SourcePath: source,
            OutputPath: output,
            ManifestPath: manifestPath,
            Changes: actions,
            SourceMutated: sourceMutated);
    }

    public JsonObject Import(string sourcePath)
    {
        var source = ResolvePath(sourcePath);
        if (File.Exists(source))
        {
            return NormalizeImportedJson(source);
        }

        var package = LoadPetPackage(source);
        return NormalizePackage(package);
    }

    public ExportResult Export(string sourcePath, string target, string outputPath)
    {
        var normalized = Import(sourcePath);
        var adapter = TargetAdapter.Resolve(target);
        var output = ResolvePath(outputPath);

        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
        var petJson = ExportPetJson(normalized, adapter);
        JsonFile.WriteObject(Path.Combine(output, PetJsonFileName), petJson);

        var sourceSprite = ReadString(normalized, "spritesheet", "sourcePath");
        if (sourceSprite is null || !File.Exists(sourceSprite))
        {
            throw new FrankensteinException("normalized import is missing a readable spritesheet sourcePath.");
        }

        var spriteName = ReadString(normalized, "spritesheet", "path") ?? "spritesheet.webp";
        File.Copy(sourceSprite, Path.Combine(output, spriteName), overwrite: true);

        var validation = Validate(output, adapter.Name);
        return new ExportResult(output, validation.Valid);
    }

    public NormalizedDiff DiffNormalized(string leftPath, string rightPath)
    {
        var left = JsonFormatter.Canonical(StripVolatileImportFields(Import(leftPath)));
        var right = JsonFormatter.Canonical(StripVolatileImportFields(Import(rightPath)));
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return new NormalizedDiff(true, []);
        }

        var leftLines = left.Split('\n');
        var rightLines = right.Split('\n');
        var lines = new List<string>();
        var max = Math.Max(leftLines.Length, rightLines.Length);

        for (var index = 0; index < max; index++)
        {
            var leftLine = index < leftLines.Length ? leftLines[index] : string.Empty;
            var rightLine = index < rightLines.Length ? rightLines[index] : string.Empty;
            if (!string.Equals(leftLine, rightLine, StringComparison.Ordinal))
            {
                lines.Add($"- {leftLine}");
                lines.Add($"+ {rightLine}");
                if (lines.Count >= 40)
                {
                    lines.Add("...");
                    break;
                }
            }
        }

        return new NormalizedDiff(false, lines);
    }

    private static JsonObject StripVolatileImportFields(JsonObject normalized)
    {
        var clone = (JsonObject)normalized.DeepClone();
        if (clone["spritesheet"] is JsonObject spritesheet)
        {
            spritesheet.Remove("sourcePath");
        }

        return clone;
    }

    public RoundTripResult RoundTrip(string sourcePath, string target)
    {
        var source = ResolvePath(sourcePath);
        var tempRoot = Path.Combine(_workingDirectory, ".tmp", "frankenstein");
        Directory.CreateDirectory(tempRoot);

        var beforeHash = DirectoryHasher.HashDirectory(source);
        var importedPath = Path.Combine(tempRoot, "imported.json");
        var exportedPath = Path.Combine(tempRoot, "exported");
        var reimportedPath = Path.Combine(tempRoot, "reimported.json");

        var imported = Import(source);
        JsonFile.WriteObject(importedPath, imported);
        var sourceValidation = Validate(source, target);
        var export = Export(importedPath, target, exportedPath);
        var exportedValidation = Validate(exportedPath, target);
        var reimported = Import(exportedPath);
        JsonFile.WriteObject(reimportedPath, reimported);
        var reimportedValidation = Validate(exportedPath, target);
        var diff = DiffNormalized(importedPath, reimportedPath);
        var afterHash = DirectoryHasher.HashDirectory(source);
        var sourceMutated = !string.Equals(beforeHash, afterHash, StringComparison.Ordinal);

        return new RoundTripResult(
            SourcePath: source,
            Target: TargetAdapter.Resolve(target).Name,
            ImportedPath: importedPath,
            ExportedPath: exportedPath,
            ReimportedPath: reimportedPath,
            SourceValidation: sourceValidation,
            ExportedValidation: exportedValidation,
            ReimportedValidation: reimportedValidation,
            Diff: diff,
            SourceMutated: sourceMutated,
            Pass: sourceValidation.Valid &&
                  export.Valid &&
                  exportedValidation.Valid &&
                  reimportedValidation.Valid &&
                  diff.IsEmpty &&
                  !sourceMutated);
    }

    public AbilityCheckResult CheckAbilities(string sourcePath, string target)
    {
        var adapter = TargetAdapter.Resolve(target);
        var normalized = Import(sourcePath);
        var abilities = normalized["abilities"] as JsonArray;
        var errors = AbilityValidator.Validate(abilities, adapter);
        return new AbilityCheckResult(adapter.Name, errors.Count is 0, errors);
    }

    public AtlasInspection InspectAtlas(string sourcePath)
    {
        var package = LoadPetPackage(ResolvePath(sourcePath));
        var atlas = AtlasContract.TryRead(package.PetJson);
        if (atlas is null || package.SpritePath is null || !File.Exists(package.SpritePath))
        {
            return new AtlasInspection(false, null, null, [], package.Animations);
        }

        var image = ImageMetadataReader.Read(package.SpritePath);
        var rows = AtlasOccupancyInspector.ReadRows(package.SpritePath, atlas);
        return new AtlasInspection(image.Width == atlas.ExpectedWidth && image.Height == atlas.ExpectedHeight, atlas, image, rows, package.Animations);
    }

    public QuarantineResult Quarantine(string sourcePath, string outputPath)
    {
        var source = ResolvePath(sourcePath);
        var output = ResolvePath(outputPath);
        if (!Directory.Exists(source))
        {
            throw new FrankensteinException($"package directory not found: {source}");
        }

        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        CopyDirectory(source, output);
        return new QuarantineResult(source, output, DirectoryHasher.HashDirectory(source));
    }

    private static Inspection InspectPackage(string packageRoot, TargetAdapter target, string displayRoot)
    {
        var checks = new InspectionChecks();
        var errors = new List<PetIssue>();
        var warnings = new List<string>();
        JsonObject? petJson = null;
        AtlasContract? atlas = null;
        ImageInfo? asset = null;
        var animations = new SortedDictionary<string, AnimationContract>(StringComparer.Ordinal);
        IReadOnlyList<RowEvidence> rowEvidence = [];
        string? spritePath = null;

        checks.PetJsonExists = File.Exists(Path.Combine(packageRoot, PetJsonFileName));
        if (!checks.PetJsonExists)
        {
            errors.Add(PetIssue.NotRepairable("Missing pet.json", "package input", "pet.json", "", "file exists", "importer / validator"));
        }

        if (checks.PetJsonExists)
        {
            try
            {
                petJson = JsonFile.ReadObject(Path.Combine(packageRoot, PetJsonFileName));
                checks.JsonParses = true;
            }
            catch (JsonException)
            {
                errors.Add(PetIssue.NotRepairable("Invalid JSON", "package input", "pet.json", "", "valid JSON", "validator"));
            }
        }

        if (petJson is not null)
        {
            atlas = AtlasContract.TryRead(petJson);
            if (atlas is null)
            {
                errors.Add(PetIssue.NotRepairable("Invalid atlas contract", "contract", "pet.json", "atlas", "positive columns, rows, cellWidth, cellHeight", "pet contract"));
            }
            else
            {
                checks.AtlasGridValid = true;
            }

            animations = ReadAnimations(petJson);
            spritePath = ResolveSpritePath(packageRoot, petJson);
            checks.SpritesheetExists = spritePath is not null && File.Exists(spritePath);
            if (!checks.SpritesheetExists)
            {
                errors.Add(PetIssue.NotRepairable("Missing spritesheet", "package input", "spritesheet.webp", "", "file exists", "importer / validator"));
            }
        }

        if (spritePath is not null && File.Exists(spritePath) && atlas is not null)
        {
            asset = ImageMetadataReader.Read(spritePath);
            checks.SpritesheetDimensionsMatch = asset.Width == atlas.ExpectedWidth && asset.Height == atlas.ExpectedHeight;
            if (!checks.SpritesheetDimensionsMatch)
            {
                errors.Add(PetIssue.NotRepairable(
                    "Spritesheet dimensions mismatch",
                    "asset",
                    "spritesheet.webp",
                    $"{asset.Width}x{asset.Height}",
                    $"{atlas.ExpectedWidth}x{atlas.ExpectedHeight}",
                    "atlas validator"));
            }

            rowEvidence = AtlasOccupancyInspector.ReadRows(spritePath, atlas);
            if (rowEvidence.Count is 0)
            {
                warnings.Add("row occupancy evidence unavailable; install ImageMagick for visible-pixel row proof");
            }
        }

        if (atlas is not null)
        {
            checks.AnimationRowsValid = true;
            foreach (var (animationName, animation) in animations)
            {
                if (animation.Row < 0 || animation.Row >= atlas.Rows)
                {
                    checks.AnimationRowsValid = false;
                    var proposed = RepairPlanner.FindCandidateRow(animation, atlas, animations, rowEvidence);
                    errors.Add(PetIssue.ForRepair(
                        "Invalid animation row",
                        "contract",
                        "pet.json",
                        $"animations.{animationName}.row",
                        animation.Row.ToString(CultureInfo.InvariantCulture),
                        $"0..{atlas.Rows - 1}",
                        proposed?.ToString(CultureInfo.InvariantCulture),
                        proposed is null
                            ? "row index exceeded atlas row range"
                            : $"row {proposed.Value} contains non-empty frames and matches declared {animationName} frame count",
                        "pet.json animations",
                        RepairKind.AnimationRow));
                }

                if (animation.Frames <= 0 || animation.Frames > atlas.Columns)
                {
                    checks.AnimationRowsValid = false;
                    errors.Add(PetIssue.NotRepairable(
                        "Invalid animation frame count",
                        "contract",
                        "pet.json",
                        $"animations.{animationName}.frames",
                        $"1..{atlas.Columns}",
                        "pet.json animations"));
                }
            }
        }

        if (petJson is not null)
        {
            var targetIssues = target.ValidateRoot(petJson);
            errors.AddRange(targetIssues);
            checks.TargetCompatibilityValid = targetIssues.Count is 0;

            var abilityErrors = AbilityValidator.Validate(ReadAbilities(petJson), target);
            foreach (var abilityError in abilityErrors)
            {
                errors.Add(PetIssue.NotRepairable(
                    abilityError,
                    "ability metadata",
                    "pet.json",
                    "abilities",
                    "valid ability metadata",
                    "ability validator"));
            }
        }

        return new Inspection(
            PackageRoot: displayRoot,
            PetJsonFound: checks.PetJsonExists,
            SpritesheetFound: checks.SpritesheetExists,
            Checks: checks,
            Target: target.Name,
            Atlas: atlas,
            Asset: asset,
            Animations: animations,
            RowEvidence: rowEvidence,
            Errors: errors,
            Warnings: warnings);
    }

    private static PetPackage LoadPetPackage(string packageRoot)
    {
        if (!Directory.Exists(packageRoot))
        {
            throw new FrankensteinException($"package directory not found: {packageRoot}");
        }

        var petJsonPath = Path.Combine(packageRoot, PetJsonFileName);
        if (!File.Exists(petJsonPath))
        {
            throw new FrankensteinException($"pet.json not found: {petJsonPath}");
        }

        var petJson = JsonFile.ReadObject(petJsonPath);
        var spritePath = ResolveSpritePath(packageRoot, petJson);
        return new PetPackage(packageRoot, petJson, spritePath, ReadAnimations(petJson));
    }

    private static JsonObject NormalizePackage(PetPackage package)
    {
        var atlas = AtlasContract.TryRead(package.PetJson);
        if (atlas is null)
        {
            throw new FrankensteinException("cannot import package without a valid atlas contract.");
        }

        if (package.SpritePath is null || !File.Exists(package.SpritePath))
        {
            throw new FrankensteinException("cannot import package without a readable spritesheet.");
        }

        var image = ImageMetadataReader.Read(package.SpritePath);
        var output = new JsonObject
        {
            ["schema"] = "frankenstein.normalized-pet.v1",
            ["name"] = ReadString(package.PetJson, "name"),
            ["version"] = package.PetJson["version"]?.DeepClone(),
            ["description"] = ReadString(package.PetJson, "description"),
            ["spritesheet"] = new JsonObject
            {
                ["path"] = Path.GetFileName(package.SpritePath),
                ["sourcePath"] = package.SpritePath,
                ["sha256"] = FileHasher.HashFile(package.SpritePath),
                ["width"] = image.Width,
                ["height"] = image.Height
            },
            ["atlas"] = atlas.ToJson(),
            ["animations"] = AnimationsToJson(package.Animations),
            ["abilities"] = ReadAbilities(package.PetJson)?.DeepClone() ?? new JsonArray(),
            ["extensions"] = ReadExtensions(package.PetJson)
        };

        return output;
    }

    private static JsonObject NormalizeImportedJson(string path)
    {
        var json = JsonFile.ReadObject(path);
        if (!string.Equals(ReadString(json, "schema"), "frankenstein.normalized-pet.v1", StringComparison.Ordinal))
        {
            throw new FrankensteinException($"JSON input is not a Frankenstein normalized import: {path}");
        }

        return json;
    }

    private static JsonObject ExportPetJson(JsonObject normalized, TargetAdapter adapter)
    {
        var output = new JsonObject
        {
            ["name"] = normalized["name"]?.DeepClone(),
            ["version"] = normalized["version"]?.DeepClone(),
            ["description"] = normalized["description"]?.DeepClone(),
            ["spritesheet"] = ReadString(normalized, "spritesheet", "path") ?? "spritesheet.webp",
            ["atlas"] = normalized["atlas"]?.DeepClone(),
            ["animations"] = normalized["animations"]?.DeepClone()
        };

        var abilities = normalized["abilities"]?.DeepClone();
        if (abilities is JsonArray { Count: > 0 })
        {
            if (adapter.SupportsRootAbilities)
            {
                output["abilities"] = abilities;
            }
            else
            {
                var extension = normalized["extensions"]?.DeepClone() as JsonObject ?? new JsonObject();
                extension["abilities"] = abilities;
                output["x-frankenstein"] = extension;
            }
        }
        else if (normalized["extensions"]?.DeepClone() is JsonObject extension)
        {
            output["x-frankenstein"] = extension;
        }

        return output;
    }

    private static void ApplyRepairAction(JsonObject petJson, RepairAction action)
    {
        switch (action.Kind)
        {
            case RepairKind.AnimationRow:
                var animations = petJson["animations"] as JsonObject
                                 ?? throw new FrankensteinException("pet.json is missing animations.");
                var animationName = action.Path.Split('.')[1];
                var animation = animations[animationName] as JsonObject
                                ?? throw new FrankensteinException($"animation not found: {animationName}");
                if (action.To is null || !int.TryParse(action.To, NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
                {
                    throw new FrankensteinException($"repair action has no concrete row proposal: {action.Path}");
                }

                animation["row"] = row;
                break;
            case RepairKind.MoveAbilitiesToExtension:
                if (petJson["abilities"] is not { } abilities)
                {
                    break;
                }

                var extension = petJson["x-frankenstein"] as JsonObject ?? new JsonObject();
                extension["abilities"] = abilities.DeepClone();
                petJson["x-frankenstein"] = extension;
                petJson.Remove("abilities");
                break;
            default:
                throw new FrankensteinException($"unsupported repair action: {action.Kind}");
        }
    }

    private static SortedDictionary<string, AnimationContract> ReadAnimations(JsonObject petJson)
    {
        var output = new SortedDictionary<string, AnimationContract>(StringComparer.Ordinal);
        if (petJson["animations"] is not JsonObject animations)
        {
            return output;
        }

        foreach (var (name, value) in animations)
        {
            if (value is not JsonObject animation)
            {
                continue;
            }

            output[name] = new AnimationContract(
                Name: name,
                Row: ReadInt(animation, "row") ?? -1,
                Frames: ReadInt(animation, "frames") ?? ReadInt(animation, "frameCount") ?? -1);
        }

        return output;
    }

    private static JsonObject AnimationsToJson(SortedDictionary<string, AnimationContract> animations)
    {
        var output = new JsonObject();
        foreach (var (name, animation) in animations)
        {
            output[name] = new JsonObject
            {
                ["row"] = animation.Row,
                ["frames"] = animation.Frames
            };
        }

        return output;
    }

    private static JsonArray? ReadAbilities(JsonObject petJson)
    {
        if (petJson["abilities"] is JsonArray abilities)
        {
            return abilities;
        }

        if (petJson["x-frankenstein"] is JsonObject extension && extension["abilities"] is JsonArray extensionAbilities)
        {
            return extensionAbilities;
        }

        return null;
    }

    private static JsonObject ReadExtensions(JsonObject petJson)
    {
        var output = new JsonObject();
        if (petJson["x-frankenstein"] is JsonObject extension)
        {
            foreach (var (key, value) in extension)
            {
                if (!string.Equals(key, "abilities", StringComparison.Ordinal))
                {
                    output[key] = value?.DeepClone();
                }
            }
        }

        return output;
    }

    private static string? ResolveSpritePath(string packageRoot, JsonObject petJson)
    {
        var spriteName = ReadString(petJson, "spritesheet") ?? "spritesheet.webp";
        return Path.GetFullPath(spriteName, packageRoot);
    }

    private static string? ReadString(JsonObject json, string name)
    {
        return json[name]?.GetValue<string>();
    }

    private static string? ReadString(JsonObject json, string first, string second)
    {
        return json[first] is JsonObject nested ? ReadString(nested, second) : null;
    }

    private static int? ReadInt(JsonObject json, string name)
    {
        return json[name]?.GetValue<int>();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, file);
            var output = Path.Combine(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.Copy(file, output, overwrite: true);
        }
    }

    private string ResolvePath(string path) => Path.GetFullPath(path, _workingDirectory);

    private string DisplayPath(string path) => Path.GetRelativePath(_workingDirectory, path);
}

internal sealed record PetPackage(
    string Root,
    JsonObject PetJson,
    string? SpritePath,
    SortedDictionary<string, AnimationContract> Animations);

internal sealed record AnimationContract(string Name, int Row, int Frames);

internal sealed record AtlasContract(int Columns, int Rows, int CellWidth, int CellHeight)
{
    public int ExpectedWidth => Columns * CellWidth;

    public int ExpectedHeight => Rows * CellHeight;

    public int GridCells => Columns * Rows;

    public JsonObject ToJson() => new()
    {
        ["columns"] = Columns,
        ["rows"] = Rows,
        ["cellWidth"] = CellWidth,
        ["cellHeight"] = CellHeight
    };

    public static AtlasContract? TryRead(JsonObject petJson)
    {
        if (petJson["atlas"] is not JsonObject atlas)
        {
            return null;
        }

        var columns = ReadPositiveInt(atlas, "columns");
        var rows = ReadPositiveInt(atlas, "rows");
        var cellWidth = ReadPositiveInt(atlas, "cellWidth");
        var cellHeight = ReadPositiveInt(atlas, "cellHeight");
        if (columns is null || rows is null || cellWidth is null || cellHeight is null)
        {
            return null;
        }

        return new AtlasContract(columns.Value, rows.Value, cellWidth.Value, cellHeight.Value);
    }

    private static int? ReadPositiveInt(JsonObject json, string name)
    {
        var value = json[name]?.GetValue<int>();
        return value > 0 ? value : null;
    }
}

internal sealed record ImageInfo(int Width, int Height, string Format);

internal sealed record RowEvidence(int Row, int NonEmptyFrames, bool ToolAvailable)
{
    public bool NonEmpty => NonEmptyFrames > 0;
}

internal sealed record Inspection(
    string PackageRoot,
    bool PetJsonFound,
    bool SpritesheetFound,
    InspectionChecks Checks,
    string Target,
    AtlasContract? Atlas,
    ImageInfo? Asset,
    SortedDictionary<string, AnimationContract> Animations,
    IReadOnlyList<RowEvidence> RowEvidence,
    IReadOnlyList<PetIssue> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool Broken => Errors.Count > 0;

    public int RepairableErrorCount => Errors.Count(static issue => issue.Repairable);
}

internal sealed class InspectionChecks
{
    public bool PetJsonExists { get; set; }

    public bool SpritesheetExists { get; set; }

    public bool JsonParses { get; set; }

    public bool AtlasGridValid { get; set; }

    public bool SpritesheetDimensionsMatch { get; set; }

    public bool AnimationRowsValid { get; set; }

    public bool TargetCompatibilityValid { get; set; }

    public JsonObject ToJson() => new()
    {
        ["petJsonExists"] = PetJsonExists,
        ["spritesheetExists"] = SpritesheetExists,
        ["jsonParses"] = JsonParses,
        ["atlasGridValid"] = AtlasGridValid,
        ["spritesheetDimensionsMatch"] = SpritesheetDimensionsMatch,
        ["animationRowsValid"] = AnimationRowsValid,
        ["targetCompatibilityValid"] = TargetCompatibilityValid
    };
}

internal sealed record DoctorResult(
    string SourcePath,
    string QuarantinePath,
    string Target,
    Inspection Inspection,
    IReadOnlyList<RepairAction> RepairActions,
    bool Repairable,
    string SourceHashBefore,
    string SourceHashAfter);

internal sealed record ValidationResult(
    bool Valid,
    string Target,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    InspectionChecks Checks)
{
    public JsonObject ToJson()
    {
        var errors = new JsonArray();
        foreach (var error in Errors)
        {
            errors.Add(error);
        }

        var warnings = new JsonArray();
        foreach (var warning in Warnings)
        {
            warnings.Add(warning);
        }

        return new JsonObject
        {
            ["valid"] = Valid,
            ["target"] = Target,
            ["errors"] = errors,
            ["warnings"] = warnings,
            ["checks"] = Checks.ToJson()
        };
    }

    public static ValidationResult FromInspection(Inspection inspection, string target) =>
        new(
            inspection.Errors.Count is 0 &&
            inspection.Checks.PetJsonExists &&
            inspection.Checks.SpritesheetExists &&
            inspection.Checks.JsonParses &&
            inspection.Checks.AtlasGridValid &&
            inspection.Checks.SpritesheetDimensionsMatch &&
            inspection.Checks.AnimationRowsValid &&
            inspection.Checks.TargetCompatibilityValid,
            target,
            inspection.Errors.Select(static issue => issue.Message).ToArray(),
            inspection.Warnings,
            inspection.Checks);
}

internal sealed record RepairResult(
    string SourcePath,
    string OutputPath,
    string ManifestPath,
    IReadOnlyList<RepairAction> Changes,
    bool SourceMutated);

internal sealed record ExportResult(string OutputPath, bool Valid);

internal sealed record NormalizedDiff(bool IsEmpty, IReadOnlyList<string> Lines);

internal sealed record RoundTripResult(
    string SourcePath,
    string Target,
    string ImportedPath,
    string ExportedPath,
    string ReimportedPath,
    ValidationResult SourceValidation,
    ValidationResult ExportedValidation,
    ValidationResult ReimportedValidation,
    NormalizedDiff Diff,
    bool SourceMutated,
    bool Pass);

internal sealed record AbilityCheckResult(string Target, bool Valid, IReadOnlyList<string> Errors)
{
    public JsonObject ToJson()
    {
        var errors = new JsonArray();
        foreach (var error in Errors)
        {
            errors.Add(error);
        }

        return new JsonObject
        {
            ["valid"] = Valid,
            ["target"] = Target,
            ["errors"] = errors
        };
    }
}

internal sealed record AtlasInspection(
    bool Valid,
    AtlasContract? Atlas,
    ImageInfo? Image,
    IReadOnlyList<RowEvidence> Rows,
    SortedDictionary<string, AnimationContract> Animations);

internal sealed record QuarantineResult(string SourcePath, string OutputPath, string SourceHash);

internal sealed class FrankensteinException : Exception
{
    public FrankensteinException(string message)
        : base(message)
    {
    }
}
