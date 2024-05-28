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

namespace LumiTracker.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly IServiceProvider _serviceProvider;

        private readonly ILocalizationService _localizationService;

        public MainWindow(
            MainWindowViewModel  viewModel,
            IPageService         pageService,
            INavigationService   navigationService,
            IServiceProvider     serviceProvider,
            ILocalizationService localizationService
        )
        {
            SourceInitialized += MainWindow_SourceInitialized;
            Activated         += MainWindow_Activated;
            Loaded            += MainWindow_Loaded;
            ContentRendered   += MainWindow_ContentRendered;

            ShowActivated        = false;
            ViewModel            = viewModel;
            DataContext          = this;
            _serviceProvider     = serviceProvider;
            _localizationService = localizationService;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(pageService);

            navigationService.SetNavigationControl(RootNavigation);
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
            var cfg = Configuration.Data;
            _localizationService.ChangeLanguage(cfg.lang);

            Enum.TryParse(cfg.theme, out ApplicationTheme curTheme);
            ApplicationThemeManager.Apply(curTheme);

            // TODO
            Task task = Program.Main(new string[] { });

            Window window = (_serviceProvider.GetService(typeof(Window)) as Window)!;
            window.Show();

            RootNavigation.Navigate(typeof(DashboardPage));
        }

        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
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
    }
}
