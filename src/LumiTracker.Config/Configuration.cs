using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using System.Reflection;

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


    public class Configuration
    {
        private static readonly Lazy<Configuration> _lazyInstance = new Lazy<Configuration>(() => new Configuration());

        private JObject _db;

        private JObject _defaultConfig;

        private JObject _userConfig;

        private ILogger _logger;

        private StreamWriter _errorWriter;

        // Directories
        private static readonly string ExeDir = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location
        )!;

        private static readonly string DocumentsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LumiTracker"
        );

        public static readonly string ConfigDir = Path.Combine(
            DocumentsDir,
            "config"
        );

        private static readonly string LogDir = Path.Combine(
            DocumentsDir,
            "log"
        );

        // Files
        private static readonly string DbFilePath = Path.Combine(
            ExeDir,
            "assets",
            "database",
            "db.json"
        );

        private static readonly string DefaultConfigPath = Path.Combine(
            ExeDir,
            "assets",
            "config.json"
        );

        private static readonly string UserConfigPath = Path.Combine(
            ConfigDir,
            "config.json"
        );

        private static readonly string LogFilePath = Path.Combine(
            LogDir,
            "error.log"
        );

        private Configuration()
        {
            _db             = LoadJObject(DbFilePath);
            _defaultConfig  = LoadJObject(DefaultConfigPath);
            if (File.Exists(UserConfigPath))
            {
                _userConfig = LoadJObject(UserConfigPath);
            }
            else
            {
                _userConfig = new JObject();
            }

            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }
            _errorWriter = new StreamWriter(LogFilePath, false) { AutoFlush = true };

            bool DEBUG = _defaultConfig["DEBUG"]!.ToObject<bool>();
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

        public static JObject LoadJObject(string path)
        {
            string jsonString = File.ReadAllText(path);
            return JObject.Parse(jsonString, new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                LineInfoHandling = LineInfoHandling.Load
            });
        }

        public static bool SaveJObject(JObject jObject, string path)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (var stringWriter = new StringWriter())
                {
                    var customWriter = new CustomJsonTextWriter(stringWriter)
                    {
                        Formatting  = Formatting.Indented,
                        Indentation = 2,
                        IndentChar  = ' ',
                    };
                    var serializer = new JsonSerializer();
                    serializer.Serialize(customWriter, jObject);

                    File.WriteAllText(path, stringWriter.ToString());
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred while saving to {path} \n{ex.ToString()}");
                return false;
            }
        }

        private static Configuration Instance
        {
            get
            {
                return _lazyInstance.Value;
            }
        }

        private static JObject DefaultConfig
        {
            get
            {
                return Instance._defaultConfig;
            }
        }

        private static JObject UserConfig
        {
            get
            {
                return Instance._userConfig;
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
            JToken? value;
            if (!UserConfig.TryGetValue(key, out value))
            {
                value = DefaultConfig[key]!;
            }
            return value!.ToObject<T>()!;
        }

        public static void Set<T>(string key, T value, bool auto_save = true)
        {
            UserConfig[key] = JToken.FromObject(value!);
            if (auto_save)
            {
                Save();
            }
        }

        public static bool Save()
        {
            if (UserConfig.Count == 0)
            {
                return true;
            }

            return SaveJObject(UserConfig, UserConfigPath);
        }
    }

}

#pragma warning restore CS8618


