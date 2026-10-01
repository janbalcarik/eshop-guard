using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Tests.Shared;

/// <summary>One captured log record: rendered message, exception text and state values.</summary>
internal sealed record CapturedLog(string Category, LogLevel Level, string Message, string? Exception, IReadOnlyList<string> StateValues)
{
    /// <summary>Everything a log sink could write, for searching.</summary>
    public string AllText => string.Join('\n', [Message, Exception ?? string.Empty, .. StateValues]);
}

/// <summary>Captures every log record of a host, including exceptions and structured state.</summary>
internal sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _logs = new();

    /// <summary>Records so far.</summary>
    public IReadOnlyList<CapturedLog> Logs => [.. _logs];

    /// <summary>True when some record's message contains the text.</summary>
    public bool Contains(string text) => _logs.Any(l => l.Message.Contains(text, StringComparison.Ordinal));

    /// <summary>Waits until a record contains the text.</summary>
    public async Task WaitForAsync(string text, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!Contains(text))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Log record '{text}' did not appear within {timeout}.");
            }

            await Task.Delay(50);
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _logs);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<CapturedLog> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.Select(p => $"{p.Key}={p.Value}").ToList()
                : [state?.ToString() ?? string.Empty];
            logs.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception?.ToString(), values));
        }
    }
}
