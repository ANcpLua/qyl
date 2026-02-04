# ANcpLua.Roslyn.Utilities.Testing Reference

Test infrastructure for Roslyn analyzers, code fixes, and refactorings.

## Base Classes

### AnalyzerTest<TAnalyzer>

For testing diagnostic analyzers.

```csharp
public sealed class Al0001Tests : AnalyzerTest<Al0001Analyzer>
{
    [Theory]
    [InlineData("int i", "[|i|] = 10")]
    public Task ShouldReport(string param, string stmt) =>
        VerifyAsync($"public class C({param}) {{ void M() {{ {stmt}; }} }}");

    [Fact]
    public Task ShouldNotReport() =>
        VerifyAsync("public class C { void M() { } }");
}
```

### CodeFixTest<TAnalyzer, TCodeFix>

For testing code fix providers.

```csharp
public sealed class Al0001CodeFixTests : CodeFixTest<Al0001Analyzer, Al0001CodeFixProvider>
{
    [Fact]
    public Task ShouldFix() =>
        VerifyAsync(
            input: "class C([|int i|]) { }",
            output: "class C(int i) { void M() => _ = i; }");
}
```

### RefactoringTest<TRefactoring>

For testing code refactorings.

```csharp
public sealed class Ar0001Tests : RefactoringTest<Ar0001Refactoring>
{
    [Fact]
    public Task ShouldRefactor() =>
        VerifyAsync(
            input: "class C { [||]void M() { } }",
            output: "class C { public void M() { } }");
}
```

### SolutionRefactoringTest<TRefactoring>

For refactorings that affect multiple files.

```csharp
public sealed class Ar0002Tests : SolutionRefactoringTest<Ar0002Refactoring>
{
    [Fact]
    public Task ShouldRefactorAcrossFiles() =>
        VerifyAsync(
            input: new[]
            {
                ("File1.cs", "[||]partial class C { }"),
                ("File2.cs", "partial class C { }")
            },
            output: new[]
            {
                ("File1.cs", "partial class C { void M() { } }"),
                ("File2.cs", "partial class C { }")
            });
}
```

---

## Diagnostic Markers

### Span markers

| Marker | Meaning |
|--------|---------|
| `[|code|]` | Diagnostic expected on this span |
| `[||]` | Cursor position (for refactorings) |
| `{|DiagnosticId:code|}` | Diagnostic with specific ID |
| `{|AL0001:code|}` | Example with specific diagnostic |

### Examples

```csharp
// Expect diagnostic on 'i' assignment
"[|i|] = 10"

// Cursor for refactoring
"[||]void Method()"

// Specific diagnostic ID
"{|AL0001:value|} = 10"

// Multiple diagnostics
"[|a|] = [|b|] = 10"
```

---

## Patterns to Find

### Not using base classes

```csharp
// Before - manual test setup
public class MyTests
{
    [Fact]
    public async Task Test()
    {
        var source = "...";
        var compilation = CreateCompilation(source);
        var diagnostics = await GetDiagnostics(compilation);
        Assert.Single(diagnostics);
    }
}

// After - use base class
public class MyTests : AnalyzerTest<MyAnalyzer>
{
    [Fact]
    public Task Test() => VerifyAsync("...[|expected|]...");
}
```

### Manual diagnostic assertion

```csharp
// Before
diagnostics.Should().HaveCount(1);
diagnostics[0].Id.Should().Be("AL0001");

// After
VerifyAsync("code with [|markers|]")
```

### Complex test setup

```csharp
// Before - lots of boilerplate
var workspace = new AdhocWorkspace();
var project = workspace.AddProject("Test", "C#");
var document = project.AddDocument("Test.cs", source);
// ... more setup

// After - base class handles it
VerifyAsync(source)
```

---

## Method Reference

### VerifyAsync

```csharp
// Single source file
Task VerifyAsync(string source)

// With specific diagnostics
Task VerifyAsync(string source, params DiagnosticResult[] expected)

// Code fix verification
Task VerifyAsync(string input, string output)

// Multi-file refactoring
Task VerifyAsync(
    (string fileName, string content)[] input,
    (string fileName, string content)[] output)
```

### Configuration

Override virtual properties:

```csharp
protected override string Language => LanguageNames.CSharp;
protected override ReferenceAssemblies ReferenceAssemblies => ReferenceAssemblies.Net.Net80;
protected override string DefaultFileExt => ".cs";
```

---

## Improvement Opportunities

| Current Pattern | Improvement |
|----------------|-------------|
| Manual compilation setup | Use `AnalyzerTest<T>` |
| Assert on diagnostic count/ID | Use markers `[|...|]` |
| Custom test helper methods | Inherit from base classes |
| Repeated source templates | Use `[Theory]` with `[InlineData]` |
| Complex workspace setup | Base class handles it |
