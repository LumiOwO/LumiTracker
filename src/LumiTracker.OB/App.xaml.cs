using LumiTracker.Config;
using LumiTracker.OB.ViewModels.Pages;
using LumiTracker.OB.ViewModels.Windows;
using LumiTracker.OB.Views.Pages;
using LumiTracker.OB.Views.Windows;
using LumiTracker.OB.Services;
using LumiTracker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace LumiTracker.OB
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
                services.AddHostedService<ApplicationHostService<MainWindow>>();

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
                services.AddSingleton<OBServerService>();

                // Localization
                services.AddSingleton<ILocalizationService, LocalizationService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<StartPage>();
                services.AddSingleton<StartViewModel>();
                services.AddSingleton<DuelPage>();
                services.AddSingleton<DuelViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();

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

        private AppSingleInstanceGuard SingleInstanceGuard = new();

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private void OnStartup(object sender, StartupEventArgs e)
        {
            // Configure custom file paths
            if (!Directory.Exists(Configuration.OBWorkingDir))
            {
                Directory.CreateDirectory(Configuration.OBWorkingDir);
            }
            Configuration.DefaultConfigPath = Path.Combine(Configuration.AssetsDir, "obconfig.json");
            Configuration.UserConfigPath    = Path.Combine(Configuration.OBWorkingDir, "obconfig.json");
            Configuration.LogFilePath       = Path.Combine(Configuration.OBWorkingDir, "error.log");

            SingleInstanceGuard.Init(this);
            if (!SingleInstanceGuard.IsAppRunning)
            {
                return;
            }

            if (e.Args.Length == 1 && e.Args[0] == "just_updated")
            {
                Configuration.SetTemporal("just_updated", true);
            }
            ApplicationThemeService.ChangeThemeTo(Configuration.Get<ApplicationTheme>("theme"));

            _host.Start();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            if (!SingleInstanceGuard.IsAppRunning)
            {
                return;
            }

            await _host.StopAsync();
            _host.Dispose();

            if (Configuration.Get<string>("updated_to") is string version && !string.IsNullOrEmpty(version))
            {
                // Start server app
                string launcherPath = Path.Combine(Configuration.RootDir, $"LumiTrackerApp-{version}", "LumiTrackerOB.exe");
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = launcherPath,
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
    }
}
