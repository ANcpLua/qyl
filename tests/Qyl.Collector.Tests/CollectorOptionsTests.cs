using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Qyl.Collector.Analysis;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Retention;
using Keys = Qyl.Collector.Ingestion.Configuration.CollectorConfigurationKeys;

namespace Qyl.Collector.Tests;

/// <summary>
/// The Collector's options bind from the configuration keys the Collector has always read, which are
/// also its environment variable names, and an invalid value stops the host when it starts.
/// </summary>
[DoNotParallelize]
[TestClass]
public sealed class CollectorOptionsTests(TestContext testContext)
{
    private const string SigningKey = "analysis-options-test-key-of-forty-bytes";
    private const string ProjectKeysJson = """{"alpha":[{"key":"a1","roles":["ingest","read"]}]}""";

    [TestMethod]
    public void CollectorPortOptions_ValidConfiguration_BindsTheConfiguredValues()
    {
        var ports = Bind<CollectorPortOptions>(new Dictionary<string, string>
        {
            [Keys.BindAddress] = "0.0.0.0",
            [Keys.Port] = "6100",
            [Keys.PlatformPort] = "8080",
            [Keys.OtlpPort] = "0",
            [Keys.GrpcPort] = "6317"
        });

        Assert.AreEqual((IPAddress.Any, 6100, 0, 6317), (ports.ParseBindAddress(), ports.Http, ports.OtlpHttp, ports.Grpc));
    }

    [TestMethod]
    public void CollectorPortOptions_OnlyPlatformPort_UsesItAsTheApiPort()
    {
        var ports = Bind<CollectorPortOptions>(new Dictionary<string, string> { [Keys.PlatformPort] = "8080" });

        Assert.AreEqual(8080, ports.Http);
    }

    [TestMethod]
    public async Task CollectorPortOptions_PortOutOfRange_FailsStartNamingTheKeyAndValue()
    {
        using var host = StartableHost(new Dictionary<string, string> { [Keys.OtlpPort] = "70000" });

        var failure = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync(testContext.CancellationToken));

