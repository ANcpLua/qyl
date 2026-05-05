using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Qyl.Instrumentation.Instrumentation;

namespace Qyl.Instrumentation;

public static class QylServiceDefaults
{
    public static TBuilder TryUseQylConventions<TBuilder>(
        this TBuilder builder,
        Action<QylOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.UseQyl(configure);
        return builder;
    }

    public static WebApplication MapQylDefaultEndpoints(this WebApplication app) => app.MapQylEndpoints();
}
