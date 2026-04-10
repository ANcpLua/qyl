namespace Qyl.Agents.Generator.Extraction;

using Models;

internal static class ToolExtractor
{
    private const string ToolAttributeName = "Qyl.Agents.ToolAttribute";
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";

    public static DiagnosticFlow<ToolModel> Extract(
        IMethodSymbol method,
        Compilation compilation,
        AwaitableContext awaitable,
        CancellationToken cancellationToken)
    {
        var guardFlow = SemanticGuard.ForMethod(method)
            .MustNotBeStatic(
                DiagnosticInfo.Create(DiagnosticDescriptors.ToolMethodMustNotBeStatic, method, method.Name))
            .Must(static m => !m.IsGenericMethod,
                DiagnosticInfo.Create(DiagnosticDescriptors.ToolMethodMustNotBeGeneric, method, method.Name))
            .ToFlow();

        if (guardFlow.IsFailed)
            return DiagnosticFlow.Fail<ToolModel>(guardFlow.Diagnostics);

        var (returnKind, resultTypeFqn) = ClassifyReturnType(method, awaitable);

        if (returnKind is null)
            return DiagnosticFlow.Fail<ToolModel>(DiagnosticInfo.Create(
                DiagnosticDescriptors.UnsupportedReturnType, method, method.Name,
                method.ReturnType.ToDisplayString()));

        var toolAttr = method.GetAttribute(ToolAttributeName);
        var toolName = toolAttr?.GetConstructorArgument<string>(0)
                       ?? toolAttr?.GetNamedArgument<string>("Name")
                       ?? method.Name.ToKebabCase();
        var description = toolAttr?.GetNamedArgument<string>("Description")
                          ?? method.GetSummaryText(compilation, cancellationToken)
                          ?? string.Empty;

        var hasCancellationToken = HasCancellationToken(method);

        var readOnly = ReadHint(toolAttr, "ReadOnly");
        var destructive = ReadHint(toolAttr, "Destructive");
        var idempotent = ReadHint(toolAttr, "Idempotent");
        var openWorld = ReadHint(toolAttr, "OpenWorld");
        var taskSupport = ReadTaskSupport(toolAttr);

        var flow = ParameterExtractor.ExtractParameters(method, cancellationToken)
            .Select(parameters => new ToolModel(
                method.Name,
                toolName,
                description,
                resultTypeFqn,
                returnKind.Value,
                hasCancellationToken,
                parameters,
                readOnly,
                destructive,
                idempotent,
                openWorld,
                taskSupport));

        if (readOnly == ToolHintValue.Unset &&
            destructive == ToolHintValue.Unset &&
            idempotent == ToolHintValue.Unset &&
            openWorld == ToolHintValue.Unset)
            flow = flow.Warn(DiagnosticInfo.Create(
                DiagnosticDescriptors.AllHintsUnset, method, method.Name));

        // Claude-quality: tool description should be 50+ chars (3-4 sentences)
        if (description.Length < 50)
            flow = flow.Warn(DiagnosticInfo.Create(
                DiagnosticDescriptors.ToolDescriptionTooShort, method,
                toolName, description.Length.ToString()));

        // Claude-quality: check parameter descriptions
        foreach (var param in method.Parameters)
        {
            if (param.Type.ToDisplayString() == CancellationTokenTypeName)
                continue;

            var paramDesc = param.GetAttribute("System.ComponentModel.DescriptionAttribute");
            var paramDescLength = paramDesc?.GetConstructorArgument<string>(0)?.Length ?? 0;
            if (paramDescLength < 10)
                flow = flow.Warn(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ParameterDescriptionTooShort, param,
                    param.Name, toolName, paramDescLength.ToString()));
        }

        // Claude-quality: suggest input_examples for complex tools
        var complexParamCount = 0;
        foreach (var param in method.Parameters)
        {
            if (param.Type.ToDisplayString() == CancellationTokenTypeName)
                continue;
            if (param.Type is IArrayTypeSymbol or INamedTypeSymbol { IsGenericType: true })
                complexParamCount++;
        }

        if (method.Parameters.Length >= 3 && complexParamCount > 0)
            flow = flow.Warn(DiagnosticInfo.Create(
                DiagnosticDescriptors.ConsiderInputExamples, method,
                toolName, method.Parameters.Length.ToString()));

        return flow;
    }

    private static (ReturnKind? Kind, string ResultFqn) ClassifyReturnType(
        IMethodSymbol method, AwaitableContext awaitable)
    {
        if (method.ReturnsVoid)
            return (ReturnKind.Void, string.Empty);

        var ret = method.ReturnType;

        if (awaitable.IsTaskLike(ret))
        {
            if (ret is not INamedTypeSymbol namedRet)
                return (null, string.Empty);

            var resultType = awaitable.GetTaskResultType(ret);
            var original = namedRet.OriginalDefinition.ToDisplayString();
            var isValueTask = original.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);

            if (resultType is not null)
                return (isValueTask ? ReturnKind.ValueTaskOfT : ReturnKind.TaskOfT,
                    resultType.GetFullyQualifiedName());

            return (isValueTask ? ReturnKind.ValueTask : ReturnKind.Task, string.Empty);
        }

        // Plain synchronous return — valid as long as it's not a raw generic/open type
        if (ret is INamedTypeSymbol { IsUnboundGenericType: false } or IArrayTypeSymbol)
            return (ReturnKind.Sync, ret.GetFullyQualifiedName());

        return (null, string.Empty);
    }

    private static ToolHintValue ReadHint(AttributeData? attr, string name)
    {
        if (attr is null || attr.NamedArguments.IsDefaultOrEmpty)
            return ToolHintValue.Unset;

        foreach (var arg in attr.NamedArguments)
        {
            if (string.Equals(arg.Key, name, StringComparison.Ordinal) &&
                arg.Value.Value is not null)
                return (ToolHintValue)Convert.ToByte(arg.Value.Value);
        }

        return ToolHintValue.Unset;
    }

    private static ToolTaskSupportValue ReadTaskSupport(AttributeData? attr)
    {
        if (attr is null || attr.NamedArguments.IsDefaultOrEmpty)
            return ToolTaskSupportValue.Unset;

        foreach (var arg in attr.NamedArguments)
        {
            if (string.Equals(arg.Key, "TaskSupport", StringComparison.Ordinal) &&
                arg.Value.Value is not null)
                return (ToolTaskSupportValue)Convert.ToByte(arg.Value.Value);
        }

        return ToolTaskSupportValue.Unset;
    }

    private static bool HasCancellationToken(IMethodSymbol method)
    {
        foreach (var p in method.Parameters)
            if (p.Type.ToDisplayString() == CancellationTokenTypeName)
                return true;
        return false;
    }
}
