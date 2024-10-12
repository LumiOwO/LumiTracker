using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace LumiTracker.Config
{
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

    public class LogHelper
    {
        public static readonly string AnsiWhite   = "\x1b[37m";
        public static readonly string AnsiGray    = "\x1b[37m";
        public static readonly string AnsiRed     = "\x1b[31m";
        public static readonly string AnsiGreen   = "\x1b[32m";
        public static readonly string AnsiBlue    = "\x1b[34m";
        public static readonly string AnsiCyan    = "\x1b[36m";
        public static readonly string AnsiMagenta = "\x1b[35m";
        public static readonly string AnsiYellow  = "\x1b[33m";
        public static readonly string AnsiOrange  = "\x1b[38;5;208m";
        public static readonly string AnsiEnd     = "\x1b[39m\x1b[22m";

        public static readonly Regex AnsiRegex = new Regex(@"\x1b\[\d{1,3}(;\d{1,3})*m", RegexOptions.Compiled);

        public static string GetAnsiColor(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace       => AnsiGray,
            LogLevel.Debug       => AnsiCyan,
            LogLevel.Information => AnsiGreen,
            LogLevel.Warning     => AnsiYellow,
            LogLevel.Error       => AnsiRed,
            LogLevel.Critical    => AnsiMagenta,
            _                    => AnsiWhite,
        };

        public static string GetShortLevelStr(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace       => "[TRACE]",
            LogLevel.Debug       => "[DEBUG]",
            LogLevel.Information => " [INFO]",
            LogLevel.Warning     => " [WARN]",
            LogLevel.Error       => "[ERROR]",
            LogLevel.Critical    => "[FATAL]",
            _                    => "  [LOG]",
        };

        public static bool ContainsDictOrArray(JToken token)
        {
            var jObject = token as JObject;
            if (jObject == null) return false;

            foreach (var property in jObject.Properties())
            {
                if (property.Value is JObject || property.Value is JArray)
                {
                    return true;
                }
            }
            return false;
        }

        public static string JsonToConsoleStr(JToken token, bool forceIndent = false, bool forceCompact = false)
        {
            string res = "";
            using (var stringWriter = new StringWriter())
            {
                bool indented = forceIndent  ? true  :
                                forceCompact ? false :
                                ContainsDictOrArray(token);
                var customWriter = new CustomJsonTextWriter(stringWriter)
                {
                    Formatting  = indented ? Formatting.Indented : Formatting.None,
                    Indentation = 2,
                    IndentChar  = ' ',
                };
                var serializer = new JsonSerializer();
                serializer.Serialize(customWriter, token);
                res = stringWriter.ToString();
            }
            return res;
        }
    }

    public class ScopeState
    {
        public string Name { get; set; }  = "";
        public string Color { get; set; } = LogHelper.AnsiWhite;
    }

    public class ScopeDisposer : IDisposable
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

    /////////////////////////////
    /// File Logger Formatting
    ///

    public class FileLogger : ILogger
    {
        private readonly StreamWriter _writer;
        private readonly LogLevel _minLevel;

        private readonly AsyncLocal<ScopeState> _currentScope = new AsyncLocal<ScopeState>();

        public FileLogger(StreamWriter writer, LogLevel minLevel)
        {
            _writer   = writer;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            ScopeState previousScope = _currentScope.Value!;
            _currentScope.Value = (state as ScopeState)!;
            return new ScopeDisposer(() => _currentScope.Value = previousScope);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            string scope    = _currentScope.Value != null ? $"({_currentScope.Value.Name}) " : "";
            string levelStr = LogHelper.GetShortLevelStr(logLevel);

            string message  = formatter(state, exception);
            message = LogHelper.AnsiRegex.Replace(message, string.Empty); // Filter ansi color text

            string timeStr = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff");
            _writer.WriteLine($"{scope}{timeStr} {levelStr}> {message}");
            if (exception != null)
            {
                _writer.WriteLine($"{scope}{timeStr} {levelStr}> {exception.ToString()}");
            }
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly StreamWriter _writer;
        private readonly LogLevel _minLevel;

        public FileLoggerProvider(StreamWriter writer, LogLevel minLevel)
        {
            _writer   = writer;
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

    /////////////////////////////
    /// Console Logger Formatting
    ///

    public class CustomConsoleFormatter : ConsoleFormatter
    {
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
                    scopeProvider.ForEachScope((state, writer) =>
                    {
                        var scope = (state as ScopeState)!;
                        writer.Write($".{scope.Color}({scope.Name}){LogHelper.AnsiEnd}");
                    }, stringWriter);

                    scopeString = stringWriter.ToString();
                    if (scopeString.Length > 0)
                    {
                        scopeString = scopeString.Substring(1) + " ";
                    }
                }
            }

            // Set the color for the log output
            textWriter.Write($"{scopeString}");
            textWriter.Write(LogHelper.GetAnsiColor(logEntry.LogLevel));
            {
                textWriter.Write($"{DateTime.Now.ToString("HH:mm:ss.fff")} {LogHelper.GetShortLevelStr(logEntry.LogLevel)}>");
            }
            textWriter.Write(LogHelper.AnsiEnd);
            textWriter.WriteLine($" {logEntry.Formatter(logEntry.State, logEntry.Exception)}");
        }
    }

    public class CustomConsoleFormatterOptions : ConsoleFormatterOptions
    {
        // Define any custom options here if needed
    }
}
