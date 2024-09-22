using LumiTracker.Config;
using LumiTracker.Models;
using LumiTracker.ViewModels.Pages;
using LumiTracker.ViewModels.Windows;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

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

        private ScopeState OBScopeState;
        private string OBDataDir;
        private OBConfig.Wrapper OBProxyConfig;

        public GameWatcherOBProxy(string scopeName, string scopeColor)
        {
            OBScopeState = new ScopeState { Name = scopeName, Color = scopeColor };
            using var scopeGuard = Configuration.Logger.BeginScope(OBScopeState);

            OBDataDir = Path.Combine(OBConfig.WorkingDir, scopeName);
            OBProxyConfig = OBConfig.Root[scopeName];

            OBGameWatcher         = new GameWatcher();
            OBDeckViewModel       = new DeckViewModel(null, null);
            OBDeckWindowViewModel = new DeckWindowViewModel(OBDeckViewModel, OBGameWatcher);
            OBDeckWindowViewModel.ShareCodeOverride = OBProxyConfig.Get<string>("deck");

            ResetOBData();

            OBGameWatcher.GameStarted       += OnGameStarted;
            OBGameWatcher.GameOver          += OnGameOver;
            OBGameWatcher.MyCardsDrawn      += OnMyCardsDrawn;
            OBGameWatcher.MyCardsCreateDeck += OnMyCardsCreateDeck;
            OBGameWatcher.WindowWatcherExit += OnWindowWatcherExit;
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

                string imageLinkPath = Path.Combine(OBDataDir, $"token{i}.png");
                string countTextPath = Path.Combine(OBDataDir, $"token{i}.txt");
                string targetPath;
                if (token_id >= 0)
                {
                    targetPath = Path.Combine(OBConfig.ResDirToken, $"{token_id}.png");
                }
                else
                {
                    targetPath = Path.Combine(Configuration.AssetsDir, "images", "empty.png");
                }

                if (File.Exists(imageLinkPath))
                {
                    File.Delete(imageLinkPath);
                }
                CreateLink(imageLinkPath, targetPath);

                using (StreamWriter writer = new StreamWriter(countTextPath, append: false))
                {
                    if (token_id >= 0)
                    {
                        writer.Write($"{it.Current.Value.Count}");
                    }
                }
            }
        }

        public static void CreateLink(string linkPath, string targetPath)
        {
            try
            {
                bool isDirectory = Directory.Exists(targetPath);
                bool isFile = File.Exists(targetPath);
                if (!isDirectory && !isFile)
                {
                    throw new ArgumentException($"Target path does not exist.");
                }

                string arguments;
                if (isDirectory)
                {
                    arguments = $"/C mklink /J \"{linkPath}\" \"{targetPath}\""; // Junction for directories
                }
                else
                {
                    arguments = $"/C mklink \"{linkPath}\" \"{targetPath}\""; // Symbolic link for files
                }

                Process process = new Process();
                process.StartInfo.FileName  = "cmd.exe";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardError  = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow  = true;

                process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => { Configuration.Logger.LogError(e.Data); };
                
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Configuration.Logger.LogDebug($"Link {linkPath} --> {targetPath} success.");
                }
                else
                {
                    throw new ArgumentException($"Process exit with code {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Link {linkPath} --> {targetPath} failed: {ex.Message}");
            }
        }

        public void Start()
        {
            using var scopeGuard = Configuration.Logger.BeginScope(OBScopeState);
            OBGameWatcher.Start(OBProxyConfig.Get<EClientType>("client"));
        }

        public void Wait()
        {
            OBGameWatcher.Wait();
        }

        private void OnGameStarted()
        {
            ResetOBData();
        }

        private void OnGameOver()
        {
            ResetOBData();
        }

        private void OnWindowWatcherExit()
        {
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
    }

    public class OBApp
    {
        public static void Main(string[] args)
        {
            Configuration.Logger.LogInformation($"App Version: LumiTrackerOB-{Configuration.GetAssemblyVersion()}");

            GameWatcherOBProxy myProxy = new GameWatcherOBProxy("MY", LogHelper.AnsiOrange);
            GameWatcherOBProxy opProxy = new GameWatcherOBProxy("OP", LogHelper.AnsiBlue);

            myProxy.Start();
            opProxy.Start();

            myProxy.Wait();
            opProxy.Wait();
        }
    }
}