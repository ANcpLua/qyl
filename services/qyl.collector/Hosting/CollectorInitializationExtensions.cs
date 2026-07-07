using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Cost;

namespace Qyl.Collector.Hosting;

internal static class CollectorInitializationExtensions
{
    public static async Task InitializeQylCollectorAsync(this WebApplication app)
    {
        await app.Services.GetRequiredService<ModelPricingService>().InitializeAsync().ConfigureAwait(false);
    }
}
