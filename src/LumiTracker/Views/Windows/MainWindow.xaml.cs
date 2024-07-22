using LumiTracker.ViewModels.Windows;
using LumiTracker.Watcher;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using Microsoft.Extensions.Logging;
using LumiTracker.Controls;
using LumiTracker.Views.Pages;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Wpf.Ui.Extensions;
using Microsoft.Extensions.Options;
using System.Threading;

namespace LumiTracker.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private IntPtr hwnd = 0;

        private readonly IPageService _pageService;

        private readonly IContentDialogService _contentDialogService;

        private Task? _ShowClosingDialog = null;

        public MainWindow(
            MainWindowViewModel   viewModel,
            IPageService          pageService,
            INavigationService    navigationService,
            IContentDialogService contentDialogService
        )
        {
            SourceInitialized += MainWindow_SourceInitialized;
            Activated         += MainWindow_Activated;
            Loaded            += MainWindow_Loaded;
            ContentRendered   += MainWindow_ContentRendered;
            Closing           += MainWindow_Closing;

            ShowActivated = false;
            ViewModel     = viewModel;
            DataContext   = this;

            InitializeComponent();

            SetPageService(pageService);
            navigationService.SetNavigationControl(RootNavigation);
            contentDialogService.SetDialogHost(RootContentDialog);

            _pageService = pageService;
            _contentDialogService = contentDialogService;

            TrayIcon.Menu!.DataContext = this;
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

            ViewModel.Init(TrayMenuItemClickedCommand);

            StartPage startPage = (
                _pageService.GetPage<StartPage>()
            )!;
            startPage.Init();

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
            e.Cancel = true; // Cancel the default close operation

            if (_ShowClosingDialog != null)
            {
                return;
            }

            var closingDialog = new ClosingDialog();
            closingDialog.DataContext = this;
            _ShowClosingDialog = OnShowClosingDialog(closingDialog);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        [RelayCommand]
        private async Task OnShowClosingDialog(object content)
        {
            try
            {
                Configuration.Logger.LogDebug("OnShowClosingDialog");
                ContentDialogResult result = await _contentDialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions()
                    {
                        Title = "Save your work?",
                        Content = content,
                        PrimaryButtonText = "Save",
                        //SecondaryButtonText = "Don't Save",
                        CloseButtonText = "Cancel",
                    }
                );

                string dialogResultText = result switch
                {
                    ContentDialogResult.Primary => "User saved their work",
                    ContentDialogResult.Secondary => "User did not save their work",
                    _ => "User cancelled the dialog"
                };
                Configuration.Logger.LogDebug(dialogResultText);

                // Handle the result here, e.g., save work if Primary button was clicked

                if (result == ContentDialogResult.Primary)
                {
                    TryToCloseWindow();
                }
            }
            finally
            {
                // Ensure the task variable is reset to null
                _ShowClosingDialog = null;
            }
        }

        private void TryToCloseWindow()
        {
            string behavior = Configuration.Data.closing_behavior;
            if (behavior == "Minimize")
            {
                ShowInTaskbar = false;
                //WindowState   = WindowState.Minimized;

                const int WM_SYSCOMMAND = 0x0112;
                const int SC_MINIMIZE   = 0xF020;
                SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
            }
            else if (behavior == "Quit")
            {
                Application.Current.Shutdown();
            }
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
        public void TrayMenuItemClicked(string parameter)
        {
            if (parameter == "tray_home")
            {
                TrayShowMainWindow();
            }
            else if (parameter == "tray_quit")
            {
                TrayExit();
            }
        }

        public void TrayShowMainWindow()
        {
            Configuration.Logger.LogDebug("Tray clicked");

            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Focus();
        }

        public void TrayExit()
        {
            Closing -= MainWindow_Closing;
            Close();
        }
    }
}
