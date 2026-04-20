namespace Qyl.Instrumentation.ErrorCapture;

using Instrumentation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        if (Activity.Current is not { } activity) return;

        ActivityExceptionTelemetry.Record(activity, ex);

        logger.LogExceptionCaptured(ex.GetType().Name, ex.Message);
    }
}

public static class GlobalExceptionHooks
{
    private static int s_registered;

    private static readonly ActivitySource Source = new("Qyl.Instrumentation.ErrorCapture");

    public static void Register(ILoggerFactory loggerFactory)
    {
        if (Interlocked.Exchange(ref s_registered, 1) is not 0) return;

        var logger = loggerFactory.CreateLogger("Qyl.Instrumentation.ErrorCapture");

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

    private static void RecordGlobalException(Exception ex, string source)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            if (Source.StartActivity("UnhandledException", ActivityKind.Internal, parentContext: default) is not
                { } fallback) return;
            TagActivity(fallback, ex, source);
            return;
        }

        TagActivity(activity, ex, source);
    }

    private static void TagActivity(Activity activity, Exception ex, string source)
    {
        ActivityExceptionTelemetry.ApplyError(activity, ex);

        var tags = ActivityExceptionTelemetry.CreateTags(ex);
        tags.Add("exception.source", source);
        activity.AddEvent(new ActivityEvent("exception", tags: tags));
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
