namespace Qyl.Instrumentation;

using Instrumentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

/// <summary>
///     Generator-facing entry points. The <c>qyl.instrumentation.generators</c> interceptor wraps every
///     <c>builder.Build()</c> call site to emit <c>builder.TryUseQylConventions()</c> and
///     <c>app.MapQylDefaultEndpoints()</c>, so author code never has to call them directly.
///     The real surface lives in <see cref="QylServiceDefaultsExtensions" />.
/// </summary>
public static class QylServiceDefaults
{
    /// <summary>Generator-facing alias for <see cref="QylServiceDefaultsExtensions.UseQyl" />.</summary>
    public static TBuilder TryUseQylConventions<TBuilder>(
        this TBuilder builder,
        Action<QylOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.UseQyl(configure);
        return builder;
    }

    /// <summary>Generator-facing alias for <see cref="QylServiceDefaultsExtensions.MapQylEndpoints" />.</summary>
    public static WebApplication MapQylDefaultEndpoints(this WebApplication app) => app.MapQylEndpoints();
}
