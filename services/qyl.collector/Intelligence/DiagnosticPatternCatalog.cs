using Qyl.Api.Contracts.Intelligence;

namespace Qyl.Collector.Intelligence;

internal static class DiagnosticPatternCatalog
{
    public static readonly IReadOnlyList<DiagnosticPattern> Patterns = [];

    public static readonly IReadOnlyList<CausalRule> CausalRules = [];

    public static readonly IReadOnlyList<InvestigationStrategy> InvestigationStrategies = [];
}
