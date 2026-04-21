namespace Qyl.Instrumentation.Generators.Loom.Models;

internal readonly record struct LoomTypeDeclarationModel(
    string Name,
    string Keyword,
    string Modifiers,
    string TypeParameters,
    EquatableArray<string> ConstraintClauses);

internal readonly record struct LoomParameterModel(
    string Name,
    string TypeFullyQualified,
    bool IsNullable,
    bool HasDefaultValue,
    string? DefaultValueLiteral,
    string? Description,
    bool IsCancellationToken,
    bool IsSchemaVisible,
    bool IsInfrastructureBound,
    EquatableArray<string> EnumValues);

internal readonly record struct LoomToolBudgetModel(
    int MaxAttempts,
    int MaxToolCalls,
    int MaxTokens);

internal readonly record struct LoomToolResultModel(
    string? OutputTypeFullyQualified,
    string? StructuredOutputTypeFullyQualified,
    string? ResultSchemaHint,
    bool HasStructuredOutput,
    bool IsSchemaVisible);

internal readonly record struct LoomToolModel(
    string Name,
    string Description,
    int Phase,
    string? UseOnlyWhen,
    string? DoNotUseWhen,
    string ContainingTypeFullyQualified,
    EquatableArray<LoomTypeDeclarationModel> DeclarationChain,
    string MethodName,
    bool IsStatic,
    bool IsAwaitable,
    bool ReturnsValue,
    string? OutputTypeFullyQualified,
    string? StructuredOutputTypeFullyQualified,
    LoomToolResultModel Result,
    LoomToolBudgetModel Budget,
    EquatableArray<LoomParameterModel> Parameters,
    EquatableArray<string> RequiredCapabilities,
    bool RequiresApproval,
    int SideEffect);

internal readonly record struct LoomContractPropertyModel(
    string Name,
    string TypeFullyQualified,
    bool IsNullable,
    bool IsRequired,
    EquatableArray<string> EnumValues);

internal readonly record struct LoomContractModel(
    string Name,
    string FullyQualifiedTypeName,
    EquatableArray<LoomTypeDeclarationModel> DeclarationChain,
    EquatableArray<LoomContractPropertyModel> Properties);

internal readonly record struct LoomStepModel(
    string Id,
    int Phase,
    string ExecutorTypeFullyQualified,
    EquatableArray<LoomTypeDeclarationModel> DeclarationChain,
    string? Description);

internal readonly record struct LoomWorkflowModel(
    string Id,
    string RunStateTypeFullyQualified,
    EquatableArray<string> StepIds,
    string WorkflowTypeFullyQualified,
    EquatableArray<LoomTypeDeclarationModel> DeclarationChain,
    string? Description);
