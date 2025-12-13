// using System.Collections.Immutable;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Diagnostics;
//
// namespace qyl.analyzers;
//
// [DiagnosticAnalyzer(LanguageNames.CSharp)]
// public sealed class QylGenAiNonCanonicalAttributeAnalyzer : DiagnosticAnalyzer
// {
//     public const string DiagnosticId = "QYL002";
//
//     private const string Category = "Qyl.GenAI";
//
//     private static readonly LocalizableString Title = new LocalizableResourceString(
//         nameof(Resources.QYL002Title), Resources.ResourceManager, typeof(Resources));
//
//     private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
//         nameof(Resources.QYL002MessageFormat), Resources.ResourceManager, typeof(Resources));
//
//     private static readonly LocalizableString Description = new LocalizableResourceString(
//         nameof(Resources.QYL002Description), Resources.ResourceManager, typeof(Resources));
//
//     private static readonly DiagnosticDescriptor Rule = new(
//         DiagnosticId, Title, MessageFormat, Category,
//         DiagnosticSeverity.Warning, true, Description);
//
//     private static readonly ImmutableHashSet<string> Allowed =
//         ImmutableHashSet.Create(
//
//             OpenTelemetryConsts.GenAI.Agent.Id,
//             OpenTelemetryConsts.GenAI.Agent.Name,
//             OpenTelemetryConsts.GenAI.Agent.Description,
//
//             OpenTelemetryConsts.GenAI.Conversation.Id,
//             OpenTelemetryConsts.GenAI.DataSource.Id,
//
//             OpenTelemetryConsts.GenAI.Messages.SystemInstructions,
//             OpenTelemetryConsts.GenAI.Messages.InputMessages,
//             OpenTelemetryConsts.GenAI.Messages.OutputMessages,
//             OpenTelemetryConsts.GenAI.Messages.OutputType,
//
//             OpenTelemetryConsts.GenAI.Provider.Name,
//             OpenTelemetryConsts.GenAI.Operation.Name,
//
//             OpenTelemetryConsts.GenAI.Request.Model,
//             OpenTelemetryConsts.GenAI.Request.Temperature,
//             OpenTelemetryConsts.GenAI.Request.TopK,
//             OpenTelemetryConsts.GenAI.Request.TopP,
//             OpenTelemetryConsts.GenAI.Request.PresencePenalty,
//             OpenTelemetryConsts.GenAI.Request.FrequencyPenalty,
//             OpenTelemetryConsts.GenAI.Request.MaxTokens,
//             OpenTelemetryConsts.GenAI.Request.StopSequences,
//             OpenTelemetryConsts.GenAI.Request.ChoiceCount,
//             OpenTelemetryConsts.GenAI.Request.Seed,
//             OpenTelemetryConsts.GenAI.Request.EncodingFormats,
//
//             OpenTelemetryConsts.GenAI.Response.Id,
//             OpenTelemetryConsts.GenAI.Response.Model,
//             OpenTelemetryConsts.GenAI.Response.FinishReasons,
//
//             OpenTelemetryConsts.GenAI.Usage.InputTokens,
//             OpenTelemetryConsts.GenAI.Usage.OutputTokens,
//
//             OpenTelemetryConsts.GenAI.Token.Type,
//
//             OpenTelemetryConsts.GenAI.Tool.Definitions,
//             OpenTelemetryConsts.GenAI.Tool.Name,
//             OpenTelemetryConsts.GenAI.Tool.Description,
//             OpenTelemetryConsts.GenAI.Tool.Type,
//             OpenTelemetryConsts.GenAI.Tool.Call.Id,
//             OpenTelemetryConsts.GenAI.Tool.Call.Arguments,
//             OpenTelemetryConsts.GenAI.Tool.Call.Result,
//
//             OpenTelemetryConsts.GenAI.Evaluation.Name,
//             OpenTelemetryConsts.GenAI.Evaluation.ScoreValue,
//             OpenTelemetryConsts.GenAI.Evaluation.ScoreLabel,
//             OpenTelemetryConsts.GenAI.Evaluation.Explanation
//         );
//
//     private static readonly ImmutableHashSet<string> Deprecated =
//         ImmutableHashSet.Create(
//             OpenTelemetryConsts.GenAI.Deprecated.System,
//             OpenTelemetryConsts.GenAI.Deprecated.Prompt,
//             OpenTelemetryConsts.GenAI.Deprecated.Completion,
//             OpenTelemetryConsts.GenAI.Deprecated.UsagePromptTokens,
//             OpenTelemetryConsts.GenAI.Deprecated.UsageCompletionTokens
//         );
//
//     public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
//         => [Rule];
//
//     public override void Initialize(AnalysisContext context)
//     {
//         context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
//         context.EnableConcurrentExecution();
//         context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
//     }
//
//     private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
//     {
//         var invocation = (InvocationExpressionSyntax)context.Node;
//
//         if (!GenAiAttributeUsage.TryGetAttributeNameLiteral(
//                 invocation,
//                 context.SemanticModel,
//                 context.CancellationToken,
//                 out var attributeName))
//             return;
//
//         if (Deprecated.Contains(attributeName))
//             return;
//
//         if (Allowed.Contains(attributeName))
//             return;
//
//         var diagnostic = Diagnostic.Create(
//             Rule,
//             invocation.GetLocation(),
//             attributeName);
//
//         context.ReportDiagnostic(diagnostic);
//     }
// }
//
//


