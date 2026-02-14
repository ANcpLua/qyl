using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.ServiceDefaults.ErrorCapture;

public sealed class ExceptionCaptureMiddleware(RequestDelegate next, ILogger<ExceptionCaptureMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordException(ex);
            throw; // Re-throw — we capture, we don't swallow
        }
    }

    private void RecordException(Exception ex)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.ToString() },
            { "exception.escaped", "true" },
        }));

        logger.LogExceptionCaptured(ex.GetType().Name, ex.Message);
    }
}

public static class GlobalExceptionHooks
{
    private static int _registered;

    public static void Register(ILoggerFactory loggerFactory)
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0) return;

        var logger = loggerFactory.CreateLogger("Qyl.ServiceDefaults.ErrorCapture");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                logger.LogUnhandledException(ex, args.IsTerminating);
                RecordGlobalException(ex, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogUnobservedTaskException(args.Exception);
            RecordGlobalException(args.Exception, "TaskScheduler.UnobservedTaskException");
        };
    }

    private static readonly ActivitySource Source = new("Qyl.ServiceDefaults.ErrorCapture");

    private static void RecordGlobalException(Exception ex, string source)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            using var fallback = Source.StartActivity("UnhandledException", ActivityKind.Internal, parentContext: default);
            if (fallback is null) return;
            TagActivity(fallback, ex, source);
            return;
        }

        TagActivity(activity, ex, source);
    }

    private static void TagActivity(Activity activity, Exception ex, string source)
    {
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.ToString() },
            { "exception.escaped", "true" },
            { "exception.source", source },
        }));
    }
}

internal sealed class ExceptionHookRegistrar(ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        GlobalExceptionHooks.Register(loggerFactory);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static partial class ExceptionCaptureLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "[qyl] Captured {ExceptionType}: {Message}")]
    public static partial void LogExceptionCaptured(this ILogger logger, string exceptionType, string message);

    [LoggerMessage(Level = LogLevel.Critical, Message = "[qyl] Unhandled exception (IsTerminating={IsTerminating})")]
    public static partial void LogUnhandledException(this ILogger logger, Exception exception, bool isTerminating);

    [LoggerMessage(Level = LogLevel.Error, Message = "[qyl] Unobserved task exception")]
    public static partial void LogUnobservedTaskException(this ILogger logger, Exception? exception);
}
