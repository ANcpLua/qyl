#nullable enable

namespace Qyl.Intelligence;

public sealed class Signal
{
    public required string Attribute { get; init; }
    public required Qyl.Intelligence.SignalOperator Operator { get; init; }
    public string? Value { get; init; }
}

public sealed class DiagnosticPattern
{
    public required string Id { get; init; }
    public required Qyl.Intelligence.PatternCategory Category { get; init; }
    public required IReadOnlyList<Qyl.Intelligence.Signal> Signals { get; init; }
    public required string Hypothesis { get; init; }
    public required double Confidence { get; init; }
}

public sealed class CausalRule
{
    public required string Id { get; init; }
    public required string CausePattern { get; init; }
    public required string EffectPattern { get; init; }
    public required double Strength { get; init; }
    public string? TemporalWindow { get; init; }
}

public sealed class InvestigationStep
{
    public required string Action { get; init; }
    public required string Query { get; init; }
    public required string Description { get; init; }
}

public sealed class InvestigationStrategy
{
    public required string Id { get; init; }
    public required string TriggerPattern { get; init; }
    public required IReadOnlyList<Qyl.Intelligence.InvestigationStep> Steps { get; init; }
}

public enum SignalOperator
{
    Eq,
    Neq,
    Gt,
    Gte,
    Lt,
    Lte,
    Contains,
    Exists,
    Not_exists,
    Matches,
    In_set
}

public enum PatternCategory
{
    Error,
    Latency,
    Cost,
    Availability,
    Genai,
    Data
}
