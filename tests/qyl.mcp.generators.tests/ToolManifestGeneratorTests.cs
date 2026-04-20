namespace qyl.mcp.generators.tests;

using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.Mcp.Generators;
using Xunit;

/// <summary>
///     Tests for the ToolManifestGenerator.
///     Verifies that the generator correctly discovers [McpServerToolType] classes
///     and their [McpServerTool] methods, emitting both the Type[] array and
///     the AOT-safe CreateTools factory method.
/// </summary>
public sealed class ToolManifestGeneratorTests
{
    private const string AttributeStubs = """
                                          namespace ModelContextProtocol.Server
                                          {
                                              [System.AttributeUsage(System.AttributeTargets.Class)]
                                              public sealed class McpServerToolTypeAttribute : System.Attribute { }

                                              [System.AttributeUsage(System.AttributeTargets.Method)]
                                              public sealed class McpServerToolAttribute : System.Attribute
                                              {
                                                  public string? Name { get; set; }
                                                  public string? Title { get; set; }
                                                  public bool ReadOnly { get; set; }
                                                  public bool Destructive { get; set; }
                                                  public bool Idempotent { get; set; }
                                                  public bool OpenWorld { get; set; }
                                              }
                                          }

                                          namespace Microsoft.Extensions.AI
                                          {
                                              public abstract class AIFunction { }
                                              public sealed class AIFunctionFactoryOptions { public string? Name { get; set; } }
                                              public static class AIFunctionFactory
                                              {
                                                  public static AIFunction Create(System.Delegate method, AIFunctionFactoryOptions? options = null)
                                                      => throw new System.NotImplementedException();
                                              }
                                          }

                                          namespace Microsoft.Extensions.DependencyInjection
                                          {
                                              public static class ServiceProviderServiceExtensions
                                              {
                                                  public static T GetRequiredService<T>(System.IServiceProvider services)
                                                      => throw new System.NotImplementedException();
                                              }
                                          }
                                          """;

    [Fact]
    public void SingleToolType_WithMethods_EmitsTypeArrayAndCreateTools()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class MyTools
                     {
                         [McpServerTool(Name = "test.greet")]
                         public string Greet(string name) => $"Hello {name}";

                         [McpServerTool(Name = "test.farewell")]
                         public string Farewell(string name) => $"Goodbye {name}";
                     }
                     """;

        var generated = RunGenerator(source);

        Assert.Contains("typeof(global::TestApp.Tools.MyTools)", generated, StringComparison.Ordinal);
        Assert.Contains("svc_TestApp_Tools_MyTools.Greet", generated, StringComparison.Ordinal);
        Assert.Contains("svc_TestApp_Tools_MyTools.Farewell", generated, StringComparison.Ordinal);
        Assert.Contains("Name = \"test.greet\"", generated, StringComparison.Ordinal);
        Assert.Contains("Name = \"test.farewell\"", generated, StringComparison.Ordinal);
        Assert.Contains("CreateTools(", generated, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<global::TestApp.Tools.MyTools>", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void MethodName_UsedAsFallback_WhenAttributeNameIsNull()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class FallbackTools
                     {
                         [McpServerTool]
                         public string DoWork() => "done";
                     }
                     """;

        var generated = RunGenerator(source);

        Assert.Contains("Name = \"DoWork\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void NoToolMethods_SkipsServiceResolution()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class EmptyTools
                     {
                         public string NotATool() => "nope";
                     }
                     """;

        var generated = RunGenerator(source);

        Assert.Contains("typeof(global::TestApp.Tools.EmptyTools)", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRequiredService<global::TestApp.Tools.EmptyTools>", generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedFile_IsExcluded()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class GeneratedTools
                     {
                         [McpServerTool(Name = "gen.tool")]
                         public string GenTool() => "gen";
                     }
                     """;

        var generated = RunGenerator(source, "GeneratedTools.g.cs");

        Assert.Empty(generated);
    }

    [Fact]
    public void MultipleClasses_DeterministicOrdering()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class ZetaTools
                     {
                         [McpServerTool(Name = "zeta.go")]
                         public string Go() => "z";
                     }

                     [McpServerToolType]
                     public sealed class AlphaTools
                     {
                         [McpServerTool(Name = "alpha.go")]
                         public string Go() => "a";
                     }
                     """;

        var generated = RunGenerator(source);

        var alphaIndex = generated.IndexOf("AlphaTools", StringComparison.Ordinal);
        var zetaIndex = generated.IndexOf("ZetaTools", StringComparison.Ordinal);
        Assert.True(alphaIndex < zetaIndex, "AlphaTools should appear before ZetaTools (ordered by FQN)");
    }

    [Fact]
    public void FilterParameter_EmittedOnCreateTools()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class FilterableTools
                     {
                         [McpServerTool(Name = "f.tool")]
                         public string Tool() => "t";
                     }
                     """;

        var generated = RunGenerator(source);

        Assert.Contains("filter?.Invoke(typeof(global::TestApp.Tools.FilterableTools)) != false", generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StaticMethods_AreExcluded()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class MixedTools
                     {
                         [McpServerTool(Name = "instance.tool")]
                         public string InstanceTool() => "i";

                         [McpServerTool(Name = "static.tool")]
                         public static string StaticTool() => "s";
                     }
                     """;

        var generated = RunGenerator(source);

        Assert.Contains("Name = \"instance.tool\"", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Name = \"static.tool\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateMethods_AreExcluded()
    {
        var source = """
                     using ModelContextProtocol.Server;

                     namespace TestApp.Tools;

                     [McpServerToolType]
                     public sealed class AccessTools
                     {
                         [McpServerTool(Name = "pub.tool")]
                         public string PublicTool() => "p";

                         [McpServerTool(Name = "priv.tool")]
                         private string PrivateTool() => "v";
                     }
                     """;

        var generated = RunGenerator(source);

        Assert.Contains("Name = \"pub.tool\"", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Name = \"priv.tool\"", generated, StringComparison.Ordinal);
    }

    private static string RunGenerator(string source, string filePath = "Test.cs")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: filePath);
        var stubTree = CSharpSyntaxTree.ParseText(AttributeStubs, path: "Stubs.cs");

        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        TryAddReference(references, Path.Combine(runtimeDir, "System.Runtime.dll"));
        TryAddReference(references, Path.Combine(runtimeDir, "netstandard.dll"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree, stubTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ToolManifestGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var result = driver.RunGenerators(compilation).GetRunResult();

        var generatedSource = result.GeneratedTrees
            .FirstOrDefault(static t => t.FilePath.Contains("QylToolManifest", StringComparison.Ordinal));

        return generatedSource?.GetText().ToString() ?? string.Empty;
    }

    // Probe optional runtime reference without System.IO.File — RS1035 bans File in
    // projects pulling in Microsoft.CodeAnalysis.Analyzers via the generator ref.
    // MetadataReference.CreateFromFile throws FileNotFoundException on missing path;
    // that's expected when the SDK layout differs across hosts.
    private static void TryAddReference(List<MetadataReference> references, string path)
    {
        try
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }
        catch (FileNotFoundException ex)
        {
            Trace.WriteLine($"optional ref skipped: {path} ({ex.Message})");
        }
    }
}
