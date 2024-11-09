using Wpf.Ui;
using Wpf.Ui.Controls;

using LumiTracker.Config;
using LumiTracker.Services;
using LumiTracker.OB.Views.Pages;
using LumiTracker.OB.ViewModels.Pages;
using LumiTracker.OB.ViewModels.Windows;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Controls;

namespace LumiTracker.OB.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private IntPtr hwnd = 0;

        private SettingsViewModel _settingsViewModel;

        private readonly IPageService _pageService;

        private readonly ISnackbarService _snackbarService;

        private readonly StyledContentDialogService _contentDialogService;

        private Task? _ShowClosingDialog = null;

        public MainWindow(
            MainWindowViewModel        viewModel,
            SettingsViewModel          settingsViewModel,
            IPageService               pageService,
            INavigationService         navigationService,
            ISnackbarService           snackbarService,
            StyledContentDialogService contentDialogService
        )
        {
            SourceInitialized += MainWindow_SourceInitialized;
            Activated         += MainWindow_Activated;
            Loaded            += MainWindow_Loaded;
            ContentRendered   += MainWindow_ContentRendered;
            Closing           += MainWindow_Closing;
            MouseDown         += MainWindow_MouseDown;

            ShowActivated = false;
            ViewModel     = viewModel;
            DataContext   = this;

            InitializeComponent();

            SetPageService(pageService);
            navigationService.SetNavigationControl(RootNavigation);
            contentDialogService.SetDialogHosts(MainContentDialog, ClosingContentDialog);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            _settingsViewModel = settingsViewModel;
            _pageService = pageService;
            _contentDialogService = contentDialogService;
            _snackbarService = snackbarService;

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

            // Show donate dialog
            var task = _contentDialogService.ShowStartupDialogIfNeededAsync(async () =>
            {
                // Will only do this when just updated
                if (Configuration.Get<bool>("just_updated"))
                {
                    UpdateUtils.CleanCacheAndOldFiles();
                }
                if (Configuration.Get<bool>("check_updates_on_startup"))
                {
                    await _settingsViewModel.OnUpdateButtonClicked();
                }
            });
        }

        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            ShowInTaskbar = true;
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Try to clear selection from the focused control if applicable
            if (FocusManager.GetFocusedElement(this) is DependencyObject focusedElement)
            {
                // Kill logical focus
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(focusedElement), null);
            }
            // Kill keyboard focus
            Keyboard.ClearFocus();
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
            
            if (Configuration.Get<bool>("show_closing_dialog"))
            {
                _ShowClosingDialog = OnShowClosingDialog();
            }
            else
            {
                TryToCloseWindow();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        private async Task OnShowClosingDialog()
        {
            try
            {
                var (result, MinimizeChecked, NotShowAgainChecked) = await _contentDialogService.ShowClosingDialogAsync();
                bool PressedOK = (result == ContentDialogResult.Primary);

                // Save to config
                Configuration.Set("closing_behavior", MinimizeChecked ? "Minimize" : "Quit", auto_save: false);
                if (PressedOK && NotShowAgainChecked)
                {
                    Configuration.Set("show_closing_dialog", false, auto_save: false);
                }
                Configuration.Save();

                // Handle the result here, e.g., save work if Primary button was clicked
                if (PressedOK)
                {
                    await Task.Delay(100); // Wait a while for saving
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
            EClosingBehavior behavior = Configuration.Get<EClosingBehavior>("closing_behavior");
            if (behavior == EClosingBehavior.Minimize)
            {
                ShowInTaskbar = false;
                //WindowState   = WindowState.Minimized;

                const int WM_SYSCOMMAND = 0x0112;
                const int SC_MINIMIZE   = 0xF020;
                SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
            }
            else if (behavior == EClosingBehavior.Quit)
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
