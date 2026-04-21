namespace Qyl.Instrumentation.Generators.Analyzers;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
///     QYL0137 — Flags direct instantiation of provider SDK clients
///     (<c>OpenAIClient</c>, <c>AzureOpenAIClient</c>, <c>AnthropicClient</c>, <c>OllamaApiClient</c>)
///     from any file that is not a sanctioned composition root
///     (<c>**/Clients/*ChatClientBuilder.cs</c> or <c>**/Factories/*ChatClientFactory.cs</c>).
///     Exempt under <c>tests/**</c> and <c>samples/**</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ChatClientBuilderBypassAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QYL0137";

    private static readonly string[] SProviderClients =
    [
        "OpenAI.OpenAIClient",
        "Azure.AI.OpenAI.AzureOpenAIClient",
        "Anthropic.AnthropicClient",
        "OllamaSharp.OllamaApiClient"
    ];

    private static readonly DiagnosticDescriptor SRule = new(
        DiagnosticId,
        "Provider SDK client instantiated outside ChatClientBuilder",
        "Provider SDK clients may only be instantiated inside an `IXxxChatClientBuilder` implementation. " +
        "Resolve `IXxxChatClientBuilder` from DI and call `.BuildChatClient(provider)` instead.",
        "Qyl.Instrumentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var name in SProviderClients)
                if (start.Compilation.GetTypeByMetadataName(name) is { } symbol)
                    builder.Add(symbol);

            if (builder.Count is 0)
                return;

            var providers = builder.ToImmutable();
            start.RegisterOperationAction(ctx => Analyze(ctx, providers), OperationKind.ObjectCreation);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        ImmutableHashSet<INamedTypeSymbol> providers)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not INamedTypeSymbol created || !providers.Contains(created))
            return;

        if (IsAllowed(context.Operation.Syntax.SyntaxTree.FilePath))
            return;

        context.ReportDiagnostic(Diagnostic.Create(SRule, creation.Syntax.GetLocation()));
    }

    private static bool IsAllowed(string path)
    {
        // Wrap so startsWith-tests paths (e.g. `tests/foo.cs`) match `/tests/` too.
        var normalized = "/" + path.Replace('\\', '/');
        if (normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/samples/", StringComparison.OrdinalIgnoreCase))
            return true;

        return (normalized.Contains("/Clients/", StringComparison.Ordinal) &&
                normalized.EndsWithOrdinal("ChatClientBuilder.cs")) ||
               (normalized.Contains("/Factories/", StringComparison.Ordinal) &&
                normalized.EndsWithOrdinal("ChatClientFactory.cs"));
    }
}
