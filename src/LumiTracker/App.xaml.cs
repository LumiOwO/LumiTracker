using LumiTracker.Services;
using LumiTracker.ViewModels.Pages;
using LumiTracker.ViewModels.Windows;
using LumiTracker.Views.Pages;
using LumiTracker.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using System.Windows.Media;
using Wpf.Ui;

using Microsoft.Extensions.Logging;
using LumiTracker.Config;
using LumiTracker.Models;
using Wpf.Ui.Appearance;
using Microsoft.Win32;
using System.IO.Pipes;
using System.Diagnostics;

namespace LumiTracker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!); })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<ApplicationHostService>();

                // Page resolver service
                services.AddSingleton<IPageService, PageService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ISnackbarService, SnackbarService>();
                services.AddSingleton<StyledContentDialogService>();
                services.AddSingleton<UpdateService>();

                // Localization
                services.AddSingleton<ILocalizationService, LocalizationService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                // Deck window
                services.AddSingleton<IDeckWindow, DeckWindow>();
                services.AddSingleton<DeckWindowViewModel>();

                services.AddSingleton<StartPage>();
                services.AddSingleton<StartViewModel>();
                services.AddSingleton<DeckPage>();
                services.AddSingleton<DeckViewModel>();
                services.AddSingleton<RankPage>();
                services.AddSingleton<RankViewModel>();
                services.AddSingleton<AboutPage>();
                services.AddSingleton<AboutViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();

                services.AddSingleton<GameWatcher>();

            }).Build();

        public App()
        {
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        /// <summary>
        /// Gets registered service.
        /// </summary>
        /// <typeparam name="T">Type of the service to get.</typeparam>
        /// <returns>Instance of the service or <see langword="null"/>.</returns>
        public static T? GetService<T>()
            where T : class
        {
            return _host.Services.GetService(typeof(T)) as T;
        }

        private static Mutex? mutex = null;

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private void OnStartup(object sender, StartupEventArgs e)
        {
            // Ensure Singleton
            string appName = Assembly.GetExecutingAssembly().GetName().Name!;
            bool createdNew;
            mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                // Application is already running
                SignalFirstInstance();
                Application.Current.Shutdown();
                return;
            }

            // Start listening for pipe messages asynchronously
            Task.Run(() => ListenForPipeMessagesAsync());

            if (e.Args.Length == 1 && e.Args[0] == "just_updated")
            {
                Configuration.SetTemporal("just_updated", true);
            }
            // refresh theme
            ApplicationThemeManager.Apply(Configuration.Get<ApplicationTheme>("theme"));
            // Overwrite accent color
            ApplicationAccentColorManager.Apply(
                Color.FromArgb(0xff, 0x1c, 0xdd, 0xe9),
                ApplicationTheme.Dark
            );

            _host.Start();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();

            // Release the mutex before starting a new instance
            mutex!.ReleaseMutex();
            mutex!.Dispose();

            if (Configuration.Get<bool>("restart"))
            {
                string launcherPath = Path.Combine(Configuration.RootDir, "LumiTracker.exe");
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = launcherPath,
                    Arguments = Configuration.RootDir + "\\",
                    UseShellExecute = false,  // Required to set CreateNoWindow to true
                    CreateNoWindow = true,   // Hides the console window
                };

                var process = new Process();
                process.StartInfo = startInfo;
                if (!process.Start())
                {
                    Configuration.Logger.LogError("[Update] Failed to Restart app.");
                }
            }
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
            Configuration.Logger.LogError($"[App] {e.Exception.ToString()}");
            MessageBox.Show($"App error occurred: {e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            Configuration.Logger.LogDebug($"SystemEvents_SessionSwitch Reason={e.Reason}");
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    // Screen locked, application might be suspended
                    break;
                case SessionSwitchReason.SessionUnlock:
                    // Screen unlocked, application might be resumed
                    break;
                case SessionSwitchReason.SessionLogoff:
                    // User logged off
                    break;
                case SessionSwitchReason.SessionLogon:
                    // User logged on
                    break;
                case SessionSwitchReason.RemoteDisconnect:
                    // Remote session disconnected
                    break;
                case SessionSwitchReason.RemoteConnect:
                    // Remote session connected
                    break;
                default:
                    // Other session switch reasons
                    break;
            }
        }

        private void SignalFirstInstance()
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", "Pipe_" + Assembly.GetExecutingAssembly().GetName().Name, PipeDirection.Out))
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
                using (NamedPipeServerStream server = new NamedPipeServerStream("Pipe_" + Assembly.GetExecutingAssembly().GetName().Name, PipeDirection.In))
                {
                    await server.WaitForConnectionAsync();

                    using (StreamReader reader = new StreamReader(server))
                    {
                        string? message = await reader.ReadLineAsync();
                        if (message != null && message == "Activate")
                        {
                            Dispatcher.Invoke(() => BringToFront());
                        }
                    }
                }
            }
        }

        private void BringToFront()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }
            mainWindow.Activate();
            mainWindow.Topmost = true;  // set topmost
            mainWindow.Topmost = false; // remove topmost
            mainWindow.Focus();         // set focus
        }
    }
}
