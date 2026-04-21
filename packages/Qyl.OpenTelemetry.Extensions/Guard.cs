// Copyright (c) 2025-2026 ancplua

namespace Qyl.OpenTelemetry.Extensions;

/// <summary>
/// Minimal null-guard helper shared across the package. Kept internal so consumers can't collide
/// with it; avoids pulling in a runtime polyfill package just for a single parameter check
/// (ArgumentNullException.ThrowIfNull is net6.0+, so it's not usable in our netstandard2.0 target).
/// </summary>
internal static class Guard
{
    public static void NotNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
