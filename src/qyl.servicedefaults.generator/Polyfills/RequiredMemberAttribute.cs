using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>Specifies that a type has required members or that a member is required.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class RequiredMemberAttribute : Attribute;
