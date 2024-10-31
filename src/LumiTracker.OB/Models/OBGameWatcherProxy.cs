using LumiTracker.Config;
using LumiTracker.OB.ViewModels.Pages;
using LumiTracker.ViewModels.Pages;
using LumiTracker.ViewModels.Windows;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using System.IO;

namespace LumiTracker.OB
{
    public class OBGameWatcherProxy
    {
        private ScopeState          Scope;
        private GameEventHook       Hook;
        private DeckViewModel       DeckViewModel;
        public  DeckWindowViewModel DeckWindowViewModel { get; }
        private string              WorkingDir;

        private ClientInfo ClientInfo;

        public OBGameWatcherProxy(ScopeState scope, ClientInfo clientInfo)
        {
            ClientInfo = clientInfo;
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
            if (message.Event == EGameEvent.INITIAL_DECK)
            {
                string sharecode = message.Data["sharecode"].ToString();
                DeckWindowViewModel.InitDeckOnGameStart(sharecode);
            }
            else
            {
                Hook.ParseGameEventMessage(message);
            }
        }

        private void ResetOBData()
        {
            // When this called, the tracked card list is already empty
            // so this will set all tracked images to empty.png
            try
            {
                if (!Directory.Exists(WorkingDir))
                {
                    Directory.CreateDirectory(WorkingDir);
                }
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
            int max_cnt = ClientInfo.ViewModel.MaxTrackedTokens;
            string tokens_dir = ClientInfo.ViewModel.TokensIconDir;
            bool has_tokens_dir = Directory.Exists(tokens_dir);

            for (int i = 1; i <= max_cnt; i++)
            {
                int token_id = -1;
                if (it.MoveNext())
                {
                    token_id = it.Current.Key - (int)EActionCard.NumSharables;
                }

                string imageFileForOBS = Path.Combine(WorkingDir, $"token{i}.png");
                string countFileForOBS = Path.Combine(WorkingDir, $"token{i}.txt");
                string imageSrcPath = Path.Combine(Configuration.AssetsDir, "images", "empty.png");
                if (token_id >= 0 && has_tokens_dir)
                {
                    string path = Path.Combine(tokens_dir, $"{token_id}.png");
                    if (File.Exists(path))
                    {
                        imageSrcPath = path;
                    }
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
            ClientInfo.GameStarted = true;
        }

        private void OnGameOver()
        {
            ResetOBData();
            ClientInfo.GameStarted = false;
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