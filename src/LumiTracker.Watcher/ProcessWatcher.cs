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
    }

    public delegate void OnGenshinWindowFoundCallback();

    public delegate void OnWindowWatcherStartCallback(IntPtr hwnd);

    public delegate void OnWindowWatcherExitCallback();

    public delegate void OnGameStartedCallback();

    public delegate void OnMyActionCardPlayedCallback(int card_id);

    public delegate void OnOpActionCardPlayedCallback(int card_id);

    public delegate void OnGameOverCallback();

    public delegate void OnRoundDetectedCallback(int round);

    public delegate void OnMyCardsDrawnCallback(int[] card_ids);

    public delegate void OnMyCardsCreateDeckCallback(int[] card_ids);

    public delegate void OnOpCardsCreateDeckCallback(int[] card_ids);

    public delegate void OnUnsupportedRatioCallback();

    public delegate void OnCaptureTestDoneCallback(string filename, int width, int height);

    public delegate void OnLogFPSCallback(float fps);

    public delegate void ExceptionHandlerCallback(Exception ex);

    public class ProcessWatcher : IAsyncDisposable
    {
        public event OnGenshinWindowFoundCallback? GenshinWindowFound;
        public event OnWindowWatcherStartCallback? WindowWatcherStart;
        public event OnWindowWatcherExitCallback?  WindowWatcherExit;
        public event OnGameStartedCallback?        GameStarted;
        public event OnMyActionCardPlayedCallback? MyActionCardPlayed;
        public event OnOpActionCardPlayedCallback? OpActionCardPlayed;
        public event OnGameOverCallback?           GameOver;
        public event OnRoundDetectedCallback?      RoundDetected;
        public event OnMyCardsDrawnCallback?       MyCardsDrawn;
        public event OnMyCardsCreateDeckCallback?  MyCardsCreateDeck;
        public event OnOpCardsCreateDeckCallback?  OpCardsCreateDeck;
        public event OnUnsupportedRatioCallback?   UnsupportedRatio;
        public event OnCaptureTestDoneCallback?    CaptureTestDone;
        public event OnLogFPSCallback?             LogFPS;
        public event ExceptionHandlerCallback?     ExceptionHandler;


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
            GenshinWindowFound?.Invoke();

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
                ExceptionHandler?.Invoke(ex);
            }
        }

        public async Task StartWindowWatcher(WindowInfo info, CaptureInfo captureInfo, int interval)
        {
            Configuration.Logger.LogInformation($"Begin to start window watcher");

            //////////////////////////
            // Prepare start info
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

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Configuration.AppDir, "python", "python.exe"),
                Arguments = $"-E -m watcher.window_watcher {info.hwnd.ToInt64()} {captureInfo.ClientType} {captureType} {(canHideBorder ? 1 : 0)} {port}",
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

            //////////////////////////
            // Main loop
            WindowWatcherStart?.Invoke(info.hwnd);
            while (!process.HasExited)
            {
                if (ShouldCancel.Value)
                {
                    KillProcess();
                }
                await Task.Delay(interval);
            }

            Configuration.Logger.LogInformation($"Subprocess terminated with exit code: {process.ExitCode}");
            WindowWatcherExit?.Invoke();
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
                    ParseBackendMessage(message_data, message_str);
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
                ExceptionHandler?.Invoke(ex);
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[ProcessWatcher] {ex.ToString()}");
                ExceptionHandler?.Invoke(ex);
            }
        }

        private void ParseBackendMessage(JToken message_data, string message_str)
        {
            JObject? message_obj = message_data as JObject;
            if (message_obj == null || !message_obj.TryGetValue("type", out JToken? typeValue))
            {
                Configuration.Logger.LogInformation(message_str);
                return;
            }

            string task_type_name = typeValue!.ToString();
            bool valid_task_type = Enum.TryParse(task_type_name, out ETaskType task_type);

            if (!valid_task_type || task_type != ETaskType.LOG_FPS)
            {
                Configuration.Logger.LogInformation(message_str);
            }

            if (!valid_task_type)
            {
                Configuration.Logger.LogWarning($"[ProcessWatcher] Unknown task type: {task_type_name}\n{message_data}");
            }
            else if (task_type == ETaskType.GAME_START)
            {
                GameStarted?.Invoke();
            }
            else if (task_type == ETaskType.MY_PLAYED)
            {
                int card_id = message_data["card_id"]!.ToObject<int>();
                MyActionCardPlayed?.Invoke(card_id);
            }
            else if (task_type == ETaskType.OP_PLAYED)
            {
                int card_id = message_data["card_id"]!.ToObject<int>();
                OpActionCardPlayed?.Invoke(card_id);
            }
            else if (task_type == ETaskType.GAME_OVER)
            {
                GameOver?.Invoke();
            }
            else if (task_type == ETaskType.ROUND)
            {
                int round = message_data["round"]!.ToObject<int>();
                RoundDetected?.Invoke(round);
            }
            else if (task_type == ETaskType.MY_DRAWN)
            {
                int[] cards = message_data["cards"]!.ToObject<int[]>()!;
                MyCardsDrawn?.Invoke(cards);
            }
            else if (task_type == ETaskType.MY_CREATE_DECK)
            {
                int[] cards = message_data["cards"]!.ToObject<int[]>()!;
                MyCardsCreateDeck?.Invoke(cards);
            }
            else if (task_type == ETaskType.OP_CREATE_DECK)
            {
                int[] cards = message_data["cards"]!.ToObject<int[]>()!;
                OpCardsCreateDeck?.Invoke(cards);
            }
            else if (task_type == ETaskType.UNSUPPORTED_RATIO)
            {
                int client_width  = message_data["client_width"]!.ToObject<int>();
                int client_height = message_data["client_height"]!.ToObject<int>();
                float ratio = 1.0f * client_width / client_height;
                Configuration.Logger.LogWarning(
                    $"[ProcessWatcher] Current resolution is {client_width} x {client_height} with ratio = {ratio}, which is not supported now.");
                UnsupportedRatio?.Invoke();
            }
            else if (task_type == ETaskType.CAPTURE_TEST)
            {
                string filename = message_data["filename"]!.ToObject<string>()!;
                int width  = message_data["width"]!.ToObject<int>();
                int height = message_data["height"]!.ToObject<int>();
                CaptureTestDone?.Invoke(filename, width, height);
            }
            else if (task_type == ETaskType.LOG_FPS)
            {
                long cur_fps_time = Stopwatch.GetTimestamp();
                float elapsedSeconds = (cur_fps_time - _last_fps_time.Value) / (float)Stopwatch.Frequency;
                if (elapsedSeconds >= LOG_INTERVAL)
                {
                    Configuration.Logger.LogInformation(message_str);
                    _last_fps_time.Value = cur_fps_time;
                }

                float fps = message_data["fps"]!.ToObject<float>()!;
                LogFPS?.Invoke(fps);
            }
            else
            {
                Configuration.Logger.LogWarning($"[ProcessWatcher] Enum {task_type_name} defined but not handled: {task_type_name}\n{message_data}");
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
