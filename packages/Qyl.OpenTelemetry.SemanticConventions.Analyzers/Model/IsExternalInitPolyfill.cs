// Copyright (c) 2025-2026 ancplua

// Polyfill required by C# records + init-only setters on netstandard2.0 —
// the compiler resolves IsExternalInit by its fully qualified name, so the
// type must live in System.Runtime.CompilerServices.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
