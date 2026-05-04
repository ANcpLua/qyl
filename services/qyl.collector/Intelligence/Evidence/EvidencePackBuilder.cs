using Qyl.Contracts.Agenting;

namespace Qyl.Collector.Intelligence.Evidence;

public interface IEvidencePackBuilder
{
    AgentRunEvidencePack Build(AutofixEvidenceInput input);
}

public sealed class DeterministicEvidencePackBuilder : IEvidencePackBuilder
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public AgentRunEvidencePack Build(AutofixEvidenceInput input)
    {
        Guard.NotNull(input);

        var artifacts = (input.Artifacts ?? Array.Empty<AgentRunArtifactRef>())
            .OrderBy(static artifact => artifact.Kind)
            .ThenBy(static artifact => artifact.ArtifactId, StringComparer.Ordinal)
            .ToArray();

        var issueFacts = OrderFacts(input.Issue.Facts);
        var regressionFacts = OrderFacts(input.Regression?.Facts);
        var deploymentFacts = OrderFacts(input.Deployment?.Facts);
        var contextFacts = OrderFacts(input.ContextFacts);
        var causalFacts = OrderFacts(input.CausalFacts);

        return new AgentRunEvidencePack
        {
            PackId = $"{input.RunId}:evidence",
            RunId = input.RunId,
            IssueId = input.IssueId,
            CreatedAtUtc = input.CreatedAtUtc,
            Artifacts = artifacts,
            ContextJson =
                Serialize(new { issue = input.Issue, regression = input.Regression, deployment = input.Deployment }),
            SignalsSummaryJson = Serialize(new { issueFacts, regressionFacts, deploymentFacts, contextFacts }),
            CausalHintsJson = Serialize(causalFacts)
        };
    }

    private static EvidenceFact[] OrderFacts(IReadOnlyList<EvidenceFact>? facts) =>
        (facts ?? Array.Empty<EvidenceFact>())
        .OrderBy(static fact => fact.Category, StringComparer.Ordinal)
        .ThenBy(static fact => fact.Key, StringComparer.Ordinal)
        .ThenBy(static fact => fact.Value, StringComparer.Ordinal)
        .ThenBy(static fact => fact.Source, StringComparer.Ordinal)
        .ToArray();

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, s_jsonOptions);
}
