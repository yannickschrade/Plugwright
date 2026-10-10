using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// Collects log entries written in phase 1, before the logging system exists, and writes them
/// to the real loggers once <see cref="Flush"/> is called in phase 2. Loggers created by the buffer
/// write directly to the real loggers after that.
/// </summary>
internal sealed class LogBuffer
{
    private readonly Lock _syncRoot = new();
    private readonly List<(string Category, Action<ILogger> Write)> _entries = [];
    private ILoggerFactory? _loggerFactory;

    public ILogger CreateLogger(string category) => new BufferedLogger(this, category);

    public void Flush(ILoggerFactory factory)
    {
        lock (_syncRoot)
        {
            _loggerFactory = factory;
            foreach ((var category, var write) in _entries)
            {
                write(factory.CreateLogger(category));
            }

            _entries.Clear();
        }
    }

    private sealed class BufferedLogger(LogBuffer buffer, string category) : ILogger
    {
        private ILogger? _target;

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
            lock (buffer._syncRoot)
            {
                var logger = GetTarget();
                if (logger is null)
                {
                    buffer._entries.Add((category, logger => logger.Log(logLevel, eventId, state, exception, formatter)));
                    return;
                }

                logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }

        private ILogger? GetTarget()
        {
            if (_target is null && buffer._loggerFactory is not null)
            {
                _target = buffer._loggerFactory.CreateLogger(category);
            }

            return _target;
        }
    }
}
