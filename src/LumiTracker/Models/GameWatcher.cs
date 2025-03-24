using LumiTracker.Config;
using LumiTracker.OB.Services;
using LumiTracker.Watcher;
using System.Diagnostics;

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

    public class GameWatcher : GameEventHook
    {
        private Task? MainLoopTask = null;

        private OBClientService? client = null;

        // Critical section
        private SpinLock mLock = new SpinLock();
        private CaptureInfo? info = null;
        private ProcessWatcher? processWatcher = null;

        public GameWatcher()
        {
            GameEventMessage += async message =>
            {
                await SendMessageToServer(message);
            };
        }

        public void Start(EClientType clientType, ECaptureType captureType)
        {
            using (new SpinLockGuard(ref mLock))
            {
                info = new CaptureInfo 
                { 
                    ClientType = clientType, 
                    CaptureType = captureType,
                };
            }
            MainLoopTask = MainLoop();
        }

        public void Close()
        {
            if (client != null)
            {
                client.Close();
                client = null;
            }
        }

        public void ChangeGameClient(EClientType clientType, ECaptureType captureType)
        {
            using (new SpinLockGuard(ref mLock))
            {
                info = new CaptureInfo
                {
                    ClientType = clientType,
                    CaptureType = captureType,
                };
            }
            StopCurrentProcessWatcher();
        }

        public EClientType? ClientType
        {
            get
            {
                using var guard = new SpinLockGuard(ref mLock);
                return info?.ClientType;
            }
        }

        public async Task DumpToBackend(object message_obj)
        {
            var watcher = SpinLockGuard.Scope(ref mLock, () => processWatcher);
            if (watcher != null)
            {
                await watcher.DumpToBackend(message_obj);
            }
        }

        public async Task<bool> ConnectToServer(string host, int port)
        {
            client = new OBClientService(host, port);
            bool success = await client.ConnectAsync();
            return success;
        }

        public void AddServerDisconnectedCallback(OnServerDisconnectedCallback callback)
        {
            if (client != null)
            {
                client.ServerDisconnected += callback;
            }
        }

        public async Task SendMessageToServer(GameEventMessage message)
        {
            if (client == null || !client.Connected())
                return;

            await client.SendMessageAsync(message);
        }

        private async Task MainLoop()
        {
            int interval = Configuration.Get<int>("proc_watch_interval") * 1000;
            while (true)
            {
                ProcessWatcher? watcher = null;
                while (true)
                {
                    watcher = SpinLockGuard.Scope(ref mLock, () => processWatcher);
                    if (watcher == null) break;

                    await Task.Delay(interval);
                }

                watcher = new ProcessWatcher();
                this.HookTo(watcher);

                var curInfo = SpinLockGuard.Scope(ref mLock, () =>
                {
                    Debug.Assert(info != null);
                    processWatcher = watcher;
                    return info;
                });
                watcher.Start(curInfo);
            }
        }

        private void StopCurrentProcessWatcher()
        {
            ProcessWatcher? watcher = null;
            using (new SpinLockGuard(ref mLock))
            {
                watcher = processWatcher;
                processWatcher = null;
            }

            if (watcher != null)
            {
                this.UnhookFrom(watcher);
                var task = watcher.DisposeAsync();
            }
        }
    }
}
