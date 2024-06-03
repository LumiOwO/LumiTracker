using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LumiTracker.Config;
using LumiTracker.ViewModels.Windows;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Appearance;

namespace LumiTracker.Models
{
    public enum EGameWatcherState
    {
        NoWindowFound,
        WindowNotForeground,
        WindowWatcherStarted
    }

    public class GameWatcher
    {
        private SpinLockedValue<string> processName = new ("");

        private SpinLockedValue<ProcessWatcher> processWatcher = new (null);

        private SpinLockedValue<Task> stopProcessWatcherTask = new (null);

        public event OnGenshinWindowFoundCallback? GenshinWindowFound;

        public event OnWindowWatcherStartCallback? WindowWatcherStart;

        public event OnWindowWatcherExitCallback?  WindowWatcherExit;

        public event OnGameStartedCallback? GameStarted;

        public event OnMyEventCardCallback? MyEventCard;

        public event OnOpEventCardCallback? OpEventCard;

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
            var logger = Configuration.Logger;
            var cfg    = Configuration.Data;

            int interval = cfg.proc_watch_interval * 1000;
            while (true)
            {
                while (processWatcher.Value != null)
                {
                    await Task.Delay(interval);
                }

                ProcessWatcher watcher = new(logger, cfg);
                watcher.GenshinWindowFound += OnGenshinWindowFound;
                watcher.WindowWatcherStart += OnWindowWatcherStart;
                watcher.WindowWatcherExit  += OnWindowWatcherExit;
                watcher.MyEventCard        += OnMyEventCard;
                watcher.OpEventCard        += OnOpEventCard;
                watcher.GameStarted        += OnGameStarted;
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

        private void OnMyEventCard(int card_id)
        {
            Configuration.Logger.LogDebug("OnMyEventCard");
            MyEventCard?.Invoke(card_id);
        }

        private void OnOpEventCard(int card_id)
        {
            Configuration.Logger.LogDebug("OnOpEventCard");
            OpEventCard?.Invoke(card_id);
        }
    }
}
