using qyl.collector.Errors;
using qyl.collector.Storage;

namespace qyl.collector.tests.Errors;

public sealed class ErrorEndpointTests : IAsyncLifetime
{
    private DuckDbStore? _store;
    private DuckDbStore Store => _store ?? throw new InvalidOperationException();

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public ValueTask DisposeAsync() => _store?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task UpdateErrorStatus_ChangesStatus()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "Exception", Message = "test", Category = "unknown",
            Fingerprint = "fp-status", ServiceName = "api", TraceId = "t1"
        });

        var errors = await Store.GetErrorsAsync();
        Assert.Equal("new", errors[0].Status);

        await Store.UpdateErrorStatusAsync(errors[0].ErrorId, "acknowledged");

        var updated = await Store.GetErrorsAsync();
        Assert.Equal("acknowledged", updated[0].Status);
    }

    [Fact]
    public async Task GetErrorById_ReturnsCorrectError()
    {
        await Store.UpsertErrorAsync(new ErrorEvent
        {
            ErrorType = "NullRef", Message = "null", Category = "internal",
            Fingerprint = "fp-byid", ServiceName = "api", TraceId = "t1"
        });

        var errors = await Store.GetErrorsAsync();
        var detail = await Store.GetErrorByIdAsync(errors[0].ErrorId);
        Assert.NotNull(detail);
        Assert.Equal("NullRef", detail.ErrorType);
    }

    [Fact]
    public async Task GetErrorById_NotFound_ReturnsNull()
    {
        var detail = await Store.GetErrorByIdAsync("nonexistent");
        Assert.Null(detail);
    }
}
