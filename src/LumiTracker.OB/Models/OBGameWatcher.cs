using LumiTracker.Config;
using LumiTracker.Helpers;
using LumiTracker.Models;
using LumiTracker.ViewModels.Pages;
using LumiTracker.ViewModels.Windows;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;

namespace LumiTracker.OB
{
    public class OBConfig
    {
        private static readonly Lazy<OBConfig> _lazyInstance = new Lazy<OBConfig>(() => new OBConfig());

        private JObject _obConfig;

        public static readonly string WorkingDir = Path.Combine(
            Configuration.DocumentsDir,
            "ob"
        );

        public static readonly string CaptureDir = Path.Combine(
            WorkingDir,
            "capture"
        );

        public static readonly string OBLogFilePath = Path.Combine(
            WorkingDir,
            "error.log"
        );

        private static readonly string ConfigPath = Path.Combine(
            Configuration.AppDir,
            "assets",
            "obconfig.json"
        );

        public static readonly string ResDirToken = Root["resource_dirs"].Get<string>("tokens");

        private OBConfig()
        {
            _obConfig = Configuration.LoadJObject(ConfigPath);
        }

        private static OBConfig Instance
        {
            get
            {
                return _lazyInstance.Value;
            }
        }

        public class Wrapper
        {
            public JToken? Token { get; set; } = null;
            public Wrapper GetToken(string key)
            {
                JToken? token = null;
                if (Token != null && Token.Type == JTokenType.Object)
                {
                    Token.ToObject<JObject>()!.TryGetValue(key, out token);
                }

                Wrapper res = new Wrapper();
                res.Token = token;
                return res;
            }
            public Wrapper this[string key]
            {
                get => GetToken(key);
            }
            public T Get<T>(string key)
            {
                JToken? token = GetToken(key).Token;
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
        }

        public static Wrapper Root
        {
            get
            {
                return new Wrapper { Token = Instance._obConfig };
            }
        }

    }

    public class GameWatcherOBProxy
    {
        private DeckViewModel OBDeckViewModel;
        private DeckWindowViewModel OBDeckWindowViewModel;
        private GameWatcher OBGameWatcher;
        private WindowSnapper? OBSnapper;

        private ScopeState OBScopeState;
        private string OBDataDir;
        private OBConfig.Wrapper OBProxyConfig;

        public GameWatcherOBProxy(string scopeName, string scopeColor)
        {
            OBScopeState = new ScopeState { Name = scopeName, Color = scopeColor };
            using var scopeGuard = Configuration.Logger.BeginScope(OBScopeState);

            OBDataDir = Path.Combine(OBConfig.WorkingDir, scopeName);
            OBProxyConfig = OBConfig.Root[scopeName];

            OBGameWatcher         = new GameWatcher(Path.Combine(OBConfig.WorkingDir, $"{scopeName}_init.json"), true);
            OBDeckViewModel       = new DeckViewModel(null, null);
            OBDeckWindowViewModel = new DeckWindowViewModel(OBDeckViewModel, OBGameWatcher);
            OBDeckWindowViewModel.ShareCodeOverride = OBProxyConfig.Get<string>("deck");

            ResetOBData();

            OBGameWatcher.GameStarted        += OnGameStarted;
            OBGameWatcher.GameOver           += OnGameOver;
            OBGameWatcher.MyCardsDrawn       += OnMyCardsDrawn;
            OBGameWatcher.MyCardsCreateDeck  += OnMyCardsCreateDeck;
            OBGameWatcher.WindowWatcherStart += OnWindowWatcherStart;
            OBGameWatcher.WindowWatcherExit  += OnWindowWatcherExit;
            OBGameWatcher.CaptureTestDone    += OnCaptureTestDone;
        }

