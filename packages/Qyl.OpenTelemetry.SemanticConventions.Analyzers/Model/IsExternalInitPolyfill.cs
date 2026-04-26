// Copyright (c) 2025-2026 ancplua

// Polyfill required by C# records + init-only setters on netstandard2.0 —
// the type exists in net5+ BCL, not in netstandard2.0. Declaring it in an
// internal namespace is the compiler-sanctioned pattern.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
