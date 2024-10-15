using LumiTracker.Config;
using LumiTracker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

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
                // TODO: Add OB app UI
            }).Build();

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

            server = new OBServerService();
            Task.Run(server.StartAsync);

            _host.Start();
        }
        // TODO: move to OBStartViewPage
        private OBServerService? server = null;

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            if (!SingleInstanceGuard.IsAppRunning)
            {
                return;
            }

            // TODO: fix server close
            server?.Close();

            await _host.StopAsync();
            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}
