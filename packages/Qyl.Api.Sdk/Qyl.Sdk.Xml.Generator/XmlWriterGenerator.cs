using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Qyl.Sdk.Xml.Generator;

/// <summary>
/// Implements <c>Qyl.Xml.IXmlWritable</c> on every <c>partial</c> type marked <c>[Qyl.Xml.GenerateXml]</c> by emitting plain
/// <c>XmlWriter</c> calls for the whole element tree; no <c>XmlSerializer</c>, no reflection, nothing left for the trimmer to guess.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class XmlWriterGenerator : IIncrementalGenerator
{
    /// <summary>Tracking name of the parsed-model step; tests assert that an unrelated edit leaves it cached.</summary>
    public const string ModelsStepName = "Models";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(
                XmlModelParser.GenerateXmlAttributeName,
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (attributeContext, cancellationToken) => XmlModelParser.Parse(attributeContext, cancellationToken))
            .WithTrackingName(ModelsStepName);

        context.RegisterSourceOutput(models, static (output, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                output.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (result.Spec is { } spec)
            {
                output.AddSource(spec.HintName, SourceText.From(XmlWriterEmitter.Emit(spec), Encoding.UTF8));
            }
        });
    }
}
