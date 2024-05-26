namespace LumiTracker.Watcher
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    using LumiTracker.Config;

    public class WindowInfo
    {
        public IntPtr hwnd { get; set; }  = IntPtr.Zero;
        public string title { get; set; } = "";
    }

    public class ProcessWatcher
    {
        private readonly ILogger<ProcessWatcher> _logger;
        private readonly Config _cfg;

        public ProcessWatcher(ILogger<ProcessWatcher> logger, Config cfg)
        {
            _logger = logger;
            _cfg = cfg;
        }

        public async Task StartWindowWatcher(WindowInfo info)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "python/python.exe",
                Arguments = $"-E -m watcher.window_watcher {info.hwnd.ToInt64()} {info.title}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var process = new Process();
            process.StartInfo = startInfo;
            process.OutputDataReceived += (sender, e) => Console.WriteLine("Output: " + e.Data);
            process.ErrorDataReceived += (sender, e) => Console.WriteLine("Error: " + e.Data);
            
            if (!process.Start())
            {
                _logger.LogError("Failed to start subprocess.");
                return;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());
            _logger.LogInformation($"Subprocess terminated with exit code: {process.ExitCode}");
        }

        public async Task Start()
        {
            int interval = _cfg.proc_watch_interval * 1000;
            while (true)
            {
                var info = FindProcessWindow();
                if (info.hwnd != IntPtr.Zero)
                {
                    await StartWindowWatcher(info);
                }

                await Task.Delay(interval);
            }
        }

        public WindowInfo FindProcessWindow()
        {
            var processName = _cfg.proc_name;
            var info = new WindowInfo();

            var processes = GetProcessByName(processName);
            if (processes.Count == 0)
            {
                _logger.LogInformation($"No process found with name: {processName}");
                return info;
            }

            if (processes.Count > 1)
            {
                _logger.LogWarning($"Found multiple processes with name: {processName}, using the first one");
            }

            var proc = processes[0];
            var infos = GetWindowsByPID(proc.Id);

            if (infos.Count == 0)
            {
                _logger.LogInformation($"No windows found for process '{processName}' (PID: {proc.Id})");
                return info;
            }

            var foregroundHwnd = GetForegroundWindow();
            if (infos[0].hwnd != foregroundHwnd)
            {
                _logger.LogInformation($"Window for process '{processName}' (PID: {proc.Id}) is not foreground");
                return info;
            }

            info = infos[0];
            _logger.LogInformation($"Window titles for process '{processName}' (PID: {proc.Id}):");
            foreach (var i in infos)
            {
                _logger.LogInformation($"  - {i.title}");
            }

            return info;
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

        private List<WindowInfo> GetWindowsByPID(int pid)
        {
            var infos = new List<WindowInfo>();

            bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam)
            {
                GetWindowThreadProcessId(hwnd, out var processId);
                if (processId == pid && IsWindowVisible(hwnd))
                {
                    var info = new WindowInfo
                    {
                        hwnd = hwnd,
                        title = GetWindowText(hwnd)
                    };
                    infos.Add(info);
                }
                return true;
            }

            EnumWindows(EnumWindowsProc, IntPtr.Zero);
            return infos;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static string GetWindowText(IntPtr hWnd)
        {
            const int nChars = 256;
            var Buff = new StringBuilder(nChars);
            return GetWindowText(hWnd, Buff, nChars) > 0 ? Buff.ToString() : "";
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    }

    // Main method to start the process watcher
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<ProcessWatcher>();

            var cfg = Config.Instance;

            var processWatcher = new ProcessWatcher(logger, cfg);
            await processWatcher.Start();
        }
    }

}
