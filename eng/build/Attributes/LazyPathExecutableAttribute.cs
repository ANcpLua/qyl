// Extracted from open-telemetry/opentelemetry-dotnet-instrumentation (Apache-2.0).

using System;
using System.Reflection;
using Nuke.Common.Tooling;
using Nuke.Common.ValueInjection;

namespace Qyl.Build;

/// <summary>
///     Injects a delegate for process execution. The executable name is derived from the member name or can be
///     passed as constructor argument. Unlike <c>[PathVariable]</c>, resolution is deferred until first use, so
///     a build whose targets never touch the tool does not require it on PATH. The path is resolved in order:
///     <ul>
///         <li>From environment variables (e.g., <c>[NAME]_EXE=path</c>)</li>
///         <li>From the PATH variable using <c>which</c> or <c>where</c></li>
///     </ul>
/// </summary>
/// <example>
///     <code>
/// [LazyPathExecutable(name: "echo")] readonly Lazy&lt;Tool&gt; Echo;
/// Target FooBar => _ => _
///     .Executes(() =>
///     {
///         var output = Echo.Value(arguments: "test");
///     });
///     </code>
/// </example>
public sealed class LazyPathExecutableAttribute : ValueInjectionAttributeBase
{
    private readonly string? _name;

    public LazyPathExecutableAttribute(string? name = null)
    {
        _name = name;
    }

    public override object GetValue(MemberInfo member, object instance)
    {
        var name = _name ?? member.Name;
        return new Lazy<Tool>(() => ToolResolver.TryGetEnvironmentTool(name) ??
                                    ToolResolver.GetPathTool(name));
    }
}
