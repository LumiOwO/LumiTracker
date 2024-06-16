using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

#pragma warning disable CS8618

namespace LumiTracker.Config
{
    #region ConfigData

    // Created by Visual Studio "Paste Special > Paste JSON As Classes"
    public class ConfigData
    {
        public bool DEBUG { get; set; }
        public bool DEBUG_SAVE { get; set; }
        public string debug_dir { get; set; }
        public int LOG_INTERVAL { get; set; }
        public int proc_watch_interval { get; set; }
        public float frame_interval { get; set; }
        public float[] event_crop_box1 { get; set; }
        public float[] event_crop_box2 { get; set; }
        public string assets_dir { get; set; }
        public string database_dir { get; set; }
        public string events_ann_filename { get; set; }
        public string rounds_ann_filename { get; set; }
        public string db_filename { get; set; }
        public string cards_dir { get; set; }
        public int hash_size { get; set; }
        public int ann_index_len { get; set; }
        public int threshold { get; set; }
        public string ann_metric { get; set; }
        public int ann_n_trees { get; set; }
        public string lang { get; set; }
        public string closing_behavior { get; set; }
        public string theme { get; set; }
        public string client_type { get; set; }
    }




    #endregion ConfigData

    public enum ELanguage
    {
        zh_HANS,
        en_US,
    }

    public enum EClosingBehavior
    {
        Quit,
        Minimize,
    }

    public enum EClientType
    {
        YuanShen,
        Global,
        Cloud,
    }

    public class Configuration
    {
        private static readonly Lazy<Configuration> _lazyInstance = new Lazy<Configuration>(() => new Configuration());

        private ConfigData _data;

        private JObject _db;

        private ILogger _logger;

        private StreamWriter _errorWriter;

        private static readonly string configFilePath = "assets/config.json";

        private static readonly string dbFilePath = "assets/database/db.json";

        private Configuration()
        {
            _data = LoadConfig();
            _db = LoadDatabase();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(_data.DEBUG ? LogLevel.Debug : LogLevel.Information);
            });
            _logger = loggerFactory.CreateLogger<Configuration>();

            Directory.CreateDirectory("log");
            _errorWriter = new StreamWriter("log/error.log", false) { AutoFlush = true };
        }

        private static ConfigData LoadConfig()
        {
            string jsonString = File.ReadAllText(configFilePath);
            var settings = new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                LineInfoHandling = LineInfoHandling.Load
            };

            var jObject = JObject.Parse(jsonString, settings);
            return jObject.ToObject<ConfigData>()!;
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

        public static ConfigData Data
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

        public static StreamWriter ErrorWriter
        {
            get
            {
                return Instance._errorWriter;
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