        private void ResetOBData()
        {
            try
            {
                if (Directory.Exists(OBDataDir))
                {
                    Directory.Delete(OBDataDir, true);  // Recursively delete everything
                }
                Directory.CreateDirectory(OBDataDir);
                UpdateOBData();
                Configuration.Logger.LogInformation($"OB data reset done at {OBDataDir}");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"OB data reset failed at {OBDataDir}, you can delete it manually. Error: {ex.Message}");
            }
        }

        private void UpdateOBData()
        {
            var tracked = OBDeckWindowViewModel.TrackedCards.Data;
            var it = tracked.GetEnumerator();
            int max_cnt = OBProxyConfig.Get<int>("max_tracked_tokens");
            for (int i = 1; i <= max_cnt; i++)
            {
                int token_id = -1;
                if (it.MoveNext())
                {
                    token_id = it.Current.Key - (int)EActionCard.NumSharables;
                }

                string imageFileForOBS = Path.Combine(OBDataDir, $"token{i}.png");
                string countFileForOBS = Path.Combine(OBDataDir, $"token{i}.txt");
                string imageSrcPath;
                if (token_id >= 0)
                {
                    imageSrcPath = Path.Combine(OBConfig.ResDirToken, $"{token_id}.png");
                }
                else
                {
                    imageSrcPath = Path.Combine(Configuration.AssetsDir, "images", "empty.png");
                }

                try
                {
                    File.Copy(imageSrcPath, imageFileForOBS, overwrite: true);
                    Configuration.Logger.LogDebug($"[File.Copy] {imageSrcPath} --> {imageFileForOBS} success.");
                }
                catch (Exception ex)
                {
                    Configuration.Logger.LogError($"[File.Copy] {imageSrcPath} --> {imageFileForOBS} failed: {ex.Message}");
                }

                using (StreamWriter writer = new StreamWriter(countFileForOBS, append: false))
                {
                    if (token_id >= 0)
                    {
                        writer.Write($"{it.Current.Value.Count}");
                    }
                }
            }
        }

        public void Start()
        {
            using var scopeGuard = Configuration.Logger.BeginScope(OBScopeState);
            OBGameWatcher.Start(OBProxyConfig.Get<EClientType>("client_type"), OBProxyConfig.Get<ECaptureType>("capture_type"));
        }

        private void OnGameStarted()
        {
            ResetOBData();
        }

        private void OnGameOver()
        {
            ResetOBData();
        }

        private void OnWindowWatcherStart(IntPtr hwnd)
        {
            ResetOBData();

            OBSnapper = new WindowSnapper(null, hwnd, false);
            OBSnapper.Attach();
            OBSnapper.GenshinWindowResized += OnGenshinWindowResized;
        }

        private void OnWindowWatcherExit()
        {
            if (OBSnapper != null)
            {
                OBSnapper.GenshinWindowResized -= OnGenshinWindowResized;
                OBSnapper.Detach();
                OBSnapper = null;
            }

            ResetOBData();
        }

        private void OnMyCardsDrawn(int[] card_ids)
        {
            UpdateOBData();
        }

        private void OnMyCardsCreateDeck(int[] card_ids)
        {
            UpdateOBData();
        }

        private void OnGenshinWindowResized(int width, int height, bool isMinimized)
        {
            
        }

        private void OnCaptureTestDone(string filename, int width, int height)
        {
            if (!Directory.Exists(OBConfig.CaptureDir))
            {
                Directory.CreateDirectory(OBConfig.CaptureDir);
            }

            string src = Path.Combine(Configuration.LogDir, filename);
            string dst = Path.Combine(OBConfig.CaptureDir, $"{OBScopeState.Name}_{filename}");
            try
            {
                File.Move(src, dst, overwrite: true);
                Configuration.Logger.LogInformation($"[OnCaptureTestDone] CaptureTest saved at {OBScopeState.Name}_{filename}");
            }
            catch (IOException ex)
            {
                Configuration.Logger.LogError($"[OnCaptureTestDone] An error occurred: {ex.Message}");
            }
        }
    }
}