using LumiTracker.Config;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using System.IO;

namespace LumiTracker.Models
{
    public enum EGameWatcherState
    {
        NoWindowFound,
        StartingWindowWatcher,
        WindowWatcherStarted,

        NumGameWatcherStates,
        Invalid = NumGameWatcherStates,
    }

    public class GameWatcher
    {
        private Task? MainLoopTask = null;

        private SpinLockedValue<CaptureInfo> info = new (default);

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

        public event OnCaptureTestDoneCallback?     CaptureTestDone;

        public event OnLogFPSCallback?              LogFPS;

        private readonly string ProcessWatcherInitFilePath;

        private readonly bool TestCaptureOnResize;

        public GameWatcher()
        {
            ProcessWatcherInitFilePath = Path.Combine(Configuration.DocumentsDir, "init.json");
            TestCaptureOnResize = false;
        }

        public GameWatcher(string processWatcherInitFilePath, bool testCaptureOnResize)
        {
            ProcessWatcherInitFilePath = processWatcherInitFilePath;
            TestCaptureOnResize = testCaptureOnResize;
        }

        public void Start(EClientType clientType, ECaptureType captureType)
        {
            info.Value = new CaptureInfo 
            { 
                ClientType = clientType, 
                CaptureType = captureType,
                InitFilePath = ProcessWatcherInitFilePath,
                TestCaptureOnResize = TestCaptureOnResize,
            };
            MainLoopTask = MainLoop();
        }

        public void ChangeGameClient(EClientType clientType, ECaptureType captureType)
        {
            info.Value = new CaptureInfo
            {
                ClientType = clientType,
                CaptureType = captureType,
                InitFilePath = ProcessWatcherInitFilePath,
                TestCaptureOnResize = TestCaptureOnResize,
            };
            StopCurrentProcessWatcher();
        }

        public EClientType ClientType
        {
            get { return info.Value.ClientType; }
        }

        public async Task DumpToBackend(object message_obj)
        {
            if (processWatcher.Value == null)
                return;

            await processWatcher.Value.DumpToBackend(message_obj);
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
                watcher.CaptureTestDone    += OnCaptureTestDone;
                watcher.LogFPS             += OnLogFPS;
                watcher.ExceptionHandler   += OnException;
                processWatcher.Value = watcher;

                watcher.Start(info.Value!);
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
            Configuration.Logger.LogDebug("[GameWatcher] OnGenshinWindowFound");
            GenshinWindowFound?.Invoke();
        }

        private void OnWindowWatcherStart(IntPtr hwnd)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnWindowWatcherStart");
            WindowWatcherStart?.Invoke(hwnd);
        }

        private void OnWindowWatcherExit()
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnWindowWatcherExit");
            WindowWatcherExit?.Invoke();
        }

        private void OnGameStarted()
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnGameStarted");
            GameStarted?.Invoke();
        }

        private void OnMyActionCardPlayed(int card_id)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnMyActionCard");
            MyActionCardPlayed?.Invoke(card_id);
        }

        private void OnOpActionCardPlayed(int card_id)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnOpActionCard");
            OpActionCardPlayed?.Invoke(card_id);
        }

        private void OnGameOver()
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnGameOver");
            GameOver?.Invoke();
        }

        private void OnRoundDetected(int round)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnRoundDetected");
            RoundDetected?.Invoke(round);
        }

        private void OnMyCardsDrawn(int[] card_ids)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnMyCardsDrawn");
            MyCardsDrawn?.Invoke(card_ids);
        }

        private void OnMyCardsCreateDeck(int[] card_ids)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnMyCardsCreateDeck");
            MyCardsCreateDeck?.Invoke(card_ids);
        }

        private void OnOpCardsCreateDeck(int[] card_ids)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnOpCardsCreateDeck");
            OpCardsCreateDeck?.Invoke(card_ids);
        }

        private void OnUnsupportedRatio()
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnUnsupportedRatio");
            UnsupportedRatio?.Invoke();
        }

        private void OnCaptureTestDone(string filename, int width, int height)
        {
            Configuration.Logger.LogDebug("[GameWatcher] OnCaptureTestDone");
            CaptureTestDone?.Invoke(filename, width, height);
        }

        private void OnLogFPS(float fps)
        {
            //Configuration.Logger.LogDebug("[GameWatcher] OnLogFPS");
            LogFPS?.Invoke(fps);
        }

        private void OnException(Exception e)
        {
            // do nothing
        }
    }
}
