// // Copyright (c) Microsoft. All rights reserved.
//
//
// namespace qyl.mcp.server.tests;
//
// /// <summary>
// /// A logger that writes to the Xunit test output
// /// </summary>
// internal sealed class XunitLogger(ITestOutputHelper output) : ILoggerFactory, ILogger, IDisposable
// {
//     private object? _scopeState;
//
//     /// <inheritdoc/>
//     public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
//         Func<TState, Exception?, string> formatter)
//     {
//         var localState = state?.ToString();
//         var line = _scopeState is not null ? $"{_scopeState} {localState}" : localState;
//         output.WriteLine(line);
//     }
//
//     /// <inheritdoc/>
//     public bool IsEnabled(LogLevel logLevel) => true;
//
//     /// <inheritdoc/>
//     public IDisposable BeginScope<TState>(TState state) where TState : notnull
//     {
//         _scopeState = state;
//         return this;
//     }
//
//     /// <inheritdoc/>
//     public void Dispose()
//     {
//         // This class is marked as disposable to support the BeginScope method.
//         // However, there is no need to dispose anything.
//     }
//
//     public ILogger CreateLogger(string categoryName) => this;
//
//     public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();
// }