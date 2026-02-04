using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults;

internal sealed class ValidationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            var options = app.ApplicationServices.GetService<QylServiceDefaultsOptions>();
            if (options is not null && !options.MapCalled)
            {
                throw new InvalidOperationException(
                    $"You must call {nameof(QylServiceDefaults.MapQylDefaultEndpoints)}.");
            }

            next(app);
        };
}
