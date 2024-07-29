using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

#pragma warning disable CS8618

namespace LumiTracker.Config
{
    public class FileLogger : ILogger
    {
        private readonly StreamWriter _writer;
        private readonly LogLevel _minLevel;

        public FileLogger(StreamWriter writer, LogLevel minLevel)
        {
            _writer = writer;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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
            _writer.WriteLine($"{DateTime.Now} [{logLevel}] {message}");
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


    public class Configuration
    {
        private static readonly Lazy<Configuration> _lazyInstance = new Lazy<Configuration>(() => new Configuration());

        private JObject _data;

        private JObject _db;

        private ILogger _logger;

        private StreamWriter _errorWriter;

        private static readonly string configFilePath = "assets/config.json";

        private static readonly string dbFilePath = "assets/database/db.json";

        private Configuration()
        {
            _data = LoadConfig();
            _db = LoadDatabase();

            Directory.CreateDirectory("log");
            _errorWriter = new StreamWriter("log/error.log", false) { AutoFlush = true };

            bool DEBUG = _data["DEBUG"]!.ToObject<bool>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .AddProvider(new FileLoggerProvider(_errorWriter, LogLevel.Warning))
                    .SetMinimumLevel(DEBUG ? LogLevel.Debug : LogLevel.Information)
                    ;
            });
            _logger = loggerFactory.CreateLogger<Configuration>();
        }

        private static JObject LoadConfig()
        {
            string jsonString = File.ReadAllText(configFilePath);
            var settings = new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                LineInfoHandling = LineInfoHandling.Load
            };

            return JObject.Parse(jsonString, settings);
        }

        private static JObject LoadDatabase()
        {
            string jsonString = File.ReadAllText(dbFilePath);
            return JObject.Parse(jsonString);
        }

        private static Configuration Instance
        {
            get
            {
                return _lazyInstance.Value;
            }
        }

        private static JObject Data
        {
            get
            {
                return Instance._data;
            }
        }

        public static JObject Database
        {
            get
            {
                return Instance._db;
            }
        }

        public static ILogger Logger
        {
            get
            {
                return Instance._logger;
            }
        }

        public static T Get<T>(string key)
        {
            return Data[key]!.ToObject<T>()!;
        }

        public static void Set<T>(string key, T value, bool auto_save = true)
        {
            Data[key] = JToken.FromObject(value!);
            if (auto_save)
            {
                Save();
            }
        }

        public static bool Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
                File.WriteAllText(configFilePath, json);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogError("Error occurred while writing to file: " + e.Message);
                return false;
            }
        }
    }

}

#pragma warning restore CS8618


