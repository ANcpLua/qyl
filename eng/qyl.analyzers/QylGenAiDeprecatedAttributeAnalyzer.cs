// using System.Collections.Immutable;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Diagnostics;
//
// namespace qyl.analyzers;
//
// [DiagnosticAnalyzer(LanguageNames.CSharp)]
// public sealed class QylGenAiDeprecatedAttributeAnalyzer : DiagnosticAnalyzer
// {
//     public const string DiagnosticId = "QYL001";
//
//     private const string Category = "Qyl.GenAI";
//
//     private static readonly LocalizableString Title = new LocalizableResourceString(
//         nameof(Resources.QYL001Title), Resources.ResourceManager, typeof(Resources));
//
//     private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
//         nameof(Resources.QYL001MessageFormat), Resources.ResourceManager, typeof(Resources));
//
//     private static readonly LocalizableString Description = new LocalizableResourceString(
//         nameof(Resources.QYL001Description), Resources.ResourceManager, typeof(Resources));
//
//     private static readonly DiagnosticDescriptor Rule = new(
//         DiagnosticId, Title, MessageFormat, Category,
//         DiagnosticSeverity.Warning, true, Description);
//
//     private static readonly ImmutableDictionary<string, string?> Deprecated =
//         new Dictionary<string, string?>
//         {
//             { OpenTelemetryConsts.GenAI.Deprecated.System, OpenTelemetryConsts.GenAI.Provider.Name },
//             { OpenTelemetryConsts.GenAI.Deprecated.Prompt, null },
//             { OpenTelemetryConsts.GenAI.Deprecated.Completion, null },
//             { OpenTelemetryConsts.GenAI.Deprecated.UsagePromptTokens, OpenTelemetryConsts.GenAI.Usage.InputTokens },
//             {
//                 OpenTelemetryConsts.GenAI.Deprecated.UsageCompletionTokens, OpenTelemetryConsts.GenAI.Usage.OutputTokens
//             }
//         }.ToImmutableDictionary();
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
//         if (!Deprecated.TryGetValue(attributeName, out var replacement))
//             return;
//
//         var messageReplacement = replacement ?? "see QYL GenAI 1.38 documentation";
//
//         var diagnostic = Diagnostic.Create(
//             Rule,
//             invocation.GetLocation(),
//             attributeName,
//             messageReplacement);
//
//         context.ReportDiagnostic(diagnostic);
//     }
// }
//
// internal static class GenAiAttributeUsage
// {
//     private static readonly ImmutableHashSet<string> GenAiPrefixes =
//         ImmutableHashSet.Create("gen_ai.");
//
//         public static bool TryGetAttributeNameLiteral(
//         InvocationExpressionSyntax invocation,
//         SemanticModel semanticModel,
//         CancellationToken cancellationToken,
//         out string attributeName)
//     {
//         attributeName = string.Empty;
//
//         if (invocation.ArgumentList.Arguments.Count == 0)
//             return false;
//
//         var firstArg = invocation.ArgumentList.Arguments[0].Expression;
//
//         var constantValue = semanticModel.GetConstantValue(firstArg, cancellationToken);
//         if (!constantValue.HasValue || constantValue.Value is not string s)
//             return false;
//
//         if (!GenAiPrefixes.Any(p => s.StartsWith(p, StringComparison.Ordinal)))
//             return false;
//
//         attributeName = s;
//         return true;
//     }
// }


