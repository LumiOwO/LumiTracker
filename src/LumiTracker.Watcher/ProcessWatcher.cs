using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using LumiTracker.Config;
using Newtonsoft.Json.Linq;


namespace LumiTracker.Watcher
{
    public class CaptureInfo
    {
        public EClientType ClientType { get; set; } = EClientType.YuanShen;
        public ECaptureType CaptureType { get; set; } = ECaptureType.BitBlt;

        // Should be assigned by FindProcessWindow()
        public IntPtr hwnd { get; set; }  = IntPtr.Zero;
        public string title { get; set; } = "";
    }

    public class ProcessWatcher : GameEventHook, IAsyncDisposable
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public ProcessWatcher()
        {
            LogFPS += OnLogFPS;
        }

        public void FindProcessWindow(CaptureInfo info, string processName)
        {
            // Reset window info
            info.hwnd  = IntPtr.Zero;
            info.title = "";

            //////////////////////////
            // Find process id by name
            var pids = new HashSet<uint>();
            foreach (var proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)))
            {
                pids.Add((uint)proc.Id);
            }
            if (pids.Count == 0)
            {
                Configuration.Logger.LogDebug($"No process found with name: {processName}");
                return;
            }
            //Configuration.Logger.LogDebug($"Found {pids.Count} processes with name: {processName}");

            //////////////////////////
            // Find hwnd by process id
            var hwnds = GetWindowHandlesByPids(pids);
            if (hwnds.Count == 0 || hwnds[0] == 0)
            {
                Configuration.Logger.LogDebug($"No window found for process '{processName}' (PIDs: [{string.Join(',', pids)}])");
                return;
            }
            // GetProcessByName() ensures that the first process has the largest window
            var hwnd = hwnds[0];
            if (hwnds.Count > 1)
            {
                Configuration.Logger.LogWarning($"Found {hwnds.Count} windows with name: {processName}, using the largest one.");
            }

            //////////////////////////
            // Get window info by hwnd
            GetMainWindowInfo(info, hwnd);
            Configuration.Logger.LogDebug("[Processwatcher] OnGenshinWindowFound");
            InvokeGenshinWindowFound();

            Configuration.Logger.LogInformation($"Window title for process '{processName}' (hwnd: {hwnd}): {info.title}");
        }

        private List<IntPtr> GetWindowHandlesByPids(HashSet<uint> processIds)
        {
            int largestArea  = 0;
            int largestIndex = 0;

            var windowHandles = new List<IntPtr>();
            EnumWindows((hwnd, lParam) =>
            {
                uint id;
                GetWindowThreadProcessId(hwnd, out id);
                if (!IsWindowVisible(hwnd) || !processIds.Contains(id)) return true; // Continue enumerating

                var rect = new RECT();
                GetClientRect(hwnd, out rect);
                int area = (rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                // Check if it's the largest
                if (area > largestArea)
                {
                    largestArea  = area;
                    largestIndex = windowHandles.Count;
                }
                //Configuration.Logger.LogDebug($"{area}, {hwnd}");

                windowHandles.Add(hwnd);
                return true; // Continue enumerating
            }, IntPtr.Zero);


            // Swap the largest window to index 0
            if (largestIndex > 0)
            {
                IntPtr temp = windowHandles[largestIndex];
                windowHandles[largestIndex] = windowHandles[0];
                windowHandles[0] = temp;
            }

            return windowHandles;
        }

        private void GetMainWindowInfo(CaptureInfo info, IntPtr hwnd)
        {
            if (IsWindow(hwnd))
            {
                info.hwnd  = hwnd;
                info.title = GetWindowText(hwnd);
            }
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            const int nChars = 256;
            var Buff = new StringBuilder(nChars);
            return GetWindowText(hWnd, Buff, nChars) > 0 ? Buff.ToString() : "";
        }

        private static readonly float LOG_INTERVAL = Configuration.Get<float>("LOG_INTERVAL");

        private SpinLock cancelLock = new SpinLock();
        private bool _ShouldCancel = false;
        private bool ShouldCancel 
        { 
            get 
            {
                using var guard = new SpinLockGuard(ref cancelLock);
                return _ShouldCancel; 
            } 
            set 
            {
                using var guard = new SpinLockGuard(ref cancelLock);
                _ShouldCancel = value;
            } 
        }

        private Task? _processWatcherTask;

        private Task? _windowWatcherTask;

        private IBackend? backend = null;

        public async ValueTask DisposeAsync()
        {
            ShouldCancel = true;

            if (_windowWatcherTask != null)
            {
                await _windowWatcherTask;
            }
            if (_processWatcherTask != null)
            {
                await _processWatcherTask;
            }
        }

        public void Start(CaptureInfo info)
        {
            _processWatcherTask = StartProcessWatcher(info);
        }

        public async Task StartProcessWatcher(CaptureInfo info)
        {
            string[] processList = EnumHelpers.GetClientProcessList(info.ClientType);
            Configuration.Logger.LogInformation($"Begin to find process: [{string.Join(", ", processList)}]");
            try
            {
                int interval = Configuration.Get<int>("proc_watch_interval") * 1000;
                while (!ShouldCancel)
                {
                    foreach (string processName in processList)
                    {
                        FindProcessWindow(info, processName);
                        if (info.hwnd != IntPtr.Zero)
                        {
                            _windowWatcherTask = RunBackend(info, interval);
                            await _windowWatcherTask;
                            break;
                        }
                    }
                    await Task.Delay(interval);
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[ProcessWatcher] {ex.ToString()}");
                InvokeException(ex);
            }
        }

        public async Task RunBackend(CaptureInfo info, int interval)
        {
            Configuration.Logger.LogInformation($"Begin to start window watcher, hwnd = {info.hwnd}");

            // Start backend
            bool success = true;
            backend = BackendFactory.Create(EBackend.Python);
            success = backend.Init(info);
            if (!success) return;
            success = await backend.StartAsync();
            if (!success) return;
            backend.MessageReceived += BackendMessageHandler;

            // Main loop
            Configuration.Logger.LogDebug("[Processwatcher] OnWindowWatcherStart");
            InvokeWindowWatcherStart(info.hwnd);

            while (!backend.HasExited())
            {
                if (ShouldCancel)
                {
                    backend.Kill();
                    break;
                }
                await Task.Delay(interval);
            }

            Configuration.Logger.LogInformation($"Backend terminated, hwnd = {info.hwnd}");
            Configuration.Logger.LogDebug("[Processwatcher] OnWindowWatcherExit");
            InvokeWindowWatcherExit();
        }

        public async Task DumpToBackend(object message_obj)
        {
            Debug.Assert(message_obj != null && backend != null);
            if (backend.HasExited()) return;
            await backend.Send(message_obj);
        }

        private void BackendMessageHandler(JObject? message, Exception? exception)
        {
            if (exception != null)
            {
                Configuration.Logger.LogError($"[ProcessWatcher] {exception.ToString()}");
                InvokeException(exception);
                return;
            }

            Debug.Assert(message != null);
            try
            {
                string  message_level = message["level"]!.ToString();
                var     message_data  = message["data"]!;

                bool forceIndent   = (message_level != "DEBUG" && message_level != "INFO");
                string message_str = $"{LogHelper.AnsiMagenta}@{LogHelper.AnsiEnd} ";
                message_str += LogHelper.JsonToConsoleStr(message_data, forceIndent);

                if (message_level == "INFO")
                {
                    if (!ParseBackendMessage(message_data, message_str))
                    {
                        Configuration.Logger.LogInformation(message_str);
                    }
                }
                else if (message_level == "DEBUG")
                {
                    Configuration.Logger.LogDebug(message_str);
                }
                else if (message_level == "WARNING")
                {
                    Configuration.Logger.LogWarning(message_str);
                }
                else if (message_level == "ERROR")
                {
                    Configuration.Logger.LogError(message_str);
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[ProcessWatcher] {ex.ToString()}");
                InvokeException(ex);
            }
        }

        private bool ParseBackendMessage(JToken message_data, string message_str)
        {
            GameEventMessage? message = null;
            try
            {
                message = message_data.ToObject<GameEventMessage>();
                if (message == null)
                {
                    throw new Exception("ToObject<GameEventMessage> failed.");
                }
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"JSON deserialization Error: {ex.Message}");
                return false;
            }

            if (message.Event != EGameEvent.LogFps)
            {
                Configuration.Logger.LogInformation(message_str);
            }

            ParseGameEventMessage(message);
            return true;
        }

        private long?  _last_fps_time = null;
        private double _fps_sum       = 0;

        private void OnLogFPS(float fps)
        {
            if (_last_fps_time == null)
            {
                _last_fps_time = Stopwatch.GetTimestamp();
            }
            _fps_sum += fps;

            long last_fps_time    = (long)_last_fps_time;
            long cur_fps_time     = Stopwatch.GetTimestamp();
            double elapsedSeconds = (cur_fps_time - last_fps_time) / (double)Stopwatch.Frequency;

            if (elapsedSeconds >= LOG_INTERVAL)
            {
                string message_str = $"{LogHelper.AnsiMagenta}@{LogHelper.AnsiEnd} FPS = {_fps_sum / elapsedSeconds}";
                Configuration.Logger.LogInformation(message_str);

                _last_fps_time = cur_fps_time;
                _fps_sum       = 0;
            }
        }
    }

    // Main method to start the process watcher
    public static class Program
    {
        public static void Main(string[] args)
        {
            var processWatcher = new ProcessWatcher();
            processWatcher.Start( new CaptureInfo { ClientType = EClientType.YuanShen, CaptureType = ECaptureType.BitBlt } );
        }
    }

}
