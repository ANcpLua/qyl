
using Qyl.Loom.Autofix.Workflow;

namespace Qyl.Loom.Workflows;

internal interface IQylLoomWorkflowBuilder
{
    Microsoft.Agents.AI.Workflows.Workflow BuildAutofixWorkflow(AutofixWorkflowConfig config);
}

internal sealed class QylLoomWorkflowBuilder(AutofixWorkflowFactory autofix) : IQylLoomWorkflowBuilder
{
    public Microsoft.Agents.AI.Workflows.Workflow BuildAutofixWorkflow(AutofixWorkflowConfig config) =>
        autofix.Build(config);
}
