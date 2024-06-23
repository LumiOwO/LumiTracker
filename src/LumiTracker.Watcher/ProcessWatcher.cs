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

    public delegate void OnMyEventCardCallback(int card_id);

    public delegate void OnOpEventCardCallback(int card_id);

    public delegate void OnUnsupportedRatioCallback();

    public delegate void ExceptionHandlerCallback(Exception ex);

    public class ProcessWatcher : IAsyncDisposable
    {
        private readonly ILogger logger;
        private ConfigData cfg;

        public event OnGenshinWindowFoundCallback? GenshinWindowFound;
        public event OnWindowWatcherStartCallback? WindowWatcherStart;
        public event OnWindowWatcherExitCallback?  WindowWatcherExit;
        public event OnGameStartedCallback? GameStarted;
        public event OnMyEventCardCallback? MyEventCard;
        public event OnOpEventCardCallback? OpEventCard;
        public event OnUnsupportedRatioCallback? UnsupportedRatio;
        public event ExceptionHandlerCallback? ExceptionHandler;


        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        public ProcessWatcher(ILogger logger, ConfigData cfg)
        {
            this.logger = logger;
            this.cfg    = cfg;
        }

        public WindowInfo FindProcessWindow(string processName)
        {
            var res = new WindowInfo();

            var processes = GetProcessByName(processName);
            if (processes.Count == 0)
            {
                logger.LogInformation($"No process found with name: {processName}");
                return res;
            }

            if (processes.Count > 1)
            {
                logger.LogWarning($"Found multiple processes with name: {processName}, using the first one");
            }
            var proc = processes[0];

            var info = GetMainWindowInfo(proc);
            if (info.hwnd == 0)
            {
                logger.LogInformation($"No windows found for process '{processName}' (PID: {proc.Id})");
                return res;
            }
            GenshinWindowFound?.Invoke();

            var foregroundHwnd = GetForegroundWindow();
            if (info.hwnd != foregroundHwnd)
            {
                logger.LogInformation($"Window for process '{processName}' (PID: {proc.Id}) is not foreground");
                return res;
            }

            logger.LogInformation($"Window title for process '{processName}' (PID: {proc.Id}): {info.title}");
            
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
                logger.LogInformation($"###Main window: hwnd={info.hwnd}, title={info.title}");
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
                int interval = cfg.proc_watch_interval * 1000;
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
                Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [ProcessWatcher] {ex.Message}\n{ex.StackTrace}");
                ExceptionHandler?.Invoke(ex);
            }
        }

        public async Task StartWindowWatcher(WindowInfo info, string processName, int interval)
        {
            logger.LogInformation($"Begin to start window watcher");

            string captureType = "BitBlt"; // default 
            if (processName == "Genshin Impact Cloud Game.exe")
            {
                captureType = "WindowsCapture"; // Genshin cloud cannot captured by bitblt
            }

            bool canHideBorder = ApiInformation.IsPropertyPresent(
                "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");

            var startInfo = new ProcessStartInfo
            {
                FileName = "python/python.exe",
                Arguments = $"-E -m watcher.window_watcher {info.hwnd.ToInt64()} {captureType} {(canHideBorder ? 1 : 0)}",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var process = new Process();
            process.StartInfo = startInfo;
            process.ErrorDataReceived += WindowWatcherEventHandler;

            if (!process.Start())
            {
                logger.LogError("Failed to start subprocess.");
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

            logger.LogInformation($"Subprocess terminated with exit code: {process.ExitCode}");
            WindowWatcherExit?.Invoke();
        }

        public enum ETaskType
        {
            GAME_START = 0,
            MY_EVENT,
            OP_EVENT,
            GAME_OVER,

            // types for exception
            UNSUPPORTED_RATIO,
        }

        private void WindowWatcherEventHandler(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e.Data == null) return;
                if (Configuration.Data.DEBUG)
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
                        Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [ProcessWatcher] Unknown task type: {task_type_name}");
                        Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [ProcessWatcher] {message}");
                    }
                    else if (task_type == ETaskType.GAME_START)
                    {
                        GameStarted?.Invoke();
                    }
                    else if (task_type == ETaskType.MY_EVENT)
                    {
                        int card_id = (int)message_data["card_id"]!;
                        MyEventCard?.Invoke(card_id);
                    }
                    else if (task_type == ETaskType.OP_EVENT)
                    {
                        int card_id = (int)message_data["card_id"]!;
                        OpEventCard?.Invoke(card_id);
                    }
                    else if (task_type == ETaskType.UNSUPPORTED_RATIO)
                    {
                        UnsupportedRatio?.Invoke();
                    }
                    else
                    {
                        Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [ProcessWatcher] Enum {task_type_name} defined but not handled");
                        Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [ProcessWatcher] {message}");
                    }
                }
                else if (message_level == "WARNING" || message_level == "ERROR")
                {
                    Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [ProcessWatcher] {message}");
                }
            }
            catch (JsonReaderException ex)
            {
                Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [python] {e.Data}");
                ExceptionHandler?.Invoke(ex);
            }
            catch (Exception ex)
            {
                Configuration.ErrorWriter.WriteLine($"[{DateTime.Now}] [ProcessWatcher] {ex.Message}\n{ex.StackTrace}");
                ExceptionHandler?.Invoke(ex);
            }
        }
    }

    // Main method to start the process watcher
    public static class Program
    {
        public static void Main(string[] args)
        {
            var logger = Configuration.Logger;
            var cfg = Configuration.Data;

            var processWatcher = new ProcessWatcher(logger, cfg);
            processWatcher.Start("YuanShen.exe");
        }
    }

}
