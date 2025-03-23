using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using LumiTracker.Config;

namespace LumiTracker
{
    public ref struct SpinLockGuard
    {
        private ref SpinLock _spinLock; // Ref to original SpinLock
        private bool _lockTaken;

        public SpinLockGuard(ref SpinLock spinLock)
        {
            _spinLock = ref spinLock; // Store reference, NOT a copy
            _lockTaken = false;
            _spinLock.Enter(ref _lockTaken); // Acquire lock
        }

        public void Dispose()
        {
            if (_lockTaken)
            {
                _spinLock.Exit(); // Release the original SpinLock
                _lockTaken = false;
            }
        }

        public static void Scope(ref SpinLock spinLock, Action func)
        {
            using (new SpinLockGuard(ref spinLock))
            {
                func();
            }
        }

        public static T Scope<T>(ref SpinLock spinLock, Func<T> func)
        {
            using (new SpinLockGuard(ref spinLock))
            {
                return func();
            }
        }
    }

    public class AppSingleInstanceGuard
    {
        private Mutex? mutex = null;

        private Application? App = null;

        public bool IsAppRunning { get; private set; } = false;

        public void Init(Application app)
        {
            App = app;

            bool createdNew;
            mutex = new Mutex(true, Configuration.AppName, out createdNew);
            if (!createdNew)
            {
                // Application is already running
                SignalFirstInstance();
                Application.Current.Shutdown();
                return;
            }

            // Start listening for pipe messages asynchronously
            Task.Run(() => ListenForPipeMessagesAsync());
            IsAppRunning = true;
        }

        private void SignalFirstInstance()
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", "Pipe_" + Configuration.AppName, PipeDirection.Out))
                {
                    client.Connect(1000); // Try to connect for 1 second
                    using (StreamWriter writer = new StreamWriter(client))
                    {
                        writer.WriteLine("Activate");
                    }
                }
            }
            catch (Exception)
            {
                // Just exit
            }
        }

        private async Task ListenForPipeMessagesAsync()
        {
            while (true)
            {
                using (NamedPipeServerStream server = new NamedPipeServerStream("Pipe_" + Configuration.AppName, PipeDirection.In))
                {
                    await server.WaitForConnectionAsync();

                    using (StreamReader reader = new StreamReader(server))
                    {
                        string? message = await reader.ReadLineAsync();
                        if (message != null && message == "Activate")
                        {
                            App!.Dispatcher.Invoke(() => BringToFront());
                        }
                    }
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private void BringToFront()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }
                mainWindow.Activate();
                mainWindow.Topmost = true;  // set topmost
                mainWindow.Topmost = false; // remove topmost
                mainWindow.Focus();         // set focus
            }
            else
            {
                IntPtr consoleWindow = GetConsoleWindow();
                if (consoleWindow != IntPtr.Zero)
                {
                    // Show and restore the console window if minimized
                    const int SW_RESTORE = 9;    // Restore window
                    ShowWindow(consoleWindow, SW_RESTORE);
                    // Give a moment for the console window to show
                    Thread.Sleep(100);

                    bool success = SetForegroundWindow(consoleWindow);
                }
            }
        }
    }
}
