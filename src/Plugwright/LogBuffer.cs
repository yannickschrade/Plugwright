using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// Collects log entries written in phase 1, before the logging system exists, and writes them
/// to the real loggers once <see cref="Flush"/> is called in phase 2. Loggers created by the buffer
/// write directly to the real loggers after that.
/// </summary>
internal sealed class LogBuffer
{
    private readonly Lock syncRoot = new();
    private readonly List<(string Category, Action<ILogger> Write)> entries = [];
    private ILoggerFactory? loggerFactory;

    public ILogger CreateLogger(string category) => new BufferedLogger(this, category);

    public void Flush(ILoggerFactory factory)
    {
        lock (syncRoot)
        {
            loggerFactory = factory;
            foreach (var (category, write) in entries)
            {
                write(factory.CreateLogger(category));
            }

            entries.Clear();
        }
    }

    private sealed class BufferedLogger(LogBuffer buffer, string category) : ILogger
    {
        private ILogger? target;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return GetTarget()?.BeginScope(state);
        }

        // Before the flush, the configured log levels are unknown. Everything is buffered,
        // and the real logger filters when the entries are written.
        public bool IsEnabled(LogLevel logLevel) => GetTarget()?.IsEnabled(logLevel) ?? logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (buffer.syncRoot)
            {
                var logger = GetTarget();
                if (logger is null)
                {
                    buffer.entries.Add((category, logger => logger.Log(logLevel, eventId, state, exception, formatter)));
                    return;
                }

                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }

        private ILogger? GetTarget()
        {
            if (target is null && buffer.loggerFactory is not null)
            {
                target = buffer.loggerFactory.CreateLogger(category);
            }

            return target;
        }
    }
}
