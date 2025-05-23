﻿global using IniSection  = System.Collections.Generic.Dictionary<string, string>;
global using IniSettings = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Diagnostics;

#pragma warning disable CS8618

namespace LumiTracker.Config
{
    public class Configuration
    {
        private static readonly Lazy<Configuration> _lazyInstance = new Lazy<Configuration>(() => new Configuration());

        private IniSettings _ini;

        private JObject _db;

        private JObject _defaultConfig;

        private JObject _userConfig;

        private ILogger _logger;

        private StreamWriter _errorWriter;

        public static readonly string AppName = Assembly.GetEntryAssembly()!.GetName().Name!;

        public static readonly AppVersion AppVersion = GetAppVersion();

        // Directories
        public static readonly string AppDir = Path.GetFullPath(Path.GetDirectoryName(
            Assembly.GetEntryAssembly()!.Location
        )!);

        public static readonly string RootDir = Path.GetFullPath(Path.Combine(
            AppDir,
            ".."
        ));

        public static readonly string CacheDir = Path.Combine(
            AppDir,
            "cache"
        );

        public static readonly string AssetsDir = Path.Combine(
            AppDir,
            "assets"
        );

        public static readonly string DocumentsDir = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LumiTracker"
        ));

        public static readonly string ConfigDir = Path.Combine(
            DocumentsDir,
            "config"
        );

        public static readonly string LogDir = Path.Combine(
            DocumentsDir,
            "log"
        );

        public static readonly string ChangeLogDir = Path.Combine(
            DocumentsDir,
            "changelog"
        );

        public static readonly string OBWorkingDir = Path.Combine(
            DocumentsDir,
            "ob"
        );

        public static readonly string DeckBuildsDir = Path.Combine(
            DocumentsDir,
            "builds"
        );

        // Files
        public static readonly string IniFilePath = Path.Combine(
            RootDir,
            "LumiTracker.ini"
        );

        private static readonly string DbFilePath = Path.Combine(
            AssetsDir,
            "database",
            "db.json"
        );

        public static readonly string GuidFilePath = Path.Combine(
            DocumentsDir, 
            "GUID.txt"
        );

        // File paths that can be customized
        public static string DefaultConfigPath = Path.Combine(
            AssetsDir,
            "config.json"
        );

        public static string UserConfigPath = Path.Combine(
            ConfigDir,
            "config.json"
        );

        public static string LogFilePath = Path.Combine(
            LogDir,
            "error.log"
        );

        private Configuration()
        {
            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }
            _errorWriter = new StreamWriter(LogFilePath, false) { AutoFlush = true };

            // Assume default config file will always be valid
            _defaultConfig = LoadJObjectInternal(DefaultConfigPath);
            bool DEBUG = _defaultConfig["DEBUG"]!.ToObject<bool>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole(options =>
                    {
                        options.FormatterName = "custom";
                    })
                    .AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>()
                    .AddProvider(new FileLoggerProvider(_errorWriter, LogLevel.Information))
                    .SetMinimumLevel(DEBUG ? LogLevel.Debug : LogLevel.Information)
                    ;
            });
            _logger = loggerFactory.CreateLogger<Configuration>();
            _logger.LogInformation($"App Version: {AppName}-{AppVersion}");

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling      = NullValueHandling.Ignore,
            };

            _ini = LoadIniSettings(IniFilePath);
            _db = LoadJObjectInternal(DbFilePath);
            if (File.Exists(UserConfigPath))
            {
                _userConfig = LoadJObjectInternal(UserConfigPath);
            }
            else
            {
                _userConfig = new JObject();
            }
        }

        private static IniSettings LoadIniSettings(string path)
        {
            var sections = new IniSettings();

            string? currentSection = null;
            foreach (var line in File.ReadLines(path))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";")) // Skip empty lines and comments
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]")) // Section header
                {
                    currentSection = trimmedLine.Trim('[', ']').Trim();
                    if (!sections.ContainsKey(currentSection))
                    {
                        sections[currentSection] = new Dictionary<string, string>();
                    }
                }
                else if (currentSection != null) // Key-value pair
                {
                    var parts = trimmedLine.Split(['='], 2);
                    var key   = parts[0].Trim();
                    var value = parts[1].Trim();
                    sections[currentSection][key] = value;
                }
            }

            return sections;
        }

        private static AppVersion GetAppVersion()
        {
            var assembly = Assembly.GetEntryAssembly();
            Debug.Assert(assembly != null);

            Version? version = assembly.GetName().Version;
            int major = version?.Major ?? 0;
            int minor = version?.Minor ?? 0;
            int build = version?.Build ?? 0;

            var attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            var betaAttr = attributes.FirstOrDefault(a => a.Key == "BetaNumber");
            if (!int.TryParse(betaAttr?.Value, out int beta))
            {
                beta = 0;
            }
            var patchAttr = attributes.FirstOrDefault(a => a.Key == "PatchNumber");
            if (!int.TryParse(patchAttr?.Value, out int patch))
            {
                patch = 0;
            }

            return new AppVersion(major, minor, build, beta, patch);
        }

        public static JObject LoadJObject(string path)
        {
            return Instance.LoadJObjectInternal(path);
        }

        private JObject LoadJObjectInternal(string path)
        {
            try
            {
                string jsonString = File.ReadAllText(path);
                return JObject.Parse(jsonString, new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    LineInfoHandling = LineInfoHandling.Load
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error when loading json from {path}: {ex.Message}");
                return new JObject();
            }
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
                    var customWriter = new CustomJsonTextWriter(stringWriter, indented: true);
                    var serializer   = JsonSerializer.CreateDefault();
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

        public static IniSection Ini
        {
            get
            {
                return Instance._ini["Application"];
            }
        }

        public static bool IsLanguageFollowSystem()
        {
            ELanguage curLang = Get<string>("lang")?.ToELanguage() ?? ELanguage.FollowSystem;
            return curLang == ELanguage.FollowSystem;
        }

        public static string GetLanguageName()
        {
            // Ensures that no invalid name will be returned
            return EnumHelpers.ParseLanguageName(Get<string>("lang"));
        }

        public static ELanguage GetELanguage()
        {
            // Ensures that no invalid ELanguage will be returned
            // And ELanguage.FollowSystem will not be returned
            return GetLanguageName().ToELanguage();
        }

        public static string GetCharacterName(int c_id, bool is_short)
        {
            if (c_id < 0 || c_id >= (int)ECharacterCard.NumCharacters)
                return Lang.UnknownCard;

            string key = GetLanguageName();
            if (is_short) key += "_short";
            return Database["characters"]![c_id]![key]!.ToString();
        }

        public static string GetActionCardName(int card_id)
        {
            if (card_id < 0 || card_id >= (int)EActionCard.NumActions)
                return Lang.UnknownCard;

            return Database["actions"]![card_id]![GetLanguageName()]!.ToString();
        }

        public static JToken? GetToken(string key)
        {
            JToken? value;
            if (!UserConfig.TryGetValue(key, out value) && !DefaultConfig.TryGetValue(key, out value))
            {
                return default;
            }
            return value;
        }

        public static T Get<T>(string key) 
        {
            JToken? token = GetToken(key);
            if (token == null)
            {
                return default!;
            }
            try
            {
                return token.ToObject<T>()!;
            }
            catch
            {
                return default!;
            }
        }

        public static void Set<T>(string key, T value, bool auto_save = true)
        {
            UserConfig[key] = JToken.FromObject(value!);
            if (auto_save)
            {
                Save();
            }
        }

        public static void SetTemporal<T>(string key, T value)
        {
            DefaultConfig[key] = JToken.FromObject(value!);
        }

        public static void RemoveTemporal(string key)
        {
            if (DefaultConfig.ContainsKey(key))
            {
                DefaultConfig.Remove(key);
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

        public static async Task RevealInExplorerAsync(string path)
        {
            if (Directory.Exists(path))
            {
                // If it's a directory, open it
                var dirInfo = new DirectoryInfo(path);
                await Task.Run(() => Process.Start("explorer.exe", dirInfo.FullName));
            }
            else if (File.Exists(path))
            {
                // If it's a file, open the directory containing the file and select the file
                var fileInfo = new FileInfo(path);
                await Task.Run(() => Process.Start("explorer.exe", $"/select,\"{fileInfo.FullName}\""));
            }
            else
            {
                Logger.LogError("[RevealInExplorer] Path not found.");
            }
        }

        private static Guid _guid = Guid.Empty;
        public static Guid GetOrCreateGuid(out bool isNewlyCreated)
        {
            isNewlyCreated = false;
            if (_guid == Guid.Empty)
            {
                try
                {
                    if (File.Exists(GuidFilePath))
                    {
                        string guidStr = File.ReadAllText(GuidFilePath);
                        if (!Guid.TryParse(guidStr, out _guid))
                        {
                            Logger.LogError("[GetOrCreateGuid] Failed to parse guid.");
                        }
                    }
                }
                catch (Exception ex) 
                {
                    Logger.LogError($"[GetOrCreateGuid] Unexpected Exception: {ex.Message}");
                }

                // Create a new guid if failed to load
                if (_guid == Guid.Empty)
                {
                    isNewlyCreated = true;
                    _guid = Guid.NewGuid();
                    File.WriteAllText(GuidFilePath, _guid.ToString());
                }
            }

            return _guid;
        }
    }

}

#pragma warning restore CS8618


