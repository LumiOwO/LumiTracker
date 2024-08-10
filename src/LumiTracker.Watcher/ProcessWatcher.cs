using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using LumiTracker.Config;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Windows;
using Newtonsoft.Json;
using Windows.Foundation.Metadata;


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
                Configuration.Logger.LogInformation($"No process found with name: {processName}");
                return res;
            }

            if (processes.Count > 1)
            {
                Configuration.Logger.LogWarning($"Found multiple processes with name: {processName}, using the first one");
            }
            var proc = processes[0];

            var info = GetMainWindowInfo(proc);
            if (info.hwnd == 0)
            {
                Configuration.Logger.LogInformation($"No windows found for process '{processName}' (PID: {proc.Id})");
                return res;
            }
            GenshinWindowFound?.Invoke();

            var foregroundHwnd = GetForegroundWindow();
            if (info.hwnd != foregroundHwnd)
            {
                Configuration.Logger.LogInformation($"Window for process '{processName}' (PID: {proc.Id}) is not foreground");
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
                processes.Add(proc);
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
                Configuration.Logger.LogInformation($"###Main window: hwnd={info.hwnd}, title={info.title}");
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

            string captureType = "BitBlt"; // default 
            if (processName == "Genshin Impact Cloud Game.exe")
            {
                captureType = "WindowsCapture"; // Genshin cloud cannot captured by bitblt
            }

            bool canHideBorder = ApiInformation.IsPropertyPresent(
                "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Configuration.AppDir, "python", "python.exe"),
                Arguments = $"-E -m watcher.window_watcher {info.hwnd.ToInt64()} {captureType} {(canHideBorder ? 1 : 0)}",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Configuration.AppDir,
            };

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

            WindowWatcherStart?.Invoke(info.hwnd);
            while (!process.HasExited)
            {
                if (ShouldCancel.Value)
                {
                    process.Kill();
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
                if (Configuration.Get<bool>("DEBUG"))
                {
                    Console.WriteLine(e.Data);
                }

                JObject message = JObject.Parse(e.Data);
                string message_level = message["level"]!.ToString();
                if (message_level == "INFO")
                {
                    var message_data = message["data"]!;

                    string task_type_name = message_data["type"]!.ToString();
                    if (!Enum.TryParse(task_type_name, out ETaskType task_type))
                    {
                        Configuration.Logger.LogWarning($"[ProcessWatcher] Unknown task type: {task_type_name}\n{message}");
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
                        int client_width  = message_data["client_width" ]!.ToObject<int>();
                        int client_height = message_data["client_height"]!.ToObject<int>();
                        float ratio = 1.0f * client_width / client_height;
                        Configuration.Logger.LogWarning(
                            $"[ProcessWatcher] Current resolution is {client_width} x {client_height} with ratio = {ratio}, which is not supported now.");
                        UnsupportedRatio?.Invoke();
                    }
                    else
                    {
                        Configuration.Logger.LogWarning($"[ProcessWatcher] Enum {task_type_name} defined but not handled: {task_type_name}\n{message}");
                    }
                }
                else if (message_level == "WARNING")
                {
                    Configuration.Logger.LogWarning($"[ProcessWatcher] {message}");
                }
                else if (message_level == "ERROR")
                {
                    Configuration.Logger.LogError($"[ProcessWatcher] {message}");
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
