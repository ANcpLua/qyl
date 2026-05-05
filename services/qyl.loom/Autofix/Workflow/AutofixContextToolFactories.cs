
namespace Qyl.Loom.Autofix.Workflow;

internal static class AutofixContextToolFactories
{
    public static IReadOnlyList<AIFunction> Create(AutofixContextTools target) =>
    [
        AIFunctionFactory.Create(target.GetIssueSummaryAsync,
            new AIFunctionFactoryOptions { Name = "qyl.autofix.get_issue_summary" }),
        AIFunctionFactory.Create(target.GetRecentEventsAsync,
            new AIFunctionFactoryOptions { Name = "qyl.autofix.get_recent_events" }),
        AIFunctionFactory.Create(target.ListRecentIssuesAsync,
            new AIFunctionFactoryOptions { Name = "qyl.autofix.list_recent_issues" }),
        AIFunctionFactory.Create(target.GetDeploymentsAfterAsync,
            new AIFunctionFactoryOptions { Name = "qyl.autofix.get_deployments_after" })
    ];
}
