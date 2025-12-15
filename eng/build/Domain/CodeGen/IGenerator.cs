using System.Collections.Frozen;
using Context;

namespace Domain.CodeGen;

/// <summary>
///     Interface for code generators that produce files from <see cref="QylSchema" />.
///     Implementations return immutable output (FrozenDictionary) at the boundary.
/// </summary>
public interface IGenerator
{
    /// <summary>
    ///     Human-readable name for logging (e.g., "DuckDB", "CSharp", "TypeScript").
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Generates code files from the schema.
    /// </summary>
    /// <param name="schema">The canonical schema definition.</param>
    /// <param name="paths">Build paths for output locations.</param>
    /// <param name="rootNamespace">Root namespace for generated C# code.</param>
    /// <returns>Dictionary of relative file names to content.</returns>
    FrozenDictionary<string, string> Generate(QylSchema schema, BuildPaths paths, string rootNamespace);
}