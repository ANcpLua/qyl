using ContractChoices = System.Collections.Generic.IReadOnlyList<(string Name, System.Collections.Generic.IReadOnlyList<string> Values, System.Collections.Generic.IReadOnlyList<System.Type> Models)>;
using ContractLengths = System.Collections.Generic.IReadOnlyList<(string Name, int? Minimum, int? Maximum)>;
using ContractPatterns = System.Collections.Generic.IReadOnlyList<(string Name, string Pattern)>;
using ContractRanges = System.Collections.Generic.IReadOnlyList<(string Name, double? Minimum, double? Maximum, double? Default)>;

namespace Qyl.Collector.Hosting;

/// <summary>The forms a member C# reads as a bare string or object takes: its strings, object forms and JSON types, and the models of its object forms.</summary>
internal sealed record ContractChoice(IReadOnlyList<string> Values, IReadOnlyList<Type> Models);

/// <summary>
/// The rules a request body's contract states beyond its type, as the C# emitter lists them beside the body, each by its
/// wire path at any depth with an array's items written <c>[]</c>: the range a number keeps, the length a string or an
/// array keeps, the pattern a string matches and the forms a member C# reads as a bare string or object takes.
/// </summary>
internal sealed class ContractRules(ContractRanges ranges, ContractLengths lengths, ContractPatterns patterns, ContractChoices choices)
{
    /// <summary>The rules of a body whose contract states none beyond its type.</summary>
    public static ContractRules None { get; } = new([], [], [], []);

    public FrozenDictionary<string, (double? Minimum, double? Maximum, double? Default)> Ranges { get; } =
        ranges.ToFrozenDictionary(static range => range.Name, static range => (range.Minimum, range.Maximum, range.Default), StringComparer.Ordinal);

    public FrozenDictionary<string, (int? Minimum, int? Maximum)> Lengths { get; } =
        lengths.ToFrozenDictionary(static length => length.Name, static length => (length.Minimum, length.Maximum), StringComparer.Ordinal);

    public FrozenDictionary<string, string> Patterns { get; } =
        patterns.ToFrozenDictionary(static pattern => pattern.Name, static pattern => pattern.Pattern, StringComparer.Ordinal);

    public FrozenDictionary<string, ContractChoice> Choices { get; } =
        choices.ToFrozenDictionary(static choice => choice.Name, static choice => new ContractChoice(choice.Values, choice.Models), StringComparer.Ordinal);
}
