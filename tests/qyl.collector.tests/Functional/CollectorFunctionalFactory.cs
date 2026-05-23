using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Qyl.Collector.Tests.Functional;

public abstract class CollectorFunctionalFactory(string dataPathPrefix) : WebApplicationFactory<Program>
{
    private readonly string _dataPath = $":memory:qyl-{dataPathPrefix}-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QYL_DATA_PATH"] = _dataPath,
                ["QYL_OTLP_AUTH_MODE"] = "Unsecured"
            });
        });
    }
}
