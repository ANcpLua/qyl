---
name: xml-doc-generator
description: |
  Generate professional XML documentation comments for C# code following established patterns. Use when documenting public APIs, adding missing docs, or when CS1591 warnings appear.
---

## Source Metadata

```yaml
# none
```


# XML Documentation Generator

Generate high-quality XML documentation comments for C# public APIs.

## Triggers

Use this skill when:
- User asks to "document", "add docs", or "add XML comments"
- CS1591 warnings appear (missing XML documentation)
- Creating new public types/members
- Improving existing sparse documentation

## Documentation Style Guide

### Class/Type Level

```csharp
/// <summary>
///     Primary purpose in one sentence.
///     <para>
///         Additional context about usage patterns or design rationale.
///     </para>
/// </summary>
/// <typeparam name="T">What T represents and any constraints.</typeparam>
/// <remarks>
///     <list type="bullet">
///         <item>
///             <description><b>Key behavior.</b> Detailed explanation.</description>
///         </item>
///         <item>
///             <description><b>Another point.</b> More details.</description>
///         </item>
///     </list>
/// </remarks>
```

### Method Level

```csharp
/// <summary>
///     What this method does (imperative: "Validates", "Creates", "Returns").
/// </summary>
/// <typeparam name="TResult">What the result type represents.</typeparam>
/// <param name="input">What this parameter is used for.</param>
/// <param name="options">Configuration options (can be null for defaults).</param>
/// <returns>Description of return value and when it's returned.</returns>
/// <exception cref="ArgumentNullException">When <paramref name="input"/> is null.</exception>
/// <exception cref="InvalidOperationException">When state is invalid.</exception>
/// <seealso cref="RelatedMethod"/>
```

### Property Level

```csharp
/// <summary>Gets the current value.</summary>
/// <summary>Gets or sets the configuration.</summary>
/// <value>The default is <c>null</c>.</value>
```

### Extension Methods

```csharp
/// <summary>
///     Converts <paramref name="value"/> to an <see cref="ErrorOr{TValue}"/> instance.
/// </summary>
/// <param name="value">The value to wrap.</param>
/// <returns>An <see cref="ErrorOr{TValue}"/> containing <paramref name="value"/>.</returns>
```

### Delegation (inheritdoc)

```csharp
/// <inheritdoc cref="IInterface.Method"/>
/// <inheritdoc cref="BaseClass.Method(string)"/>
```

## Decision Rules

| Scenario | Documentation Approach |
|----------|----------------------|
| Simple getter property | One-line `<summary>Gets the X.</summary>` |
| Complex method | Full summary + params + returns + exceptions |
| Obvious extension method | Brief summary + param + returns |
| Generic type parameter | Always document with `<typeparam>` |
| Nullable parameter | Note "can be null" or default behavior |
| Async method | Mention "A task that completes when..." in returns |
| Factory method | Note what gets created and configuration |
| Validation method | Document what's validated and failure modes |

## Cross-Reference Patterns

```csharp
// Link to type
<see cref="DiagnosticFlow{T}"/>

// Link to method with generics (escape angle brackets)
<see cref="Then{TNext}(Func{T, DiagnosticFlow{TNext}})"/>

// Link to property
<see cref="Value"/>

// Parameter reference in description
<paramref name="input"/>

// Type parameter reference
<typeparamref name="T"/>

// Literal value
<c>null</c>, <c>true</c>, <c>0</c>

// Language keyword
<see langword="null"/>, <see langword="true"/>
```

## Quality Checklist

Before marking documentation complete:

- [ ] Every public type has `<summary>`
- [ ] Every public method has `<summary>`, `<param>`, `<returns>`
- [ ] Every generic parameter has `<typeparam>`
- [ ] Nullable parameters note null behavior
- [ ] Exceptions documented with `<exception cref="...">`
- [ ] Related methods linked with `<seealso>`
- [ ] No "Gets or sets" on read-only properties (use "Gets")
- [ ] Async methods describe task completion
- [ ] No CS1591 warnings remain

## Process

1. **Read the file** to understand the code structure
2. **Identify public API surface** (public/protected members without docs)
3. **Document types first** (class, struct, interface, enum)
4. **Document members** in declaration order
5. **Add cross-references** to related APIs
6. **Build and verify** no CS1591 warnings remain

## Example Transformation

**Before:**
```csharp
public DiagnosticFlow<TNext> Then<TNext>(Func<T, DiagnosticFlow<TNext>> next)
{
    if (IsFailed || Value is null)
        return new DiagnosticFlow<TNext>(default, Diagnostics);
    var result = next(Value);
    return new DiagnosticFlow<TNext>(result.Value,
        DiagnosticFlowHelpers.MergeDiagnostics(Diagnostics, result.Diagnostics));
}
```

**After:**
```csharp
/// <summary>
///     Chains a transformation that produces a new <see cref="DiagnosticFlow{T}"/>,
///     merging diagnostics from both operations.
/// </summary>
/// <typeparam name="TNext">The result type of the chained operation.</typeparam>
/// <param name="next">
///     A function that transforms the current value into a new flow.
///     Only invoked if the current flow is successful.
/// </param>
/// <returns>
///     A new <see cref="DiagnosticFlow{TNext}"/> containing the transformed value
///     and merged diagnostics, or a failed flow if this flow has errors.
/// </returns>
/// <seealso cref="Select{TNext}(Func{T, TNext})"/>
public DiagnosticFlow<TNext> Then<TNext>(Func<T, DiagnosticFlow<TNext>> next)
```

## Common Patterns by Type

### Record Types
```csharp
/// <summary>Represents a validation result with associated metadata.</summary>
/// <param name="IsValid">Whether validation passed.</param>
/// <param name="Errors">Collection of validation errors, empty if valid.</param>
public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);
```

### Interface Methods
```csharp
/// <summary>Processes the input and returns the result.</summary>
/// <param name="input">The input to process.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A task containing the processed result.</returns>
Task<TResult> ProcessAsync(TInput input, CancellationToken cancellationToken = default);
```

### Enum Values
```csharp
/// <summary>Specifies the severity level of a diagnostic.</summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational message, no action required.</summary>
    Info,
    /// <summary>Warning that should be reviewed.</summary>
    Warning,
    /// <summary>Error that must be resolved.</summary>
    Error
}
```
