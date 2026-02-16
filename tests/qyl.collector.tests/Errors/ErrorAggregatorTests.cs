using qyl.collector.Errors;
using qyl.collector.Storage;

namespace qyl.collector.tests.Errors;

public sealed class ErrorAggregatorTests : IAsyncLifetime
{
    private DuckDbStore? _store;
    private DuckDbStore Store => _store ?? throw new InvalidOperationException("Store not initialized");

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public ValueTask DisposeAsync()
    {
        return _store?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    [Fact]
    public async Task UpsertError_NewFingerprint_InsertsRow()
    {
        var error = new ErrorEvent
        {
            ErrorType = "NullReferenceException",
            Message = "Object reference not set",
            Category = "internal",
            Fingerprint = "abc123",
            ServiceName = "my-api",
            TraceId = "trace-001"
        };

        await Store.UpsertErrorAsync(error);

        var errors = await Store.GetErrorsAsync();
        Assert.Single(errors);
        Assert.Equal("NullReferenceException", errors[0].ErrorType);
        Assert.Equal("new", errors[0].Status);
        Assert.Equal(1, errors[0].OccurrenceCount);
    }

    [Fact]
    public async Task UpsertError_ExistingFingerprint_IncrementsCount()
    {
        var error1 = new ErrorEvent
        {
            ErrorType = "NullReferenceException",
            Message = "Object reference not set",
            Category = "internal",
            Fingerprint = "same-fp",
            ServiceName = "my-api",
            TraceId = "trace-001"
        };
        var error2 = new ErrorEvent
        {
            ErrorType = "NullReferenceException",
            Message = "Object reference not set",
            Category = "internal",
            Fingerprint = "same-fp",
            ServiceName = "my-api",
            TraceId = "trace-002"
        };

        await Store.UpsertErrorAsync(error1);
        await Store.UpsertErrorAsync(error2);

        var errors = await Store.GetErrorsAsync();
        Assert.Single(errors);
        Assert.Equal(2, errors[0].OccurrenceCount);
    }

    [Fact]
    public async Task GetErrorsAsync_FilterByCategory_ReturnsFiltered()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "HttpRequestException", Message = "Connection refused",
            Category = "network", Fingerprint = "fp-net", ServiceName = "api", TraceId = "t1"
        });
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "NullReferenceException", Message = "null ref",
            Category = "internal", Fingerprint = "fp-int", ServiceName = "api", TraceId = "t2"
        });

        var networkErrors = await Store.GetErrorsAsync("network");
        Assert.Single(networkErrors);
        Assert.Equal("network", networkErrors[0].Category);
    }

    [Fact]
    public async Task GetErrorStatsAsync_ReturnsCategoryBreakdown()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "HttpRequestException", Message = "timeout",
            Category = "network", Fingerprint = "fp1", ServiceName = "api", TraceId = "t1"
        });
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "NullReferenceException", Message = "null",
            Category = "internal", Fingerprint = "fp2", ServiceName = "api", TraceId = "t2"
        });
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "HttpRequestException", Message = "dns fail",
            Category = "network", Fingerprint = "fp3", ServiceName = "api", TraceId = "t3"
        });

        var stats = await Store.GetErrorStatsAsync();
        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(2, stats.ByCategory.Count);
    }
}