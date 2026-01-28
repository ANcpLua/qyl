namespace System.Runtime.CompilerServices;

/// <summary>
///     Backport of <see cref="IsExternalInit" /> for .NET Standard 2.0/2.1 and .NET Framework.
/// </summary>
/// <remarks>
///     <para>
///         This type is only defined on older target frameworks and is automatically
///         available on .NET 5+ through the standard library.
///     </para>
///     <para>
///         The C# compiler requires this type to exist when using C# 9+ features such as:
///         <list type="bullet">
///             <item>
///                 <description>Init-only properties (<c>init</c> accessor)</description>
///             </item>
///             <item>
///                 <description>Record types (<c>record class</c> and <c>record struct</c>)</description>
///             </item>
///         </list>
///     </para>
///     <para>
///         The type itself is empty; its mere existence signals to the compiler that
///         init-only semantics are supported.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     // This syntax requires IsExternalInit to be defined:
///     public class Person
///     {
///         public string Name { get; init; }
///     }
/// 
///     // Records also require this type:
///     public record Point(int X, int Y);
///     </code>
/// </example>
internal static class IsExternalInit
{
}
