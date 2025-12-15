namespace qyl.collector;

public static class QylAttributes
{
    private const string _prefix = "qyl";

    public const string CostUsd = $"{_prefix}.cost.usd";

    public const string CostCurrency = $"{_prefix}.cost.currency";

    public const string SessionId = $"{_prefix}.session.id";

    public const string SessionName = $"{_prefix}.session.name";

    public const string FeedbackScore = $"{_prefix}.feedback.score";

    public const string FeedbackComment = $"{_prefix}.feedback.comment";

    public const string AgentId = $"{_prefix}.agent.id";

    public const string AgentName = $"{_prefix}.agent.name";

    public const string AgentRole = $"{_prefix}.agent.role";

    public const string WorkflowId = $"{_prefix}.workflow.id";

    public const string WorkflowStep = $"{_prefix}.workflow.step";

    public const string WorkflowStepIndex = $"{_prefix}.workflow.step.index";
}