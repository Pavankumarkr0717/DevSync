using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace DevSync.IntegrationTests.Infrastructure;

/// <summary>Captures every log message (with exception text and active scopes) for assertions.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private static readonly AsyncLocal<ImmutableStack<object>> Scopes = new();
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            var previous = Scopes.Value ?? ImmutableStack<object>.Empty;
            Scopes.Value = previous.Push(state);
            return new Pop(previous);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = (Scopes.Value ?? ImmutableStack<object>.Empty)
                .Select(s => s is IEnumerable<KeyValuePair<string, object>> pairs
                    ? string.Join(",", pairs.Select(p => $"{p.Key}={p.Value}"))
                    : s.ToString());
            messages.Enqueue(
                formatter(state, exception)
                + (exception is null ? string.Empty : " " + exception)
                + " [scopes: " + string.Join(" | ", scopes) + "]");
        }

        private sealed class Pop(ImmutableStack<object> previous) : IDisposable
        {
            public void Dispose() => Scopes.Value = previous;
        }
    }
}
