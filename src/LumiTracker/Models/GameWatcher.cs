﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LumiTracker.Config;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;

namespace LumiTracker.Models
{
    public record Person
    {
        public string FirstName { get; init; }

        public string LastName { get; init; }

        public string Name => $"{FirstName} {LastName}";

        public string Company { get; init; }

        public Person(string firstName, string lastName, string company)
        {
            FirstName = firstName;
            LastName = lastName;
            Company = company;
        }
    }

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
    }
}
