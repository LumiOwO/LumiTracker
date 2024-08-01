using LumiTracker.Config;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;

namespace LumiTracker.Models
{
    public enum EGameWatcherState
    {
        NoWindowFound,
        WindowNotForeground,
        WindowWatcherStarted,

        NumGameWatcherStates,
        Invalid = NumGameWatcherStates,
    }

    public class GameWatcher
    {
        private SpinLockedValue<string> processName = new ("");

        private SpinLockedValue<ProcessWatcher> processWatcher = new (null);

        private SpinLockedValue<Task> stopProcessWatcherTask = new (null);

        public event OnGenshinWindowFoundCallback?  GenshinWindowFound;

        public event OnWindowWatcherStartCallback?  WindowWatcherStart;

        public event OnWindowWatcherExitCallback?   WindowWatcherExit;

        public event OnGameStartedCallback?         GameStarted;

        public event OnMyActionCardPlayedCallback?  MyActionCardPlayed;

        public event OnOpActionCardPlayedCallback?  OpActionCardPlayed;

        public event OnGameOverCallback?            GameOver;

        public event OnRoundDetectedCallback?       RoundDetected;

        public event OnMyCardsDrawnCallback?        MyCardsDrawn;

        public event OnMyCardsCreateDeckCallback?   MyCardsCreateDeck;

        public event OnOpCardsCreateDeckCallback?   OpCardsCreateDeck;

        public event OnUnsupportedRatioCallback?    UnsupportedRatio;

        public GameWatcher()
        {

        }

        public void Start(string name)
        {
            processName.Value = name;
            Task mainLoop = MainLoop();
        }

        public void ChangeGameClient(string name)
        {
            processName.Value = name;
            StopCurrentProcessWatcher();
        }

        private async Task MainLoop()
        {
            int interval = Configuration.Get<int>("proc_watch_interval") * 1000;
            while (true)
            {
                while (processWatcher.Value != null)
                {
                    await Task.Delay(interval);
                }

                ProcessWatcher watcher = new ();
                watcher.GenshinWindowFound += OnGenshinWindowFound;
                watcher.WindowWatcherStart += OnWindowWatcherStart;
                watcher.WindowWatcherExit  += OnWindowWatcherExit;
                watcher.GameStarted        += OnGameStarted;
                watcher.MyActionCardPlayed += OnMyActionCardPlayed;
                watcher.OpActionCardPlayed += OnOpActionCardPlayed;
                watcher.GameOver           += OnGameOver;
                watcher.RoundDetected      += OnRoundDetected;
                watcher.MyCardsDrawn       += OnMyCardsDrawn;
                watcher.MyCardsCreateDeck  += OnMyCardsCreateDeck;
                watcher.OpCardsCreateDeck  += OnOpCardsCreateDeck;
                watcher.UnsupportedRatio   += OnUnsupportedRatio;
                watcher.ExceptionHandler   += OnException;
                processWatcher.Value = watcher;

                watcher.Start(processName.Value!);
            }
        }

        private void StopCurrentProcessWatcher()
        {
            if (stopProcessWatcherTask.Value == null)
            {
                stopProcessWatcherTask.Value = Task.Run(async () =>
                {
                    ProcessWatcher? watcher = processWatcher.Value;
                    if (watcher != null)
                    {
                        await watcher.DisposeAsync();
                        processWatcher.Value = null;
                    }
                    stopProcessWatcherTask.Value = null;
                });
            }
        }

        private void OnGenshinWindowFound()
        {
            Configuration.Logger.LogDebug("OnGenshinWindowFound");
            GenshinWindowFound?.Invoke();
        }

        private void OnWindowWatcherStart(IntPtr hwnd)
        {
            Configuration.Logger.LogDebug("OnWindowWatcherStart");
            WindowWatcherStart?.Invoke(hwnd);
        }

        private void OnWindowWatcherExit()
        {
            Configuration.Logger.LogDebug("OnWindowWatcherExit");
            WindowWatcherExit?.Invoke();
        }

        private void OnGameStarted()
        {
            Configuration.Logger.LogDebug("OnGameStarted");
            GameStarted?.Invoke();
        }

        private void OnMyActionCardPlayed(int card_id)
        {
            Configuration.Logger.LogDebug("OnMyActionCard");
            MyActionCardPlayed?.Invoke(card_id);
        }

        private void OnOpActionCardPlayed(int card_id)
        {
            Configuration.Logger.LogDebug("OnOpActionCard");
            OpActionCardPlayed?.Invoke(card_id);
        }

        private void OnGameOver()
        {
            Configuration.Logger.LogDebug("OnGameOver");
            GameOver?.Invoke();
        }

        private void OnRoundDetected(int round)
        {
            Configuration.Logger.LogDebug("OnRoundDetected");
            RoundDetected?.Invoke(round);
        }

        private void OnMyCardsDrawn(int[] card_ids)
        {
            Configuration.Logger.LogDebug("OnMyCardsDrawn");
            MyCardsDrawn?.Invoke(card_ids);
        }

        private void OnMyCardsCreateDeck(int[] card_ids)
        {
            Configuration.Logger.LogDebug("OnMyCardsCreateDeck");
            MyCardsCreateDeck?.Invoke(card_ids);
        }

        private void OnOpCardsCreateDeck(int[] card_ids)
        {
            Configuration.Logger.LogDebug("OnOpCardsCreateDeck");
            OpCardsCreateDeck?.Invoke(card_ids);
        }

        private void OnUnsupportedRatio()
        {
            Configuration.Logger.LogDebug("OnUnsupportedRatio");
            UnsupportedRatio?.Invoke();
        }
        
        private void OnException(Exception e)
        {
            // do nothing
        }
    }
}
