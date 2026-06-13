using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.Instrumentation.Generators.Models;

#region Composition Definitions

internal sealed record HostedServiceDefinition(
    string TypeFullyQualifiedName,
    string SortKey);

internal sealed record MapEndpointsDefinition(
    string ContainingTypeFullyQualifiedName,
    string MethodName,
    int Order,
    string SortKey);

internal sealed record QylServiceDefinition(
    string TypeFullyQualifiedName,
    string LifetimeMethodName,
    string? InterfaceFullyQualifiedName,
    string SortKey);

internal sealed record QylHealthCheckDefinition(
    string TypeFullyQualifiedName,
    string Name,
    EquatableArray<string> Tags,
    string SortKey);

#endregion

#region ASP.NET Builder Call Site Types

internal enum BuilderCallKind
{
    Build
}

internal sealed record BuilderCallSite(
    string SortKey,
    BuilderCallKind Kind,
    InterceptableLocation Location);

#endregion

#region Database Call Site Types

internal enum DbCommandMethod
{
    ExecuteReader,
    ExecuteNonQuery,
    ExecuteScalar
}

/// <summary>
/// Compile-time sampling decision for a single db call site, resolved from
/// <c>[QylNoTrace]</c>/<c>[QylSample]</c> on the enclosing method/type/assembly.
/// </summary>
internal enum SamplingMode
{
    /// <summary>Normal instrumented path; the runtime sampler decides.</summary>
    Always,

    /// <summary>Compile-time drop: emit a pass-through to the raw ADO.NET call, no instrumentation.</summary>
    Never,

    /// <summary>Emit a deterministic trace-id gate at <see cref="DbCallSite.SampleRatio"/> before instrumenting.</summary>
    Ratio
}

internal sealed record DbCallSite(
    string SortKey,
    DbCommandMethod Method,
    bool IsAsync,
    string? ConcreteCommandType,
    SamplingMode Sampling,
    double SampleRatio,
    InterceptableLocation Location);

#endregion
