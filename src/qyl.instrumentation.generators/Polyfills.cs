// =============================================================================
// Polyfills for netstandard2.0 source generators using modern C# features
// These types are normally provided by the runtime but netstandard2.0 lacks them
// =============================================================================

namespace System.Runtime.CompilerServices
{
    /// <summary>Enables init-only setters in netstandard2.0.</summary>
    internal static class IsExternalInit;

    /// <summary>Enables required members in netstandard2.0.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute;

    /// <summary>Enables compiler feature detection in netstandard2.0.</summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        public string FeatureName { get; } = featureName;
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>Enables [NotNullWhen] in netstandard2.0.</summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }

    /// <summary>Enables [NotNullIfNotNull] in netstandard2.0.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
    internal sealed class NotNullIfNotNullAttribute(string parameterName) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }
}
