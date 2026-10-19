using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.Instrumentation.Generators.Models;

#region ASP.NET Builder Call Site Types

internal sealed record BuilderCallSite(
    string SortKey,
    InterceptableLocation Location);

#endregion
