using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LumiTracker.Config;
using LumiTracker.Watcher;
using Microsoft.Extensions.Logging;

namespace LumiTracker.Models
{
    public class GameWatcher
    {
        private ProcessWatcher? processWatcher;

        private Task? watcherTask;

        public event OnGenshinWindowFoundCallback? GenshinWindowFound;

        public event OnWindowWatcherExitCallback?  WindowWatcherExit;

        public void Start()
        {
            var logger = Configuration.Logger;
            var cfg = Configuration.Data;

            processWatcher = new ProcessWatcher(logger, cfg);
            processWatcher.GenshinWindowFound += OnGenshinWindowFound;
            processWatcher.WindowWatcherExit  += OnWindowWatcherExit;

            watcherTask = processWatcher.Start();
        }

        private void OnGenshinWindowFound()
        {
            Configuration.Logger.LogDebug("OnGenshinWindowFound");
            GenshinWindowFound?.Invoke();
        }

        private void OnWindowWatcherExit()
        {
            Configuration.Logger.LogDebug("OnWindowWatcherExit");
            WindowWatcherExit?.Invoke();
        }
    }
}
