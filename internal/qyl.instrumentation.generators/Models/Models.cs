using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.Instrumentation.Generators.Models;

#region Composition Definitions

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