        StringAssert.Contains(failure.Message, $"{Keys.OtlpPort} must be between 0 and 65535, but was '70000'.");
    }

    [TestMethod]
    public async Task CollectorPortOptions_BindAddressNotAnIp_FailsStartNamingTheKeyAndValue()
    {
        using var host = StartableHost(new Dictionary<string, string> { [Keys.BindAddress] = "localhost" });

        var failure = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync(testContext.CancellationToken));

        StringAssert.Contains(failure.Message, $"{Keys.BindAddress} must be an IP address literal, but was 'localhost'.");
    }

    [TestMethod]
    public void CollectorPortOptions_EnvironmentVariables_BindUnderTheirNames()
    {
        using var environment = new EnvironmentVariableScope(new Dictionary<string, string>
        {
            [Keys.BindAddress] = "0.0.0.0",
            [Keys.Port] = "6100",
            [Keys.OtlpPort] = "6318",
            [Keys.GrpcPort] = "6317"
        });

        var ports = BindFromEnvironment<CollectorPortOptions>();

        Assert.AreEqual((IPAddress.Any, 6100, 6318, 6317), (ports.ParseBindAddress(), ports.Http, ports.OtlpHttp, ports.Grpc));
    }

    [TestMethod]
    public void OtlpCorsOptions_ValidConfiguration_BindsTheConfiguredValues()
    {
        var cors = Bind<OtlpCorsOptions>(new Dictionary<string, string>
        {
            [Keys.CorsAllowedOrigins] = "https://console.example",
            [Keys.CorsAllowedHeaders] = "x-trace"
        });

        Assert.AreEqual(("https://console.example", "x-trace"), (cors.AllowedOrigins, cors.AllowedHeaders));
    }

    [TestMethod]
    public void OtlpCorsOptions_EnvironmentVariables_BindUnderTheirNames()
    {
        using var environment = new EnvironmentVariableScope(new Dictionary<string, string>
        {
            [Keys.CorsAllowedOrigins] = "https://console.example",
            [Keys.CorsAllowedHeaders] = "x-trace"
        });

        var cors = BindFromEnvironment<OtlpCorsOptions>();

        Assert.AreEqual(("https://console.example", "x-trace"), (cors.AllowedOrigins, cors.AllowedHeaders));
    }

    [TestMethod]
    public void OtlpApiKeyOptions_ValidConfiguration_BindsModeAndParsedKeys()
    {
        var apiKeys = Bind<OtlpApiKeyOptions>(new Dictionary<string, string>
        {
            [Keys.AuthMode] = OtlpApiKeyOptions.ApiKeyMode,
            [Keys.ProjectKeys] = ProjectKeysJson
        });

        Assert.AreEqual(
            (true, new ProjectApiKey("alpha", "a1", ApiKeyRoles.Ingest | ApiKeyRoles.Read)),
            (apiKeys.IsApiKeyMode, Assert.ContainsSingle(apiKeys.Keys)));
    }

    [TestMethod]
    public void OtlpApiKeyOptions_NoAuthModeInProduction_DefaultsToApiKeyMode()
    {
        var apiKeys = Bind<OtlpApiKeyOptions>(new Dictionary<string, string> { [Keys.ProjectKeys] = ProjectKeysJson });

        Assert.IsTrue(apiKeys.IsApiKeyMode);
    }

    [TestMethod]
    public async Task OtlpApiKeyOptions_UnknownAuthMode_FailsStartNamingTheKeyAndValue()
    {
        using var host = StartableHost(new Dictionary<string, string> { [Keys.AuthMode] = "Open" });

        var failure = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync(testContext.CancellationToken));

        StringAssert.Contains(failure.Message, $"{Keys.AuthMode} must be one of ApiKey, Unsecured, but was 'Open'.");
    }

    [TestMethod]
    public void OtlpApiKeyOptions_EnvironmentVariables_BindUnderTheirNames()
    {
        using var environment = new EnvironmentVariableScope(new Dictionary<string, string>
        {
            [Keys.AuthMode] = OtlpApiKeyOptions.ApiKeyMode,
            [Keys.ProjectKeys] = ProjectKeysJson
        });

        var apiKeys = BindFromEnvironment<OtlpApiKeyOptions>();

        Assert.AreEqual(("alpha", true), (Assert.ContainsSingle(apiKeys.Keys).ProjectId, apiKeys.IsApiKeyMode));
    }

    [TestMethod]
    public void RetentionOptions_ValidConfiguration_BindsTheConfiguredValues()
    {
        var retention = Bind<RetentionOptions>(new Dictionary<string, string>
        {
            [Keys.RetentionDays] = "7",
            [Keys.RetentionIntervalMinutes] = "15",
            [Keys.StorageMinimumFreeMegabytes] = "4096"
        });

        Assert.AreEqual((7, TimeSpan.FromMinutes(15), 4096L * 1024L * 1024L),
            (retention.Days, retention.Interval, retention.StorageMinimumFreeBytes));
    }

    [TestMethod]
    public async Task RetentionOptions_ZeroInterval_FailsStartNamingTheKeyAndValue()
    {
        using var host = StartableHost(new Dictionary<string, string> { [Keys.RetentionIntervalMinutes] = "0" });

        var failure = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync(testContext.CancellationToken));

        StringAssert.Contains(failure.Message, $"{Keys.RetentionIntervalMinutes} must be between 1 and {int.MaxValue}, but was '0'.");
    }

    [TestMethod]
    public void RetentionOptions_EnvironmentVariables_BindUnderTheirNames()
    {
        using var environment = new EnvironmentVariableScope(new Dictionary<string, string>
        {
            [Keys.RetentionDays] = "7",
            [Keys.RetentionIntervalMinutes] = "15",
            [Keys.StorageMinimumFreeMegabytes] = "4096"
        });

        var retention = BindFromEnvironment<RetentionOptions>();

        Assert.AreEqual((7, 15, 4096L), (retention.Days, retention.IntervalMinutes, retention.StorageMinimumFreeMegabytes));
    }

    [TestMethod]
    public void CollectorStorageOptions_ValidConfiguration_BindsTheConfiguredValues()
    {
        var storage = Bind<CollectorStorageOptions>(new Dictionary<string, string>
        {
            [Keys.DataPath] = "data/qyl.duckdb",
            [Keys.DatabaseMemoryLimit] = "512MB",
            [Keys.DatabaseThreads] = "2",
            [Keys.DatabaseTempDirectory] = "data/tmp"
        });

        Assert.AreEqual(("data/qyl.duckdb", "512MB", (int?)2, "data/tmp"),
            (storage.DataPath, storage.MemoryLimit, storage.Threads, storage.TempDirectory));
    }

    [TestMethod]
    public async Task CollectorStorageOptions_ZeroThreads_FailsStartNamingTheKeyAndValue()
    {
        using var host = StartableHost(new Dictionary<string, string> { [Keys.DatabaseThreads] = "0" });

        var failure = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync(testContext.CancellationToken));

        StringAssert.Contains(failure.Message, $"{Keys.DatabaseThreads} must be between 1 and {int.MaxValue}, but was '0'.");
    }

    [TestMethod]
    public void CollectorStorageOptions_EnvironmentVariables_BindUnderTheirNames()
    {
        using var environment = new EnvironmentVariableScope(new Dictionary<string, string>
        {
            [Keys.DataPath] = "data/qyl.duckdb",
            [Keys.DatabaseMemoryLimit] = "512MB",
            [Keys.DatabaseThreads] = "2",
            [Keys.DatabaseTempDirectory] = "data/tmp"
        });

        var storage = BindFromEnvironment<CollectorStorageOptions>();

        Assert.AreEqual(("data/qyl.duckdb", "512MB", (int?)2, "data/tmp"),
            (storage.DataPath, storage.MemoryLimit, storage.Threads, storage.TempDirectory));
    }

    [TestMethod]
    public void AnalysisOptions_ValidConfiguration_BindsTheSigningKey()
    {
        var analysis = Bind<AnalysisOptions>(new Dictionary<string, string> { [Keys.AnalysisSigningKey] = SigningKey });

        Assert.AreEqual(SigningKey, analysis.SigningKey);
    }

    [TestMethod]
    public async Task AnalysisOptions_ShortSigningKey_FailsStartNamingTheKeyAndLength()
    {
        using var host = StartableHost(new Dictionary<string, string> { [Keys.AnalysisSigningKey] = "short" });

        var failure = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync(testContext.CancellationToken));

        StringAssert.Contains(failure.Message, $"{Keys.AnalysisSigningKey} must contain at least 32 bytes, but has 5.");
    }

    [TestMethod]
    public void AnalysisOptions_EnvironmentVariables_BindUnderTheirNames()
    {
        using var environment = new EnvironmentVariableScope(new Dictionary<string, string> { [Keys.AnalysisSigningKey] = SigningKey });

        var analysis = BindFromEnvironment<AnalysisOptions>();

        Assert.AreEqual(SigningKey, analysis.SigningKey);
    }

    private static T Bind<T>(IReadOnlyDictionary<string, string> values) where T : class
    {
        using var host = BuildHost(new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build());
        return host.Services.GetRequiredService<IOptions<T>>().Value;
    }

    private static T BindFromEnvironment<T>() where T : class
    {
        using var host = BuildHost(new ConfigurationBuilder().AddEnvironmentVariables().Build());
        return host.Services.GetRequiredService<IOptions<T>>().Value;
    }

    private static IHost StartableHost(IReadOnlyDictionary<string, string> values) => BuildHost(StartableConfiguration(values));

    private static IConfiguration StartableConfiguration(IReadOnlyDictionary<string, string> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [Keys.AuthMode] = OtlpApiKeyOptions.UnsecuredMode,
                [Keys.DataPath] = ":memory:"
            })
            .AddInMemoryCollection(values.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

    private static IHost BuildHost(IConfiguration configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Services.AddQylCollectorCore(configuration);
        builder.Services.AddQylCollectorStorage(configuration);
        builder.Services.AddQylCollectorAuth(configuration, builder.Environment);
        return builder.Build();
    }
}
