// Copyright (c) qyl. All rights reserved.
// Polyfill for IsExternalInit - enables C# 9+ records and init properties on older frameworks.

#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;

#endif
