using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

using LumiTracker.Config;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Windows.Foundation.Metadata;
using System.Net.Sockets;
using System.Net;


namespace LumiTracker.Watcher
{
    public class WindowInfo
    {
        public IntPtr hwnd { get; set; }  = IntPtr.Zero;
        public string title { get; set; } = "";
    }

    public struct CaptureInfo
    {
        public EClientType ClientType { get; set; }
        public ECaptureType CaptureType { get; set; }
        public string InitFilePath { get; set; }
        public bool TestCaptureOnResize { get; set; }
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

        public WindowInfo FindProcessWindow(string processName)
        {
            var res = new WindowInfo();

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
                return res;
            }
            //Configuration.Logger.LogDebug($"Found {pids.Count} processes with name: {processName}");

            //////////////////////////
            // Find hwnd by process id
            var hwnds = GetWindowHandlesByPids(pids);
            if (hwnds.Count == 0 || hwnds[0] == 0)
            {
                Configuration.Logger.LogDebug($"No window found for process '{processName}' (PIDs: [{string.Join(',', pids)}])");
                return res;
            }
            // GetProcessByName() ensures that the first process has the largest window
            var hwnd = hwnds[0];
            if (hwnds.Count > 1)
            {
                Configuration.Logger.LogWarning($"Found {hwnds.Count} windows with name: {processName}, using the largest one.");
            }

            //////////////////////////
            // Get window info by hwnd
            var info = GetMainWindowInfo(hwnd);
            Configuration.Logger.LogDebug("[Processwatcher] OnGenshinWindowFound");
            InvokeGenshinWindowFound();

            Configuration.Logger.LogInformation($"Window title for process '{processName}' (hwnd: {hwnd}): {info.title}");
            
            res = info;
            return res;
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

        private WindowInfo GetMainWindowInfo(IntPtr hwnd)
        {
            var info = new WindowInfo();

            if (IsWindow(hwnd))
            {
                info.hwnd  = hwnd;
                info.title = GetWindowText(hwnd);
            }

            return info;
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            const int nChars = 256;
            var Buff = new StringBuilder(nChars);
            return GetWindowText(hWnd, Buff, nChars) > 0 ? Buff.ToString() : "";
        }

        private static readonly float LOG_INTERVAL = Configuration.Get<float>("LOG_INTERVAL");
        private SpinLockedValue<long> _last_fps_time = new(Stopwatch.GetTimestamp());
        private SpinLockedValue<bool> ShouldCancel = new (false);
        private Task? _processWatcherTask;
        private Task? _windowWatcherTask;
        public Socket? BackendSocket { get; private set; } = null;

        public async ValueTask DisposeAsync()
        {
            ShouldCancel.Value = true;
            if (_windowWatcherTask != null)
            {
                await _windowWatcherTask;
            }
            if (_processWatcherTask != null)
            {
                await _processWatcherTask;
            }
        }

        public void Start(CaptureInfo captureInfo)
        {
            _processWatcherTask = StartProcessWatcher(captureInfo);
        }

