
namespace Qyl.Loom.Patterns.Agents;

public interface IQylLoomPatternsAgentsBuilder
{
    AIAgent BuildRcaAgent();

    AIAgent BuildSolutionAgent();

    AIAgent BuildConfidenceAgent();
}
