namespace qyl.collector;

public static class QylAttributes
{
    private const string Prefix = "qyl";

    public const string CostUsd = $"{Prefix}.cost.usd";

    public const string CostCurrency = $"{Prefix}.cost.currency";

    public const string SessionId = $"{Prefix}.session.id";

    public const string SessionName = $"{Prefix}.session.name";

    public const string FeedbackScore = $"{Prefix}.feedback.score";

    public const string FeedbackComment = $"{Prefix}.feedback.comment";

    public const string AgentId = $"{Prefix}.agent.id";

    public const string AgentName = $"{Prefix}.agent.name";

    public const string AgentRole = $"{Prefix}.agent.role";

    public const string WorkflowId = $"{Prefix}.workflow.id";

    public const string WorkflowStep = $"{Prefix}.workflow.step";

    public const string WorkflowStepIndex = $"{Prefix}.workflow.step.index";
}