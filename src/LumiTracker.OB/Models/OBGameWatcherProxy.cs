using LumiTracker.Config;
using LumiTracker.ViewModels.Pages;
using LumiTracker.ViewModels.Windows;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;

namespace LumiTracker.OB
{
    public class OBGameWatcherProxy
    {
        private ScopeState          Scope;
        private GameEventHook       Hook;
        private DeckViewModel       DeckViewModel;
        private DeckWindowViewModel DeckWindowViewModel;
        private string              WorkingDir;

        public OBGameWatcherProxy(ScopeState scope)
        {
            Scope = scope;
            WorkingDir = Path.Combine(Configuration.OBWorkingDir, scope.Guid.ToString());

            Hook                = new GameEventHook();
            DeckViewModel       = new DeckViewModel(null, null);
            DeckWindowViewModel = new DeckWindowViewModel(DeckViewModel, Hook);

            ResetOBData();

            Hook.GameStarted        += OnGameStarted;
            Hook.GameOver           += OnGameOver;
            Hook.MyCardsDrawn       += OnMyCardsDrawn;
            Hook.MyCardsCreateDeck  += OnMyCardsCreateDeck;
            Hook.WindowWatcherStart += OnWindowWatcherStart;
            Hook.WindowWatcherExit  += OnWindowWatcherExit;
        }

        public void ParseGameEventTask(GameEventMessage message)
        {
            Hook.ParseGameEventMessage(message);
        }

        private void ResetOBData()
        {
            try
            {
                if (Directory.Exists(WorkingDir))
                {
                    Directory.Delete(WorkingDir, true);  // Recursively delete everything
                }
                Directory.CreateDirectory(WorkingDir);
                UpdateOBData();
                Configuration.Logger.LogInformation($"OB data reset done at {WorkingDir}");
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"OB data reset failed at {WorkingDir}, you can delete it manually. Error: {ex.Message}");
            }
        }

        private void UpdateOBData()
        {
            var tracked = DeckWindowViewModel.TrackedCards.Data;
            var it = tracked.GetEnumerator();
            int max_cnt = Configuration.Get<int>("max_tracked_tokens");
            JObject resource_dirs = Configuration.Get<JObject>("resource_dirs");
            string tokens_dir = resource_dirs["tokens"]!.ToString();

            for (int i = 1; i <= max_cnt; i++)
            {
                int token_id = -1;
                if (it.MoveNext())
                {
                    token_id = it.Current.Key - (int)EActionCard.NumSharables;
                }

                string imageFileForOBS = Path.Combine(WorkingDir, $"token{i}.png");
                string countFileForOBS = Path.Combine(WorkingDir, $"token{i}.txt");
                string imageSrcPath;
                if (token_id >= 0)
                {
                    imageSrcPath = Path.Combine(tokens_dir, $"{token_id}.png");
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
}