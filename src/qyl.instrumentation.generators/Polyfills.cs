// Polyfills required for language features on netstandard2.0.
// These must live in a separate file because file-scoped namespaces cannot follow block namespaces.
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
