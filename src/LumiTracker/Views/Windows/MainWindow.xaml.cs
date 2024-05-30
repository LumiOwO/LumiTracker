using LumiTracker.ViewModels.Windows;
using LumiTracker.Watcher;
using System.Globalization;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LumiTracker.Services;
using LumiTracker.Views.Pages;
using System.Windows.Navigation;
using LumiTracker.ViewModels.Pages;
using System.Windows.Controls;
using System.Windows;
using Windows.System;
using Wpf.Ui.Tray.Controls;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LumiTracker.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private IntPtr hwnd = 0;

        public MainWindow(
            MainWindowViewModel  viewModel,
            IPageService         pageService,
            INavigationService   navigationService
        )
        {
            SourceInitialized += MainWindow_SourceInitialized;
            Activated         += MainWindow_Activated;
            Loaded            += MainWindow_Loaded;
            ContentRendered   += MainWindow_ContentRendered;
            Closing           += MainWindow_Closing;

            ShowActivated        = false;
            ViewModel            = viewModel;
            DataContext          = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(pageService);

            navigationService.SetNavigationControl(RootNavigation);

            TrayIcon.Menu!.DataContext = this;

            int n = ViewModel.TrayMenuItems.Count;
            ViewModel.TrayMenuItems[0].Command     = ShowMainWindowCommand;
            ViewModel.TrayMenuItems[n - 1].Command = ExitCommand;
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.SetPageService(pageService);

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        // https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/?view=netdesktop-8.0#window-lifetime-events
        // SourceInitialized --> [(if ShowActivated) Activated] --> Loaded --> ContentRendered --> Activated <--> Deactivated
        // !!! Loaded may happens before Activated (tested), so ensure ** ShowActivated = false ** !!!

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            hwnd = new WindowInteropHelper(this).Handle;

            ViewModel.Init();

            RootNavigation.Navigate(typeof(StartPage));
        }

        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            ShowInTaskbar = true;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            string behavior = Configuration.Data.closing_behavior;
            if (behavior == "Minimize")
            {
                e.Cancel      = true; // Cancel the default close operation
                ShowInTaskbar = false;
                //WindowState   = WindowState.Minimized;

                const int WM_SYSCOMMAND = 0x0112;
                const int SC_MINIMIZE   = 0xF020;
                SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
            }
            else if (behavior == "Quit")
            {
                e.Cancel      = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            // TODO: config
            Application.Current.Shutdown();
        }

        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        [RelayCommand]
        public void ShowMainWindow()
        {
            Configuration.Logger.LogDebug("Tray clicked");

            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Focus();
        }

        [RelayCommand]
        public void Exit()
        {
            Closing -= MainWindow_Closing;
            Close();
        }
    }
}
