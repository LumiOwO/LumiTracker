using LumiTracker.Config;
using LumiTracker.OB.Services;
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

    public class GameWatcher : GameEventHook
    {
        private Task? MainLoopTask = null;

        private SpinLockedValue<CaptureInfo> info = new (default);

        private SpinLockedValue<ProcessWatcher> processWatcher = new (null);

        private SpinLockedValue<Task> stopProcessWatcherTask = new (null);

        private readonly string ProcessWatcherInitFilePath;

        private readonly bool TestCaptureOnResize;

        private OBClientService? client = null;

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

        public async Task<bool> ConnectToServer(string host, int port)
        {
            client = new OBClientService(host, port);
            bool success = await client.ConnectAsync();
            if (success)
            {
                GameEventMessage += async (GameEventMessage message) => { await SendMessageToServer(message); };
            }
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
                while (processWatcher.Value != null)
                {
                    await Task.Delay(interval);
                }

                ProcessWatcher watcher = new ();
                HookTo(watcher);
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
    }
}
