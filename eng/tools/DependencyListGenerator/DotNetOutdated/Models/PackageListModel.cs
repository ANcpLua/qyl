using System.Text.Json.Serialization;

namespace DependencyListGenerator.DotNetOutdated.Models;

public record PackageListModel
{
    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("parameters")]
    public string Parameters { get; init; } = string.Empty;

    [JsonPropertyName("projects")]
    public IReadOnlyCollection<ProjectInfo> Projects { get; init; } = [];
}
