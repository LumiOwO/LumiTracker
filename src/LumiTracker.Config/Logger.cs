using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CS8618

namespace LumiTracker.Config
{
    public class FileLogger : ILogger
    {
        private readonly StreamWriter _writer;
        private readonly LogLevel _minLevel;

        private readonly AsyncLocal<string> _currentScope = new AsyncLocal<string>();

        public FileLogger(StreamWriter writer, LogLevel minLevel)
        {
            _writer = writer;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var previousScope = _currentScope.Value ?? "";
            _currentScope.Value = state?.ToString() ?? "";
            return new ScopeDisposer(() => _currentScope.Value = previousScope);
        }

        private class ScopeDisposer : IDisposable
        {
            private readonly Action _onDispose;

            public ScopeDisposer(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose();
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            string scope = _currentScope.Value != null ? $"<{_currentScope.Value}>" : "";
            _writer.WriteLine($"{DateTime.Now} [{logLevel}] {scope}");
            _writer.WriteLine($"{message}");
            if (exception != null)
            {
                _writer.WriteLine($"{DateTime.Now} [{logLevel}] {exception.ToString()}");
            }
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly StreamWriter _writer;
        private readonly LogLevel _minLevel;

        public FileLoggerProvider(StreamWriter writer, LogLevel minLevel)
        {
            _writer = writer;
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_writer, _minLevel);
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }

    public class CustomJsonTextWriter : JsonTextWriter
    {
        public CustomJsonTextWriter(TextWriter writer) : base(writer)
        {
        }

        protected override void WriteIndent()
        {
            if (WriteState != WriteState.Array)
            {
                base.WriteIndent();
            }
            else
            {
                WriteIndentSpace();
            }
        }
    }

    public class CustomConsoleFormatter : ConsoleFormatter
    {
        private static readonly Dictionary<LogLevel, ConsoleColor> LogLevelColors = new Dictionary<LogLevel, ConsoleColor>
        {
            { LogLevel.Trace, ConsoleColor.Gray },
            { LogLevel.Debug, ConsoleColor.Cyan },
            { LogLevel.Information, ConsoleColor.Green },
            { LogLevel.Warning, ConsoleColor.Yellow },
            { LogLevel.Error, ConsoleColor.Red },
            { LogLevel.Critical, ConsoleColor.Magenta },
            { LogLevel.None, ConsoleColor.White }
        };

        private static string GetAnsiColor(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace       => "\x1b[37m", // Gray
            LogLevel.Debug       => "\x1b[36m", // Cyan
            LogLevel.Information => "\x1b[32m", // Green
            LogLevel.Warning     => "\x1b[33m", // Yellow
            LogLevel.Error       => "\x1b[31m", // Red
            LogLevel.Critical    => "\x1b[35m", // Magenta
            _                    => "\x1b[37m", // Default to White
        };

        public CustomConsoleFormatter() : base("custom")
        {
        }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            var scopeString = "";
            if (scopeProvider != null)
            {
                // Use StringWriter to capture scope information
                using (var stringWriter = new StringWriter())
                {
                    scopeProvider.ForEachScope((scope, writer) =>
                    {
                        writer.Write($"<{scope}> ");
                    }, stringWriter);

                    scopeString = stringWriter.ToString();
                }
            }

            // Set the color for the log output
            textWriter.Write(GetAnsiColor(logEntry.LogLevel));
            {
                textWriter.Write($"{DateTime.Now} [{logEntry.LogLevel}] ");
            }
            textWriter.Write("\u001b[39m\u001b[22m");
            textWriter.WriteLine($"{scopeString}");
            textWriter.WriteLine($"{logEntry.Formatter(logEntry.State, logEntry.Exception)}");
        }
    }

    public class CustomConsoleFormatterOptions : ConsoleFormatterOptions
    {
        // Define any custom options here if needed
    }

}
