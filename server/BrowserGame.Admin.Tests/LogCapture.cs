using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BrowserGame.Admin.Tests;

/// <summary>Sammelt Log-Meldungen im Speicher, damit Tests sie prüfen können.</summary>
public sealed class LogCapture : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(LogCapture owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Messages.Enqueue(formatter(state, exception));
    }
}
