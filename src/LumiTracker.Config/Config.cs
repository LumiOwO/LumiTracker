﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#pragma warning disable CS8618

namespace LumiTracker.Config
{

    public class Config
    {
        private static Config LoadConfig() {
            string filePath = "assets/config.json";
            string jsonString = File.ReadAllText(filePath);
            var settings = new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                LineInfoHandling = LineInfoHandling.Load
            };

            var jObject = JObject.Parse(jsonString, settings);
            return jObject.ToObject<Config>()!;
        }

        private static readonly Lazy<Config> _lazyInstance = new Lazy<Config>(() => LoadConfig());
        public static Config Instance
        {
            get
            {
                return _lazyInstance.Value;
            }
        }

        public bool DEBUG { get; set; }
        public string debug_dir { get; set; }
        public int SKIP_FRAMES { get; set; }
        public int LOG_INTERVAL { get; set; }
        public string proc_name { get; set; }
        public int proc_watch_interval { get; set; }
        public string database_dir { get; set; }
        public string events_ann_filename { get; set; }
        public string db_filename { get; set; }
        public string cards_dir { get; set; }
        public int hash_size { get; set; }
        public int ann_index_len { get; set; }
        public int threshold { get; set; }
        public string ann_metric { get; set; }
        public int ann_n_trees { get; set; }
        public float[] start_screen_size { get; set; }
        public float[] event_screen_size { get; set; }
        public float[] my_event_pos { get; set; }
        public float[] op_event_pos { get; set; }
    }

}

#pragma warning restore CS8618