        public async Task StartProcessWatcher(CaptureInfo captureInfo)
        {
            string[] processList = EnumHelpers.GetClientProcessList(captureInfo.ClientType);
            Configuration.Logger.LogInformation($"Begin to find process: [{string.Join(", ", processList)}]");
            try
            {
                int interval = Configuration.Get<int>("proc_watch_interval") * 1000;
                while (!ShouldCancel.Value)
                {
                    foreach (string processName in processList)
                    {
                        var info = FindProcessWindow(processName);
                        if (info.hwnd != IntPtr.Zero)
                        {
                            _windowWatcherTask = StartWindowWatcher(info, captureInfo, interval);
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

        public async Task StartWindowWatcher(WindowInfo info, CaptureInfo captureInfo, int interval)
        {
            Configuration.Logger.LogInformation($"Begin to start window watcher");

            //////////////////////////
            // Prepare start info
            string clientType = captureInfo.ClientType.ToString();
            string captureType = EnumHelpers.BitBltUnavailable(captureInfo.ClientType) ?
                ECaptureType.WindowsCapture.ToString() :
                captureInfo.CaptureType.ToString();

            bool canHideBorder = ApiInformation.IsPropertyPresent(
                "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");

            // Grab available port
            var tempSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tempSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)tempSocket!.LocalEndPoint!).Port;
            tempSocket.Close();

            // Save init parameters to json file
            bool saved = Configuration.SaveJObject(JObject.FromObject(new
            {
                hwnd            = info.hwnd.ToInt64(),
                client_type     = clientType,
                capture_type    = captureType,
                can_hide_border = canHideBorder ? 1 : 0,
                port            = port,
                log_dir         = Configuration.LogDir,
                test_on_resize  = captureInfo.TestCaptureOnResize ? 1 : 0,
            }), captureInfo.InitFilePath);
            if (!saved)
            {
                Configuration.Logger.LogError("Failed to save ProcessWatcher init file.");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Configuration.AppDir, "python", "python.exe"),
                Arguments = $"-E -m watcher.window_watcher \"{captureInfo.InitFilePath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Configuration.AppDir,
            };

            //////////////////////////
            // Create backend process
            var process = new Process();
            process.StartInfo = startInfo;
            process.ErrorDataReceived += WindowWatcherEventHandler;

            if (!process.Start())
            {
                Configuration.Logger.LogError("Failed to start subprocess.");
                return;
            }
            ChildProcessTracker.AddProcess(process);
            process.BeginErrorReadLine();

            var KillProcess = () => 
            {
                BackendSocket?.Dispose();
                BackendSocket = null;
                process.Kill();
            };

            //////////////////////////
            // Connect backend socket
            try
            {
                BackendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await BackendSocket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"Failed to connect to backend socket.\n{ex.ToString()}");
                KillProcess();
                return;
            }
            Configuration.Logger.LogInformation($"Connected to backend socket on port {port}.");

            //////////////////////////
            // Main loop
            Configuration.Logger.LogDebug("[Processwatcher] OnWindowWatcherStart");
            InvokeWindowWatcherStart(info.hwnd);
            while (!process.HasExited)
            {
                if (ShouldCancel.Value)
                {
                    KillProcess();
                }
                await Task.Delay(interval);
            }

            Configuration.Logger.LogInformation($"Subprocess terminated with exit code: {process.ExitCode}");
            Configuration.Logger.LogDebug("[Processwatcher] OnWindowWatcherExit");
            InvokeWindowWatcherExit();
        }

        public async Task DumpToBackend(object message_obj)
        {
            string message_str = "";
            try
            {
                message_str = LogHelper.JsonToConsoleStr(JObject.FromObject(message_obj), forceCompact: true);
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[DumpToBackend] Failed to parse message object. \n{ex.ToString()}");
                return;
            }

            var socket = BackendSocket;
            if (socket != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(message_str + "\n");
                await Task.Factory.FromAsync(
                    (callback, state) => socket.BeginSend(data, 0, data.Length, SocketFlags.None, callback, state),
                    socket.EndSend,
                    null);
            }
        }

        private void WindowWatcherEventHandler(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e.Data == null) return;

                JObject message = JObject.Parse(e.Data);
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
            catch (JsonReaderException ex)
            {
                Configuration.Logger.LogError($"[python] {e.Data}");
                InvokeException(ex);
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
                Configuration.Logger.LogError($"JSON Serialization Error: {ex.Message}");
                return false;
            }

            if (message.Event != EGameEvent.LOG_FPS)
            {
                Configuration.Logger.LogInformation(message_str);
            }

            ParseGameEventMessage(message);
            return true;
        }

        private void OnLogFPS(float fps)
        {
            long cur_fps_time = Stopwatch.GetTimestamp();
            float elapsedSeconds = (cur_fps_time - _last_fps_time.Value) / (float)Stopwatch.Frequency;
            if (elapsedSeconds >= LOG_INTERVAL)
            {
                string message_str = $"{LogHelper.AnsiMagenta}@{LogHelper.AnsiEnd} FPS = {fps}";
                Configuration.Logger.LogInformation(message_str);
                _last_fps_time.Value = cur_fps_time;
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
