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

    public delegate void OnCaptureTestDoneCallback(string filename);

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
        public event ExceptionHandlerCallback?     ExceptionHandler;


        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        public ProcessWatcher()
        {

        }

        public WindowInfo FindProcessWindow(string processName)
        {
            var res = new WindowInfo();

            var processes = GetProcessByName(processName);
            if (processes.Count == 0)
            {
                Configuration.Logger.LogDebug($"No process found with name: {processName}");
                return res;
            }

            if (processes.Count > 1)
            {
                Configuration.Logger.LogWarning($"Found {processes.Count} processes with name: {processName}, using the first one");
            }
            var proc = processes[0];

            var info = GetMainWindowInfo(proc);
            if (info.hwnd == 0)
            {
                Configuration.Logger.LogDebug($"No windows found for process '{processName}' (PID: {proc.Id})");
                return res;
            }
            GenshinWindowFound?.Invoke();

            var foregroundHwnd = GetForegroundWindow();
            if (info.hwnd != foregroundHwnd)
            {
                Configuration.Logger.LogDebug($"Window for process '{processName}' (PID: {proc.Id}) is not foreground");
                return res;
            }

            Configuration.Logger.LogInformation($"Window title for process '{processName}' (PID: {proc.Id}): {info.title}");
            
            res = info;
            return res;
        }

        private List<Process> GetProcessByName(string processName)
        {
            var processes = new List<Process>();
            foreach (var proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)))
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    processes.Add(proc);
                }
            }

            return processes;
        }

        private WindowInfo GetMainWindowInfo(Process process)
        {
            var info = new WindowInfo();

            if (process != null)
            {
                IntPtr hwnd = process.MainWindowHandle;
                if (IsWindow(hwnd))
                {
                    info.hwnd  = hwnd;
                    info.title = GetWindowText(hwnd);
                }
                Configuration.Logger.LogDebug($"Main window: hwnd={info.hwnd}, title={info.title}");
            }

            return info;
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            const int nChars = 256;
            var Buff = new StringBuilder(nChars);
            return GetWindowText(hWnd, Buff, nChars) > 0 ? Buff.ToString() : "";
        }

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

        public void Start(string processName)
        {
            _processWatcherTask = StartProcessWatcher(processName);
        }

        public async Task StartProcessWatcher(string processName)
        {
            Configuration.Logger.LogInformation($"Begin to find process: {processName}");
            try
            {
                int interval = Configuration.Get<int>("proc_watch_interval") * 1000;
                while (!ShouldCancel.Value)
                {
                    var info = FindProcessWindow(processName);
                    if (info.hwnd != IntPtr.Zero)
                    {
                        _windowWatcherTask = StartWindowWatcher(info, processName, interval);
                        await _windowWatcherTask;
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

        public async Task StartWindowWatcher(WindowInfo info, string processName, int interval)
        {
            Configuration.Logger.LogInformation($"Begin to start window watcher");

            //////////////////////////
            // Prepare start info
            string captureType = 
                processName == EClientProcessNames.Values[(int)EClientType.Cloud] ?
                ECaptureType.WindowsCapture.ToString() :
                Configuration.Get<ECaptureType>("capture_type").ToString();

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
                Arguments = $"-E -m watcher.window_watcher {info.hwnd.ToInt64()} {captureType} {(canHideBorder ? 1 : 0)} {port}",
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

                bool forceIndent   = (message_level != "DEBUG");
                string message_str = $"{LogHelper.AnsiMagenta}@{LogHelper.AnsiEnd} ";
                message_str += LogHelper.JsonToConsoleStr(message_data, forceIndent);

                if (message_level == "INFO")
                {
                    Configuration.Logger.LogInformation(message_str);
                    ParseBackendMessage(message_data);
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

        private void ParseBackendMessage(JToken message_data)
        {
            string task_type_name = message_data["type"]!.ToString();
            if (!Enum.TryParse(task_type_name, out ETaskType task_type))
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
                CaptureTestDone?.Invoke(filename);
            }
            else if (task_type == ETaskType.LOG_FPS)
            {
                // TODO: add hook for fps logging

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
            processWatcher.Start("YuanShen.exe");
        }
    }

}
